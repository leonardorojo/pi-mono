#!/usr/bin/env node
import { existsSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { spawn } from "node:child_process";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const repoRoot = resolve(__dirname, "..");
const piTestPath = join(repoRoot, "pi-test.sh");
const rckRoot = join(repoRoot, ".pi", "rck");
const rpcArgs = ["--offline", "--mode", "rpc", "--no-tools", "--no-extensions", "--extension", ".pi/extensions/rck-bridge/index.ts"];
const timeoutMs = Number.parseInt(process.env.RUFUSCHAT_SHELL_TIMEOUT_MS ?? "45000", 10);

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

function cleanupArtifacts() {
	rmSync(rckRoot, { recursive: true, force: true });
}

function readRepoJsonMaybe(relativePath) {
	const filePath = join(repoRoot, relativePath);
	if (!existsSync(filePath)) {
		return null;
	}
	return JSON.parse(readFileSync(filePath, "utf8"));
}

function loadJsonRecords(relativeDir) {
	const dirPath = join(repoRoot, relativeDir);
	if (!existsSync(dirPath)) {
		return [];
	}
	return readdirSync(dirPath)
		.filter((file) => file.endsWith(".json"))
		.sort()
		.map((file) => ({
			file,
			json: JSON.parse(readFileSync(join(dirPath, file), "utf8")),
		}));
}

function parseJsonMaybe(line) {
	try {
		return JSON.parse(line);
	} catch {
		return null;
	}
}

function createLineSplitter(onLine) {
	let buffer = "";
	return {
		push(chunk) {
			buffer += chunk;
			for (;;) {
				const index = buffer.indexOf("\n");
				if (index === -1) {
					return;
				}
				const line = buffer.slice(0, index).replace(/\r$/, "");
				buffer = buffer.slice(index + 1);
				onLine(line);
			}
		},
		flush() {
			const remainder = buffer.replace(/\r$/, "");
			buffer = "";
			if (remainder) {
				onLine(remainder);
			}
		},
	};
}

function countJsonFiles(relativeDir) {
	const dirPath = join(rckRoot, relativeDir);
	if (!existsSync(dirPath)) {
		return 0;
	}
	return readdirSync(dirPath).filter((file) => file.endsWith(".json")).length;
}

function latestEvent(events, eventType) {
	for (let index = events.length - 1; index >= 0; index -= 1) {
		if (events[index]?.eventType === eventType) {
			return events[index];
		}
	}
	return undefined;
}

function buildUsage() {
	return [
		"Usage:",
		"  node scripts/rufuschat-shell.mjs",
		"  node scripts/rufuschat-shell.mjs --help",
		"  node scripts/rufuschat-shell.mjs --status",
		"  node scripts/rufuschat-shell.mjs --supervise",
		"  node scripts/rufuschat-shell.mjs --list",
		"  node scripts/rufuschat-shell.mjs --state",
		"  node scripts/rufuschat-shell.mjs --inject",
		"  node scripts/rufuschat-shell.mjs --anchor <label>",
		"  node scripts/rufuschat-shell.mjs --run-fake <prompt>",
		"  node scripts/rufuschat-shell.mjs --demo",
		"",
		"Default behavior:",
		"  No flags = read-only shell that runs /rck status and /rck supervise.",
		"",
		"Read-only flags:",
		"  --status     Run /rck status.",
		"  --supervise  Run /rck supervise.",
		"  --list       Run /rck list.",
		"",
		"Explicit mutating flags:",
		"  --state                 Run /state.",
		"  --inject                Run /rck inject.",
		"  --anchor <label>        Run /rck anchor <label>.",
		"  --run-fake <prompt>     Run /hermes <prompt>.",
		"  --demo                  Run the explicit demo flow:",
		"                         /state -> /rck inject -> /rck anchor rufuschat-shell -> /hermes inspect fake bridge -> /rck supervise",
		"",
		"Safety:",
		"  - No raw stdout/stderr is shown.",
		"  - Evidence is kept to safe refs / metadata.",
		"  - No web UI, no Codex, no RufusLab.RCK.Cli.",
	].join("\n");
}

function parseArgs(argv) {
	const actions = [];
	const errors = [];
	let help = false;
	let demoRequested = false;

	for (let index = 0; index < argv.length; index += 1) {
		const arg = argv[index];
		if (arg === "--help" || arg === "-h") {
			help = true;
			continue;
		}
		if (arg === "--demo") {
			demoRequested = true;
			actions.push({ kind: "demo" });
			continue;
		}
		if (arg === "--status") {
			actions.push({ kind: "status" });
			continue;
		}
		if (arg === "--supervise") {
			actions.push({ kind: "supervise" });
			continue;
		}
		if (arg === "--list") {
			actions.push({ kind: "list" });
			continue;
		}
		if (arg === "--state") {
			actions.push({ kind: "state" });
			continue;
		}
		if (arg === "--inject") {
			actions.push({ kind: "inject" });
			continue;
		}
		if (arg === "--anchor") {
			const label = argv[index + 1];
			if (!label || label.startsWith("--")) {
				errors.push("Missing label after --anchor.");
				continue;
			}
			actions.push({ kind: "anchor", label });
			index += 1;
			continue;
		}
		if (arg === "--run-fake") {
			const prompt = argv[index + 1];
			if (!prompt || prompt.startsWith("--")) {
				errors.push("Missing prompt after --run-fake.");
				continue;
			}
			actions.push({ kind: "run-fake", prompt });
			index += 1;
			continue;
		}
		if (arg.startsWith("--")) {
			errors.push(`Unknown flag: ${arg}`);
			continue;
		}
		errors.push(`Unexpected argument: ${arg}`);
	}

	if (help) {
		return { help: true, actions: [], errors: [], demoRequested: false };
	}

	if (actions.some((action) => action.kind === "demo") && actions.length > 1) {
		errors.push("--demo cannot be combined with other actions.");
	}

	if (actions.some((action) => action.kind === "demo")) {
		return {
			help: false,
			actions: [
				{ kind: "state" },
				{ kind: "inject" },
				{ kind: "anchor", label: "rufuschat-shell" },
				{ kind: "run-fake", prompt: "inspect fake bridge" },
				{ kind: "supervise" },
			],
			errors,
			demoRequested,
		};
	}

	if (actions.length === 0) {
		actions.push({ kind: "status" }, { kind: "supervise" });
	}

	return { help: false, actions, errors, demoRequested };
}

function commandForAction(action) {
	switch (action.kind) {
		case "status":
			return "/rck status";
		case "supervise":
			return "/rck supervise";
		case "list":
			return "/rck list";
		case "state":
			return "/state";
		case "inject":
			return "/rck inject";
		case "anchor":
			return `/rck anchor ${action.label}`;
		case "run-fake":
			return action.prompt.startsWith("/hermes ") ? action.prompt : `/hermes ${action.prompt}`;
		default:
			return "";
	}
}

function healthLabel({ needsAttention, hermesStatus, statusAvailable, supervisionAvailable }) {
	if (!statusAvailable || !supervisionAvailable) {
		return "Unknown";
	}
	if (needsAttention) {
		return "Attention";
	}
	if (hermesStatus === "running") {
		return "Running";
	}
	return "OK";
}

function pickTraceId(...values) {
	for (const value of values) {
		if (value) {
			return value;
		}
	}
	return null;
}

function renderSection(title, lines) {
	return [`## ${title}`, ...lines.map((line) => `- ${line}`)].join("\n");
}

function latestSafeSummary(snapshot) {
	return (
		snapshot.latestState?.safeSummary ??
		snapshot.latestContextPack?.safeSummary ??
		snapshot.latestAnchor?.safeSummary ??
		snapshot.latestHermesRun?.safeSummary ??
		snapshot.supervisionDto.reason ??
		snapshot.inventorySummary ??
		null
	);
}

function safeSummaryForAction(action, snapshot) {
	switch (action.kind) {
		case "status":
			return latestSafeSummary(snapshot);
		case "list":
			return snapshot.inventorySummary;
		case "supervise":
			return snapshot.supervisionDto.reason;
		case "state":
			return snapshot.latestState?.safeSummary ?? null;
		case "inject":
			return snapshot.latestContextPack?.safeSummary ?? null;
		case "anchor":
			return snapshot.latestAnchor?.safeSummary ?? null;
		case "run-fake":
			return snapshot.latestHermesRun?.safeSummary ?? null;
		default:
			return null;
	}
}

function generatedIdForAction(action, snapshot) {
	switch (action.kind) {
		case "state":
			return snapshot.latestState?.stateId ?? null;
		case "inject":
			return snapshot.latestContextPack?.contextPackId ?? null;
		case "anchor":
			return snapshot.latestAnchor?.anchorId ?? null;
		case "run-fake":
			return snapshot.latestHermesRun?.runId ?? snapshot.latestHermesRun?.eventId ?? null;
		case "supervise":
			return snapshot.supervisionDto.latestRunId ?? snapshot.supervisionDto.latestEventId ?? null;
		default:
			return null;
	}
}

function actionNeedsAttention(action, snapshot) {
	if (action.kind === "supervise" || action.kind === "run-fake") {
		return snapshot.supervisionDto.needsAttention;
	}
	return snapshot.supervisionDto.needsAttention;
}

function actionRecommendedAction(snapshot) {
	return snapshot.supervisionDto.recommendedAction;
}

function renderActionBlock(action, snapshot) {
	const command = commandForAction(action);
	const lines = [
		`action executed: ${command}`,
		`traceId: ${snapshot.traceId ?? "unknown"}`,
	];
	const generatedId = generatedIdForAction(action, snapshot);
	lines.push(`id: ${generatedId ?? "n/a"}`);
	const safeSummary = safeSummaryForAction(action, snapshot);
	if (safeSummary) {
		lines.push(`safeSummary: ${safeSummary}`);
	}
	if (typeof actionNeedsAttention(action, snapshot) === "boolean") {
		lines.push(`needsAttention: ${actionNeedsAttention(action, snapshot) ? "yes" : "no"}`);
	}
	if (actionRecommendedAction(snapshot)) {
		lines.push(`recommendedAction: ${actionRecommendedAction(snapshot)}`);
	}
	return renderSection(`Action: ${command}`, lines);
}

async function main() {
	const parsed = parseArgs(process.argv.slice(2));
	if (parsed.help) {
		process.stdout.write(`${buildUsage()}\n`);
		return 0;
	}
	if (parsed.errors.length > 0) {
		process.stderr.write(`${parsed.errors.join("\n")}\n\n${buildUsage()}\n`);
		return 1;
	}
	if (!existsSync(piTestPath)) {
		throw new Error(`Missing runner: ${piTestPath}`);
	}

	await import("tsx");
	const {
		normalizeHermesRunResultDto,
		normalizeRckInventoryDto,
		normalizeRckStatusDto,
		normalizeRckSupervisionDto,
	} = await import("../.pi/extensions/rck-bridge/rck-ui-dtos.ts");
	const { evaluateRckSupervision } = await import("../.pi/extensions/rck-bridge/rck-supervision.ts");

	const child = spawn(piTestPath, rpcArgs, {
		cwd: repoRoot,
		env: process.env,
		stdio: ["pipe", "pipe", "pipe"],
		detached: true,
	});

	const responses = new Map();
	let childExit = null;

	const stopChild = async () => {
		try {
			child.stdin.end();
		} catch {
			// ignore
		}
		try {
			if (child.pid) {
				try {
					process.kill(-child.pid, "SIGTERM");
				} catch {
					child.kill("SIGTERM");
				}
			}
		} catch {
			// ignore
		}
		await sleep(250);
		try {
			if (child.pid) {
				try {
					process.kill(-child.pid, "SIGKILL");
				} catch {
					child.kill("SIGKILL");
				}
			}
		} catch {
			// ignore
		}
	};

	child.stdout.setEncoding("utf8");
	const stdoutSplitter = createLineSplitter((line) => {
		if (!line.trim()) {
			return;
		}
		const parsedLine = parseJsonMaybe(line);
		if (parsedLine?.type === "response" && parsedLine.id) {
			responses.set(parsedLine.id, parsedLine);
		}
	});
	child.stdout.on("data", (chunk) => stdoutSplitter.push(chunk));
	child.stdout.once("end", () => stdoutSplitter.flush());
	child.stderr.resume();
	child.once("close", (code, signal) => {
		childExit = { code, signal };
	});

	const deadline = Date.now() + timeoutMs;
	const timeout = setTimeout(() => {
		void stopChild();
	}, timeoutMs);

	const sendJson = async (payload) => {
		const line = `${JSON.stringify(payload)}\n`;
		if (!child.stdin.write(line)) {
			await new Promise((resolve) => child.stdin.once("drain", resolve));
		}
	};

	const waitFor = async (predicate, label) => {
		while (Date.now() < deadline) {
			const value = predicate();
			if (value) {
				return value;
			}
			if (childExit && childExit.code !== 0) {
				throw new Error(`RPC child exited early while waiting for ${label}: ${JSON.stringify(childExit)}`);
			}
			await sleep(50);
		}
		throw new Error(`Timed out waiting for ${label}`);
	};

	const waitForResponse = async (id, label) => {
		return await waitFor(() => responses.get(id), label);
	};

	const runPrompt = async (id, message, label) => {
		await sendJson({ id, type: "prompt", message });
		const response = await waitForResponse(id, `${label} response`);
		if (!response.success) {
			throw new Error(`${label} failed: ${JSON.stringify({ id: response.id, success: response.success, error: response.error ?? null })}`);
		}
	};

	const waitForFile = async (relativePath, label) => {
		await waitFor(() => existsSync(join(rckRoot, relativePath)), label);
	};

	const waitForLatestHermesEvent = async (previousEventId) => {
		await waitFor(() => {
			const events = loadJsonRecords(".pi/rck/events").map((record) => record.json);
			const event = latestEvent(events, "HermesRunRecorded");
			if (!event) {
				return null;
			}
			if (event.eventId === previousEventId) {
				return null;
			}
			return event;
		}, "latest Hermes run");
	};

	const captureSnapshot = () => {
		const currentTrace = readRepoJsonMaybe(".pi/rck/indexes/current-trace.json") ?? { traceId: null };
		const latestStateIndex = readRepoJsonMaybe(".pi/rck/indexes/latest-state.json");
		const latestContextPackIndex = readRepoJsonMaybe(".pi/rck/indexes/latest-context-pack.json");
		const latestAnchorIndex = readRepoJsonMaybe(".pi/rck/indexes/latest-anchor.json");
		const latestState = latestStateIndex?.currentStatePath ? readRepoJsonMaybe(latestStateIndex.currentStatePath) : null;
		const latestContextPack = latestContextPackIndex?.currentContextPackPath ? readRepoJsonMaybe(latestContextPackIndex.currentContextPackPath) : null;
		const latestAnchor = latestAnchorIndex?.currentAnchorPath ? readRepoJsonMaybe(latestAnchorIndex.currentAnchorPath) : null;
		const events = loadJsonRecords(".pi/rck/events").map((record) => record.json);
		const latestHermesRecordedEvent = latestEvent(events, "HermesRunRecorded");
		const latestHermesRun = latestHermesRecordedEvent ? normalizeHermesRunResultDto(latestHermesRecordedEvent) : null;
		const latestHermesForSupervision = latestHermesRecordedEvent
			? {
					eventId: latestHermesRecordedEvent.eventId,
					runId: latestHermesRecordedEvent.payload?.runId ?? latestHermesRecordedEvent.eventId,
					traceId: latestHermesRecordedEvent.traceId,
					mode: latestHermesRecordedEvent.mode ?? latestHermesRecordedEvent.payload?.mode,
					status: latestHermesRecordedEvent.status ?? latestHermesRecordedEvent.payload?.status,
					errorKind: latestHermesRecordedEvent.errorKind ?? latestHermesRecordedEvent.payload?.errorKind,
					timedOut: latestHermesRecordedEvent.timedOut ?? latestHermesRecordedEvent.payload?.timedOut,
					durationMs: latestHermesRecordedEvent.durationMs ?? latestHermesRecordedEvent.payload?.durationMs,
					stdoutByteLength: latestHermesRecordedEvent.stdoutByteLength ?? latestHermesRecordedEvent.payload?.stdoutByteLength,
					stderrByteLength: latestHermesRecordedEvent.stderrByteLength ?? latestHermesRecordedEvent.payload?.stderrByteLength,
					stdoutTruncated: latestHermesRecordedEvent.stdoutTruncated ?? latestHermesRecordedEvent.payload?.stdoutTruncated,
					stderrTruncated: latestHermesRecordedEvent.stderrTruncated ?? latestHermesRecordedEvent.payload?.stderrTruncated,
					blockedReason: latestHermesRecordedEvent.blockedReason ?? latestHermesRecordedEvent.payload?.blockedReason,
					safeSummary: latestHermesRecordedEvent.safeSummary ?? latestHermesRecordedEvent.payload?.safeSummary,
				}
			: undefined;
		const supervisionEvaluation = evaluateRckSupervision({
			currentTrace: currentTrace.traceId ? { traceId: currentTrace.traceId } : undefined,
			latestEventId: latestHermesRecordedEvent?.eventId,
			latestHermes: latestHermesForSupervision,
		});
		const supervisionDto = normalizeRckSupervisionDto({
			evaluation: supervisionEvaluation,
			generatedAt: new Date().toISOString(),
		});
		const statusDto = normalizeRckStatusDto({
			currentTrace,
			latestStateIndex,
			latestState,
			latestContextPackIndex,
			latestContextPack,
			latestAnchorIndex,
			latestAnchor,
			latestHermesRun: latestHermesRecordedEvent ?? undefined,
			generatedAt: new Date().toISOString(),
		});
		const inventoryDto = normalizeRckInventoryDto({
			traceId: currentTrace.traceId,
			counts: {
				states: countJsonFiles("states"),
				contextPacks: countJsonFiles("context-packs"),
				anchors: countJsonFiles("anchors"),
				events: events.length,
				hermesRuns: events.filter((event) => event?.eventType === "HermesRunRecorded").length,
			},
			latestEvents: events,
			latestHermesRun: latestHermesRecordedEvent,
			generatedAt: new Date().toISOString(),
		});
		const health = healthLabel({
			needsAttention: supervisionDto.needsAttention,
			hermesStatus: latestHermesRun?.status ?? null,
			statusAvailable: Boolean(statusDto.latestState || statusDto.latestContextPack || statusDto.latestAnchor),
			supervisionAvailable: true,
		});
		const traceId = pickTraceId(statusDto.traceId, supervisionDto.traceId, inventoryDto.traceId);
		const inventorySummary = `states=${inventoryDto.counts.states} contextPacks=${inventoryDto.counts.contextPacks} anchors=${inventoryDto.counts.anchors} events=${inventoryDto.counts.events} hermesRuns=${inventoryDto.counts.hermesRuns ?? 0}`;
		return {
			currentTrace,
			latestState,
			latestContextPack,
			latestAnchor,
			latestHermesRun,
			supervisionDto,
			statusDto,
			inventoryDto,
			health,
			traceId,
			inventorySummary,
		};
	};

	const executeAction = async (action, promptId) => {
		const command = commandForAction(action);
		const beforeHermesEvents = loadJsonRecords(".pi/rck/events").map((record) => record.json);
		const previousHermesEvent = latestEvent(beforeHermesEvents, "HermesRunRecorded")?.eventId ?? null;

		if (action.kind === "state") {
			await runPrompt(promptId, command, action.kind);
			await waitForFile("indexes/latest-state.json", "latest-state index");
		}
		if (action.kind === "inject") {
			await runPrompt(promptId, command, action.kind);
			await waitForFile("indexes/latest-context-pack.json", "latest-context-pack index");
		}
		if (action.kind === "anchor") {
			await runPrompt(promptId, command, action.kind);
			await waitForFile("indexes/latest-anchor.json", "latest-anchor index");
		}
		if (action.kind === "run-fake") {
			await runPrompt(promptId, command, action.kind);
			await waitForLatestHermesEvent(previousHermesEvent);
		}
		if (action.kind === "status" || action.kind === "supervise" || action.kind === "list") {
			await runPrompt(promptId, command, action.kind);
		}
		return captureSnapshot();
	};

	try {
		await sendJson({ id: "1", type: "get_commands" });
		const commandsResponse = await waitForResponse("1", "get_commands response");
		if (!commandsResponse.success) {
			throw new Error(`get_commands failed: ${JSON.stringify({ id: commandsResponse.id, success: commandsResponse.success, error: commandsResponse.error ?? null })}`);
		}

		const commandNames = new Set((commandsResponse.data?.commands ?? []).map((command) => command?.name));
		for (const name of ["state", "rck", "hermes"]) {
			if (!commandNames.has(name)) {
				throw new Error(`Missing command: ${name}`);
			}
		}

		if (parsed.demoRequested) {
			cleanupArtifacts();
		}

		const actionSnapshots = [];
		for (let index = 0; index < parsed.actions.length; index += 1) {
			const action = parsed.actions[index];
			const snapshot = await executeAction(action, String(index + 2));
			actionSnapshots.push({ action, snapshot });
		}

		const finalSnapshot = actionSnapshots[actionSnapshots.length - 1]?.snapshot ?? captureSnapshot();
		const modeLabel = parsed.demoRequested
			? "demo"
			: parsed.actions.length === 2 && parsed.actions[0]?.kind === "status" && parsed.actions[1]?.kind === "supervise"
				? "read-only"
				: parsed.actions.map((action) => action.kind).join(", ") || "read-only";
		const renderLines = [
			"## RufusChat Minimal Shell",
			`- mode: ${modeLabel}`,
			`- traceId: ${finalSnapshot.traceId ?? "unknown"}`,
			`- health: ${finalSnapshot.health}`,
			`- needsAttention: ${finalSnapshot.supervisionDto.needsAttention ? "yes" : "no"}`,
			`- generatedAt: ${finalSnapshot.statusDto.generatedAt}`,
		];

		for (const { action, snapshot } of actionSnapshots) {
			renderLines.push("", renderActionBlock(action, snapshot));
		}

		renderLines.push(
			"",
			renderSection("Session", [
				`current trace: ${finalSnapshot.currentTrace.traceId ?? "unknown"}`,
				`repo state: ${finalSnapshot.inventoryDto.counts.states} states / ${finalSnapshot.inventoryDto.counts.contextPacks} context packs / ${finalSnapshot.inventoryDto.counts.anchors} anchors / ${finalSnapshot.inventoryDto.counts.events} events`,
			]),
			renderSection("RCK Operational Panel", [
				`latest state: ${finalSnapshot.statusDto.latestState ? `${finalSnapshot.statusDto.latestState.stateId} @ ${finalSnapshot.statusDto.latestState.updatedAt}` : "missing"}`,
				`latest context pack: ${finalSnapshot.statusDto.latestContextPack ? `${finalSnapshot.statusDto.latestContextPack.contextPackId} @ ${finalSnapshot.statusDto.latestContextPack.updatedAt}` : "missing"}`,
				`latest anchor: ${finalSnapshot.statusDto.latestAnchor ? `${finalSnapshot.statusDto.latestAnchor.anchorId} @ ${finalSnapshot.statusDto.latestAnchor.updatedAt}` : "missing"}`,
				`latest Hermes run: ${finalSnapshot.latestHermesRun ? `${finalSnapshot.latestHermesRun.status} (${finalSnapshot.latestHermesRun.runId})` : "missing"}`,
			]),
			renderSection("Conversation Area", [
				`state: ${finalSnapshot.statusDto.latestState?.safeSummary ?? "No state summary available"}`,
				`context pack: ${finalSnapshot.statusDto.latestContextPack?.safeSummary ?? "No context pack summary available"}`,
				`anchor: ${finalSnapshot.statusDto.latestAnchor?.safeSummary ?? "No anchor summary available"}`,
				`Hermes: ${finalSnapshot.latestHermesRun?.safeSummary ?? "No Hermes summary available"}`,
			]),
			renderSection("Actions Bar", [
				"/rck status",
				"/rck supervise",
				"/rck list",
				"/state",
				"/rck inject",
				"/rck anchor <label>",
				"/hermes <prompt>",
				"--demo",
			]),
			renderSection("Supervision / Attention", [
				`level: ${finalSnapshot.supervisionDto.level}`,
				`needsAttention: ${finalSnapshot.supervisionDto.needsAttention ? "yes" : "no"}`,
				`reason: ${finalSnapshot.supervisionDto.reason}`,
				`recommendedAction: ${finalSnapshot.supervisionDto.recommendedAction}`,
			]),
			renderSection("Inventory", [
				`states=${finalSnapshot.inventoryDto.counts.states}`,
				`contextPacks=${finalSnapshot.inventoryDto.counts.contextPacks}`,
				`anchors=${finalSnapshot.inventoryDto.counts.anchors}`,
				`events=${finalSnapshot.inventoryDto.counts.events}`,
				`hermesRuns=${finalSnapshot.inventoryDto.counts.hermesRuns ?? 0}`,
			]),
		);

		process.stdout.write(`${renderLines.join("\n")}\n`);
		return 0;
	} catch (error) {
		const message = error instanceof Error ? error.message : String(error);
		process.stderr.write(`RufusChat shell failed: ${message}\n`);
		return 1;
	} finally {
		clearTimeout(timeout);
		await stopChild();
		if (parsed.demoRequested) {
			cleanupArtifacts();
		}
	}
}

main().then((code) => {
	process.exitCode = code;
});
