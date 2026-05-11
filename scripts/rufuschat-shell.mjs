#!/usr/bin/env node
import { appendFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { spawn } from "node:child_process";
import { randomUUID } from "node:crypto";
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
	if (!existsSync(rckRoot)) {
		return;
	}
	for (const entry of readdirSync(rckRoot, { withFileTypes: true })) {
		if (entry.name === "rufuschat-sessions") {
			continue;
		}
		rmSync(join(rckRoot, entry.name), { recursive: true, force: true });
	}
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

function sanitizeSessionName(sessionName) {
	const fallback = "default";
	if (typeof sessionName !== "string") {
		return fallback;
	}
	const cleaned = sessionName.trim().toLowerCase().replace(/[^a-z0-9._-]+/g, "-").replace(/-+/g, "-").replace(/^[-_.]+|[-_.]+$/g, "");
	return cleaned || fallback;
}

function makeSessionTimestampStamp(date = new Date()) {
	return date.toISOString().replace(/[:]/g, "-").replace(/\./g, "-");
}

function createSessionTranscriptContext(sessionName) {
	const sessionDir = join(rckRoot, "rufuschat-sessions");
	mkdirSync(sessionDir, { recursive: true });
	const timestampStamp = makeSessionTimestampStamp();
	const safeSessionName = sanitizeSessionName(sessionName);
	const sessionId = `${timestampStamp}_${safeSessionName}_${randomUUID().slice(0, 8)}`;
	const fileName = `${timestampStamp}_${safeSessionName}.jsonl`;
	return {
		sessionId,
		sessionName: safeSessionName,
		path: join(sessionDir, fileName),
	};
}

function safeEvidenceRef(kind, refId, path) {
	if (!refId || !path) {
		return null;
	}
	return {
		kind,
		refId,
		path,
		isRaw: false,
		displayPolicy: "reference-only",
	};
}

function collectSessionEvidenceRefs(snapshot) {
	const refs = [];
	const latestState = snapshot.statusDto?.latestState;
	if (latestState) {
		const ref = safeEvidenceRef("state", latestState.stateId, latestState.statePath);
		if (ref) refs.push(ref);
	}
	const latestContextPack = snapshot.statusDto?.latestContextPack;
	if (latestContextPack) {
		const ref = safeEvidenceRef("context-pack", latestContextPack.contextPackId, latestContextPack.contextPackPath);
		if (ref) refs.push(ref);
	}
	const latestAnchor = snapshot.statusDto?.latestAnchor;
	if (latestAnchor) {
		const ref = safeEvidenceRef("anchor", latestAnchor.anchorId, latestAnchor.anchorPath);
		if (ref) refs.push(ref);
	}
	if (Array.isArray(snapshot.latestHermesRun?.evidenceRefs)) {
		for (const ref of snapshot.latestHermesRun.evidenceRefs) {
			if (ref?.refId && ref?.path) {
				refs.push({
					kind: ref.kind,
					refId: ref.refId,
					path: ref.path,
					isRaw: true,
					displayPolicy: "reference-only",
				});
			}
		}
	}
	return refs;
}

function buildSessionEntry(action, snapshot, sessionContext) {
	const result = {
		traceId: snapshot.traceId ?? null,
		needsAttention: Boolean(actionNeedsAttention(action, snapshot)),
		suggestedNextActions: suggestNextActions(action, snapshot),
	};
	const safeSummary = safeSummaryForAction(action, snapshot);
	if (safeSummary) {
		result.safeSummary = safeSummary;
	}
	const recommendedAction = actionRecommendedAction(snapshot);
	if (recommendedAction) {
		result.recommendedAction = recommendedAction;
	}
	return {
		type: "rufuschat.shell.action",
		timestamp: new Date().toISOString(),
		sessionId: sessionContext.sessionId,
		action: {
			name: action.name,
			kind: actionKindLabel(action),
			flag: action.flag,
		},
		args: buildSessionArgs(action, sessionContext),
		result,
		evidenceRefs: collectSessionEvidenceRefs(snapshot),
	};
}

function buildSessionArgs(action, sessionContext) {
	const args = {};
	if (action.name === "anchor" && action.value) {
		args.label = action.value;
	}
	if (action.name === "run-fake") {
		args.promptSupplied = true;
	}
	if (sessionContext.sessionName !== "default") {
		args.sessionName = sessionContext.sessionName;
	}
	return args;
}

function writeSessionTranscriptLine(sessionContext, action, snapshot) {
	if (!sessionContext) {
		return;
	}
	const entry = buildSessionEntry(action, snapshot, sessionContext);
	appendFileSync(sessionContext.path, `${JSON.stringify(entry)}\n`, "utf8");
}

function hasLatestState(snapshot) {
	return Boolean(snapshot.statusDto?.latestState);
}

function defaultReadOnlySuggestions(snapshot) {
	const suggestions = ["--help", "--supervise"];
	if (hasLatestState(snapshot)) {
		suggestions.push("--state");
	}
	return suggestions;
}

function statusSuggestions(snapshot) {
	const suggestions = ["--supervise"];
	if (hasLatestState(snapshot)) {
		suggestions.push("--state");
	}
	return suggestions;
}

function superviseSuggestions(snapshot) {
	if (snapshot.supervisionDto.needsAttention) {
		const suggestions = ["--status"];
		if (hasLatestState(snapshot)) {
			suggestions.push("--state");
		}
		return suggestions;
	}
	const suggestions = ["--status"];
	if (hasLatestState(snapshot)) {
		suggestions.push("--state");
	}
	return suggestions;
}

function stateSuggestions() {
	return ["--inject", "--anchor <label>"];
}

function injectSuggestions() {
	return ["--anchor <label>"];
}

function anchorSuggestions() {
	return ["--run-fake <prompt>", "--supervise"];
}

function runFakeSuggestions(snapshot) {
	const suggestions = ["--supervise", "--state"];
	if (snapshot.supervisionDto.needsAttention) {
		suggestions.unshift("--status");
	}
	return suggestions;
}

function demoSuggestions() {
	return ["--help", "--status"];
}

function buildDemoActions() {
	return [
		{ name: "state", kind: "mutating", flag: "--state", definition: ACTION_DEFINITION_BY_NAME.get("state") },
		{ name: "inject", kind: "mutating", flag: "--inject", definition: ACTION_DEFINITION_BY_NAME.get("inject") },
		{ name: "anchor", kind: "mutating", flag: "--anchor", value: "rufuschat-shell", definition: ACTION_DEFINITION_BY_NAME.get("anchor") },
		{ name: "run-fake", kind: "mutating", flag: "--run-fake", value: "inspect fake bridge", definition: ACTION_DEFINITION_BY_NAME.get("run-fake") },
		{ name: "supervise", kind: "read-only", flag: "--supervise", definition: ACTION_DEFINITION_BY_NAME.get("supervise") },
	];
}

function interactiveCommandLabel(definition) {
	return definition.requiresValue ? `${definition.name} ${definition.valueLabel}` : definition.name;
}

function buildInteractiveHelpSections() {
	const readOnly = ACTION_DEFINITIONS.filter((definition) => definition.kind === "read-only");
	const mutating = ACTION_DEFINITIONS.filter((definition) => definition.kind === "mutating");
	return [
		"Interactive commands:",
		"  help",
		"  status",
		"  supervise",
		"  list",
		"  state",
		"  inject",
		"  anchor <label>",
		"  run-fake <prompt>",
		"  demo",
		"  exit",
		"  quit",
		"",
		"Read-only commands:",
		...readOnly.map((definition) => `  ${interactiveCommandLabel(definition).padEnd(24)} ${definition.description}`),
		"",
		"Mutating commands:",
		...mutating.map((definition) => `  ${interactiveCommandLabel(definition).padEnd(24)} ${definition.description}`),
		"",
		"Safety:",
		"  - Mutating commands require explicit confirmation.",
		"  - Default confirmation is No.",
	].join("\n");
}

function parseInteractiveCommand(input) {
	const trimmed = input.trim();
	if (!trimmed) {
		return { type: "empty" };
	}
	const [rawCommand, ...rest] = trimmed.split(/\s+/);
	const command = rawCommand.toLowerCase();
	const value = rest.join(" ").trim();
	if (command === "exit" || command === "quit") {
		return { type: "exit" };
	}
	if (command === "help") {
		return { type: "help" };
	}
	const definition = ACTION_DEFINITION_BY_NAME.get(command);
	if (!definition) {
		return { type: "unknown" };
	}
	if (definition.requiresValue && !value) {
		return { type: "missing-value", command };
	}
	if (command === "demo") {
		return { type: "demo" };
	}
	return { type: "action", action: { name: definition.name, kind: definition.kind, flag: definition.flag, value: definition.requiresValue ? value : undefined, definition } };
}

function isPositiveConfirmation(input) {
	const normalized = input.trim().toLowerCase();
	return normalized === "y" || normalized === "yes";
}

const ACTION_DEFINITIONS = [
	{
		name: "default",
		flag: "(default)",
		kind: "read-only",
		description: "No flags: run /rck status and /rck supervise.",
		run: async (ctx) => ctx.runDefaultCabin(),
		suggestedNextActions: defaultReadOnlySuggestions,
	},
	{
		name: "help",
		flag: "--help",
		kind: "read-only",
		description: "Show generated usage and the action catalog.",
		run: async (ctx) => ctx.showHelp(),
		suggestedNextActions: () => [],
	},
	{
		name: "status",
		flag: "--status",
		kind: "read-only",
		description: "Run /rck status.",
		run: async (ctx) => ctx.runCoreAction("status"),
		suggestedNextActions: statusSuggestions,
	},
	{
		name: "supervise",
		flag: "--supervise",
		kind: "read-only",
		description: "Run /rck supervise.",
		run: async (ctx) => ctx.runCoreAction("supervise"),
		suggestedNextActions: superviseSuggestions,
	},
	{
		name: "list",
		flag: "--list",
		kind: "read-only",
		description: "Run /rck list.",
		run: async (ctx) => ctx.runCoreAction("list"),
		suggestedNextActions: () => ["--help", "--status"],
	},
	{
		name: "state",
		flag: "--state",
		kind: "mutating",
		description: "Run /state.",
		run: async (ctx) => ctx.runCoreAction("state"),
		suggestedNextActions: stateSuggestions,
	},
	{
		name: "inject",
		flag: "--inject",
		kind: "mutating",
		description: "Run /rck inject.",
		run: async (ctx) => ctx.runCoreAction("inject"),
		suggestedNextActions: injectSuggestions,
	},
	{
		name: "anchor",
		flag: "--anchor",
		kind: "mutating",
		requiresValue: true,
		valueLabel: "<label>",
		description: "Run /rck anchor <label>.",
		run: async (ctx, action) => ctx.runCoreAction("anchor", action.value),
		suggestedNextActions: anchorSuggestions,
	},
	{
		name: "run-fake",
		flag: "--run-fake",
		kind: "mutating",
		requiresValue: true,
		valueLabel: "<prompt>",
		description: "Run /hermes <prompt>.",
		run: async (ctx, action) => ctx.runCoreAction("run-fake", action.value),
		suggestedNextActions: runFakeSuggestions,
	},
	{
		name: "demo",
		flag: "--demo",
		kind: "mutating",
		description: "Run the explicit demo flow.",
		run: async (ctx) => ctx.runDemo(),
		suggestedNextActions: demoSuggestions,
	},
];

const ACTION_DEFINITION_BY_FLAG = new Map(ACTION_DEFINITIONS.map((definition) => [definition.flag, definition]));
const ACTION_DEFINITION_BY_NAME = new Map(ACTION_DEFINITIONS.map((definition) => [definition.name, definition]));

function formatHelpLine(definition) {
	const flag = definition.requiresValue ? `${definition.flag} ${definition.valueLabel}` : definition.flag;
	return `  ${flag.padEnd(24)} ${definition.description}`;
}

function generatedHelpSections() {
	const readOnly = ACTION_DEFINITIONS.filter((definition) => definition.kind === "read-only");
	const mutating = ACTION_DEFINITIONS.filter((definition) => definition.kind === "mutating");
	return [
		"Usage:",
		"  node scripts/rufuschat-shell.mjs",
		"  node scripts/rufuschat-shell.mjs --help",
		"  node scripts/rufuschat-shell.mjs --chat",
		"  node scripts/rufuschat-shell.mjs --session [name]",
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
		"Read-only actions:",
		...readOnly.map((definition) => formatHelpLine(definition)),
		"",
		"Mutating actions:",
		...mutating.map((definition) => formatHelpLine(definition)),
		"",
		"Session transcript:",
		"  --session [name] writes a safe JSONL transcript under .pi/rck/rufuschat-sessions/.",
		"  The transcript is opt-in and contains references/metadata only.",
		"  Raw evidence is never included.",
		"",
		"Safety:",
		"  - Mutating actions create RCK events/artifacts.",
		"  - Raw evidence is never displayed.",
		"  - No raw stdout/stderr is shown.",
		"  - Evidence is kept to safe refs / metadata.",
		"  - No web UI, no Codex, no RufusLab.RCK.Cli.",
	].join("\n");
}

function suggestNextActions(action, snapshot) {
	const definition = ACTION_DEFINITION_BY_NAME.get(action.name);
	if (!definition?.suggestedNextActions) {
		return [];
	}
	const suggestions = definition.suggestedNextActions(snapshot, action);
	return Array.isArray(suggestions) ? suggestions.filter(Boolean) : [];
}

function actionDisplayName(action) {
	if (action.name === "default") {
		return "default read-only cabin";
	}
	if (action.name === "help") {
		return "help";
	}
	if (action.value && action.name === "anchor") {
		return `/rck anchor ${action.value}`;
	}
	if (action.value && action.name === "run-fake") {
		return action.value.startsWith("/hermes ") ? action.value : `/hermes ${action.value}`;
	}
	const definition = ACTION_DEFINITION_BY_NAME.get(action.name);
	if (!definition) {
		return action.name;
	}
	if (definition.flag === "(default)") {
		return "default read-only cabin";
	}
	return definition.requiresValue && action.value ? `${definition.flag} ${action.value}` : definition.flag;
}

function actionKindLabel(action) {
	return ACTION_DEFINITION_BY_NAME.get(action.name)?.kind ?? "unknown";
}

function buildUsage() {
	return generatedHelpSections();
}

function parseArgs(argv) {
	const actions = [];
	const errors = [];
	let help = false;
	let chat = false;
	let demoRequested = false;
	let sessionEnabled = false;
	let sessionName = null;

	for (let index = 0; index < argv.length; index += 1) {
		const arg = argv[index];
		if (arg === "--help" || arg === "-h") {
			help = true;
			continue;
		}
		if (arg === "--session") {
			sessionEnabled = true;
			const maybeName = argv[index + 1];
			if (maybeName && !maybeName.startsWith("--")) {
				sessionName = maybeName;
				index += 1;
			}
			continue;
		}
		if (arg === "--chat") {
			chat = true;
			continue;
		}
		const definition = ACTION_DEFINITION_BY_FLAG.get(arg);
		if (!definition) {
			if (arg.startsWith("--")) {
				errors.push(`Unknown flag: ${arg}. Use --help.`);
				continue;
			}
			errors.push(`Unexpected argument: ${arg}`);
			continue;
		}
		if (definition.name === "demo") {
			demoRequested = true;
			actions.push({ name: definition.name, kind: definition.kind, flag: definition.flag, definition });
			continue;
		}
		if (definition.requiresValue) {
			const value = argv[index + 1];
			if (!value || value.startsWith("--")) {
				errors.push(definition.name === "anchor" ? "Missing label after --anchor." : "Missing prompt after --run-fake.");
				continue;
			}
			actions.push({ name: definition.name, kind: definition.kind, flag: definition.flag, value, definition });
			index += 1;
			continue;
		}
		actions.push({ name: definition.name, kind: definition.kind, flag: definition.flag, definition });
	}

	if (help) {
		return { help: true, chat: false, actions: [], errors: [], demoRequested: false, sessionEnabled, sessionName };
	}

	if (chat && actions.length > 0) {
		errors.push("--chat cannot be combined with action flags.");
	}

	if (actions.some((action) => action.name === "demo") && actions.length > 1) {
		errors.push("--demo cannot be combined with other actions.");
	}

	if (actions.some((action) => action.name === "demo")) {
			return {
				help: false,
				chat,
				actions: [
					...buildDemoActions(),
				],
				errors,
				demoRequested,
				sessionEnabled,
			sessionName,
		};
	}

	if (actions.length === 0) {
		actions.push(
			{ name: "status", kind: "read-only", flag: "--status", definition: ACTION_DEFINITION_BY_NAME.get("status") },
			{ name: "supervise", kind: "read-only", flag: "--supervise", definition: ACTION_DEFINITION_BY_NAME.get("supervise") },
		);
	}

	return { help: false, chat, actions, errors, demoRequested, sessionEnabled, sessionName };
}

function commandForAction(action) {
	switch (action.name) {
		case "default":
			return "default read-only cabin";
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
			return `/rck anchor ${action.value}`;
		case "run-fake":
			return action.value.startsWith("/hermes ") ? action.value : `/hermes ${action.value}`;
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
	switch (action.name) {
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
	switch (action.name) {
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
	if (action.name === "supervise" || action.name === "run-fake") {
		return snapshot.supervisionDto.needsAttention;
	}
	return snapshot.supervisionDto.needsAttention;
}

function actionRecommendedAction(snapshot) {
	return snapshot.supervisionDto.recommendedAction;
}

function renderActionBlock(action, snapshot) {
	const command = commandForAction(action);
	const suggestedNextActions = suggestNextActions(action, snapshot);
	const lines = [
		`action name: ${actionDisplayName(action)}`,
		`action kind: ${actionKindLabel(action)}`,
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
	if (suggestedNextActions.length > 0) {
		lines.push(`suggestedNextActions: ${suggestedNextActions.join(", ")}`);
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
	if (parsed.chat && !process.stdin.isTTY) {
		process.stderr.write("--chat requires an interactive TTY. Use flags for non-interactive automation.\n");
		return 1;
	}
	const sessionContext = parsed.sessionEnabled ? createSessionTranscriptContext(parsed.sessionName) : null;
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

		if (action.name === "state") {
			await runPrompt(promptId, command, action.name);
			await waitForFile("indexes/latest-state.json", "latest-state index");
		}
		if (action.name === "inject") {
			await runPrompt(promptId, command, action.name);
			await waitForFile("indexes/latest-context-pack.json", "latest-context-pack index");
		}
		if (action.name === "anchor") {
			await runPrompt(promptId, command, action.name);
			await waitForFile("indexes/latest-anchor.json", "latest-anchor index");
		}
		if (action.name === "run-fake") {
			await runPrompt(promptId, command, action.name);
			await waitForLatestHermesEvent(previousHermesEvent);
		}
		if (action.name === "status" || action.name === "supervise" || action.name === "list") {
			await runPrompt(promptId, command, action.name);
		}
		return captureSnapshot();
	};

	const runInteractiveShell = async () => {
		let promptCounter = 2;
		let closed = false;
		let inputBuffer = "";
		let inputEnded = false;
		const pendingLines = [];
		const pendingResolvers = [];
		const settleLine = (line) => {
			const resolver = pendingResolvers.shift();
			if (resolver) {
				resolver(line);
				return;
			}
			pendingLines.push(line);
		};
		const onData = (chunk) => {
			inputBuffer += chunk;
			for (;;) {
				const index = inputBuffer.indexOf("\n");
				if (index === -1) {
					break;
				}
				const line = inputBuffer.slice(0, index).replace(/\r$/, "");
				inputBuffer = inputBuffer.slice(index + 1);
				settleLine(line);
			}
		};
		const onEnd = () => {
			if (inputBuffer.length > 0) {
				settleLine(inputBuffer.replace(/\r$/, ""));
				inputBuffer = "";
			}
			inputEnded = true;
			if (pendingLines.length === 0) {
				while (pendingResolvers.length > 0) {
					pendingResolvers.shift()?.(null);
				}
			}
		};
		const promptLine = async (prompt) => {
			process.stdout.write(prompt);
			if (pendingLines.length > 0) {
				return pendingLines.shift();
			}
			if (closed || inputEnded) {
				closed = true;
				return null;
			}
			return await new Promise((resolve) => {
				pendingResolvers.push(resolve);
			});
		};
		const askConfirmation = async (name) => {
			const answer = await promptLine(`Mutating action "${name}". Continue? [y/N] `);
			return answer !== null && isPositiveConfirmation(answer);
		};

		process.stdin.setEncoding("utf8");
		process.stdin.on("data", onData);
		process.stdin.on("end", onEnd);
		process.stdin.resume();
		process.on("SIGINT", () => {
			closed = true;
			process.stdin.pause();
		});

		process.stdout.write('RufusChat interactive shell\nType "help" for commands. Type "exit" to quit.\n');

		try {
			while (!closed) {
				const line = await promptLine("rufuschat> ");
				if (line === null) {
					break;
				}

				const parsedCommand = parseInteractiveCommand(line);
				if (parsedCommand.type === "empty") {
					continue;
				}
				if (parsedCommand.type === "help") {
					process.stdout.write(`${buildInteractiveHelpSections()}\n`);
					continue;
				}
				if (parsedCommand.type === "exit") {
					closed = true;
					break;
				}
				if (parsedCommand.type === "unknown") {
					process.stdout.write('Unknown command. Type "help" for commands.\n');
					continue;
				}
				if (parsedCommand.type === "missing-value") {
					if (parsedCommand.command === "anchor") {
						process.stdout.write("Missing value for anchor. Usage: anchor <label>\n");
					} else if (parsedCommand.command === "run-fake") {
						process.stdout.write("Missing value for run-fake. Usage: run-fake <prompt>\n");
					}
					continue;
				}
				if (parsedCommand.type === "demo") {
					const confirmed = await askConfirmation("demo");
					if (!confirmed) {
						process.stdout.write("Cancelled.\n");
						continue;
					}
					cleanupArtifacts();
					for (const action of buildDemoActions()) {
						const snapshot = await executeAction(action, String(promptCounter));
						promptCounter += 1;
						writeSessionTranscriptLine(sessionContext, action, snapshot);
						process.stdout.write(`${renderActionBlock(action, snapshot)}\n`);
					}
					continue;
				}

				const action = parsedCommand.action;
				if (action.kind === "mutating") {
					const confirmed = await askConfirmation(action.name);
					if (!confirmed) {
						process.stdout.write("Cancelled.\n");
						continue;
					}
				}
				const snapshot = await executeAction(action, String(promptCounter));
				promptCounter += 1;
				writeSessionTranscriptLine(sessionContext, action, snapshot);
				process.stdout.write(`${renderActionBlock(action, snapshot)}\n`);
			}

			if (sessionContext) {
				process.stdout.write(`Transcript: ${sessionContext.path}\n`);
			}
			return 0;
		} finally {
			process.stdin.off("data", onData);
			process.stdin.off("end", onEnd);
			process.stdin.pause();
		}
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

		if (parsed.chat) {
			return await runInteractiveShell();
		}

		const actionSnapshots = [];
		for (let index = 0; index < parsed.actions.length; index += 1) {
			const action = parsed.actions[index];
			const snapshot = await executeAction(action, String(index + 2));
			actionSnapshots.push({ action, snapshot });
			writeSessionTranscriptLine(sessionContext, action, snapshot);
		}

		const finalSnapshot = actionSnapshots[actionSnapshots.length - 1]?.snapshot ?? captureSnapshot();
		const modeLabel = parsed.demoRequested
			? "demo"
			: parsed.actions.length === 2 && parsed.actions[0]?.name === "status" && parsed.actions[1]?.name === "supervise"
				? "read-only"
				: parsed.actions.map((action) => action.name).join(", ") || "read-only";
		const renderLines = [
			"## RufusChat Minimal Shell",
			`- mode: ${modeLabel}`,
			`- traceId: ${finalSnapshot.traceId ?? "unknown"}`,
			`- health: ${finalSnapshot.health}`,
			`- needsAttention: ${finalSnapshot.supervisionDto.needsAttention ? "yes" : "no"}`,
			`- generatedAt: ${finalSnapshot.statusDto.generatedAt}`,
		];

		const topLevelSuggestions = parsed.demoRequested
			? demoSuggestions(finalSnapshot)
			: parsed.actions.length === 2 && parsed.actions[0]?.name === "status" && parsed.actions[1]?.name === "supervise"
				? defaultReadOnlySuggestions(finalSnapshot)
				: [];
		if (topLevelSuggestions.length > 0) {
			renderLines.push("", renderSection("Suggested Next Actions", topLevelSuggestions));
		}

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

		if (sessionContext) {
			renderLines.push("", `Transcript: ${sessionContext.path}`);
		}

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
