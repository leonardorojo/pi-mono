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

function parseArgs(argv) {
	const flags = new Set(argv.filter((arg) => arg.startsWith("--")));
	return {
		demo: flags.has("--demo"),
		list: flags.has("--list"),
	};
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

async function main() {
	const flags = parseArgs(process.argv.slice(2));
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
		const parsed = parseJsonMaybe(line);
		if (parsed?.type === "response" && parsed.id) {
			responses.set(parsed.id, parsed);
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

		if (flags.demo) {
			cleanupArtifacts();
			await runPrompt("2", "/state", "state");
			await waitForFile("indexes/latest-state.json", "latest-state index");
			await runPrompt("3", "/rck inject", "inject");
			await waitForFile("indexes/latest-context-pack.json", "latest-context-pack index");
			await runPrompt("4", "/rck anchor rufuschat-shell", "anchor");
			await waitForFile("indexes/latest-anchor.json", "latest-anchor index");
			await runPrompt("5", "/hermes inspect fake bridge", "hermes fake");
			await runPrompt("6", "/rck supervise", "supervise");
		} else {
			await runPrompt("2", "/rck status", "status");
			let nextPromptId = 3;
			if (flags.list) {
				await runPrompt(String(nextPromptId), "/rck list", "list");
				nextPromptId += 1;
			}
			await runPrompt(String(nextPromptId), "/rck supervise", "supervise");
		}

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
		const actions = flags.demo
			? ["/state", "/rck inject", "/rck anchor rufuschat-shell", "/hermes inspect fake bridge", "/rck supervise"]
			: ["/rck status", ...(flags.list ? ["/rck list"] : []), "/rck supervise", "--demo"];
		const renderLines = [
			"## RufusChat Minimal Shell",
			`- mode: ${flags.demo ? "demo" : "read-only"}`,
			`- traceId: ${traceId ?? "unknown"}`,
			`- health: ${health}`,
			`- needsAttention: ${supervisionDto.needsAttention ? "yes" : "no"}`,
			`- generatedAt: ${statusDto.generatedAt}`,
			"",
			renderSection("Session", [
				`current trace: ${currentTrace.traceId ?? "unknown"}`,
				`repo state: ${inventoryDto.counts.states} states / ${inventoryDto.counts.contextPacks} context packs / ${inventoryDto.counts.anchors} anchors / ${inventoryDto.counts.events} events`,
			]),
			renderSection("RCK Operational Panel", [
				`latest state: ${statusDto.latestState ? `${statusDto.latestState.stateId} @ ${statusDto.latestState.updatedAt}` : "missing"}`,
				`latest context pack: ${statusDto.latestContextPack ? `${statusDto.latestContextPack.contextPackId} @ ${statusDto.latestContextPack.updatedAt}` : "missing"}`,
				`latest anchor: ${statusDto.latestAnchor ? `${statusDto.latestAnchor.anchorId} @ ${statusDto.latestAnchor.updatedAt}` : "missing"}`,
				`latest Hermes run: ${latestHermesRun ? `${latestHermesRun.status} (${latestHermesRun.runId})` : "missing"}`,
			]),
			renderSection("Conversation Area", [
				`state: ${statusDto.latestState?.safeSummary ?? "No state summary available"}`,
				`context pack: ${statusDto.latestContextPack?.safeSummary ?? "No context pack summary available"}`,
				`anchor: ${statusDto.latestAnchor?.safeSummary ?? "No anchor summary available"}`,
				`Hermes: ${latestHermesRun?.safeSummary ?? "No Hermes summary available"}`,
			]),
			renderSection("Actions Bar", actions),
			renderSection("Supervision / Attention", [
				`level: ${supervisionDto.level}`,
				`needsAttention: ${supervisionDto.needsAttention ? "yes" : "no"}`,
				`reason: ${supervisionDto.reason}`,
				`recommendedAction: ${supervisionDto.recommendedAction}`,
			]),
			renderSection("Inventory", [
				`states=${inventoryDto.counts.states}`,
				`contextPacks=${inventoryDto.counts.contextPacks}`,
				`anchors=${inventoryDto.counts.anchors}`,
				`events=${inventoryDto.counts.events}`,
				`hermesRuns=${inventoryDto.counts.hermesRuns ?? 0}`,
			]),
		];

		process.stdout.write(`${renderLines.join("\n")}\n`);
		return 0;
	} catch (error) {
		const message = error instanceof Error ? error.message : String(error);
		process.stderr.write(`RufusChat shell failed: ${message}\n`);
		return 1;
	} finally {
		clearTimeout(timeout);
		await stopChild();
		if (flags.demo) {
			cleanupArtifacts();
		}
	}
}

main().then((code) => {
	process.exitCode = code;
});
