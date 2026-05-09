#!/usr/bin/env node
import { spawn, spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const repoRoot = resolve(__dirname, "..");
const piTestPath = join(repoRoot, "pi-test.sh");
const extensionPath = ".pi/extensions/rck-bridge/index.ts";
const rpcArgs = ["--offline", "--mode", "rpc", "--no-tools", "--no-extensions", "--extension", extensionPath];
const globalTimeoutMs = Number.parseInt(process.env.RCK_BRIDGE_RPC_COCKPIT_TIMEOUT_MS ?? "40000", 10);

function nowUtc() {
	return new Date().toISOString();
}

function runGit(args) {
	const result = spawnSync("git", args, {
		cwd: repoRoot,
		encoding: "utf8",
		maxBuffer: 1024 * 1024,
	});
	if (result.error) {
		throw result.error;
	}
	if (result.status !== 0) {
		const stderr = String(result.stderr ?? "").trim();
		throw new Error(`git ${args.join(" ")} failed${stderr ? `: ${stderr}` : ""}`);
	}
	return String(result.stdout ?? "").trim();
}

function getRepoInfo() {
	const branch = runGit(["rev-parse", "--abbrev-ref", "HEAD"]);
	const head = runGit(["rev-parse", "--short", "HEAD"]);
	const dirty = runGit(["status", "--short"]).length > 0;
	return {
		path: repoRoot,
		branch,
		head,
		dirty,
	};
}

function sleep(ms) {
	return new Promise((resolve) => setTimeout(resolve, ms));
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

function normalizeLines(text) {
	return String(text ?? "")
		.split(/\r?\n/)
		.map((line) => line.trim())
		.filter(Boolean);
}

function getLatestCustomMessage(messages, customType, prefixes) {
	const list = Array.isArray(prefixes) ? prefixes : [prefixes];
	for (let index = messages.length - 1; index >= 0; index -= 1) {
		const message = messages[index];
		if (message?.role !== "custom" || message.customType !== customType || typeof message.content !== "string") {
			continue;
		}
		if (list.length === 0) {
			return message;
		}
		if (list.some((prefix) => message.content.startsWith(prefix) || message.content === prefix || message.content.includes(prefix))) {
			return message;
		}
	}
	return undefined;
}

function parseKeyValue(line) {
	const match = line.match(/^[-*]\s+([^:]+):\s*(.*)$/);
	if (!match) {
		return null;
	}
	return { key: match[1].trim(), value: match[2].trim() };
}

function parseStatusContent(content) {
	const lines = normalizeLines(content);
	const summaryParts = [];
	const parsed = {
		traceId: null,
		state: null,
		contextPack: null,
		anchor: null,
		latestHermes: null,
	};

	for (const line of lines) {
		const kv = parseKeyValue(line);
		if (!kv) {
			continue;
		}
		if (kv.key === "current trace") {
			const traceMatch = kv.value.match(/traceId=([^,\s]+)/);
			if (traceMatch) {
				parsed.traceId = traceMatch[1];
				summaryParts.push(`traceId=${traceMatch[1]}`);
			}
			continue;
		}
		if (kv.key === "state" || kv.key === "context pack" || kv.key === "anchor") {
			parsed[kv.key.replace(/\s+/g, "") === "contextpack" ? "contextPack" : kv.key.replace(/\s+/g, "")] = kv.value;
			summaryParts.push(`${kv.key}=${kv.value}`);
			continue;
		}
		if (kv.key === "latest Hermes") {
			parsed.latestHermes = kv.value;
			summaryParts.push(`latestHermes=${kv.value}`);
		}
	}

	return {
		traceId: parsed.traceId,
		state: parsed.state,
		contextPack: parsed.contextPack,
		anchor: parsed.anchor,
		latestHermes: parsed.latestHermes,
		summary: summaryParts.length > 0 ? summaryParts.join(" | ") : lines.join(" | "),
	};
}

function parseListContent(content) {
	const lines = normalizeLines(content);
	const counts = {};
	const summaryParts = [];
	let traceId = null;
	let latestHermes = null;
	let latestEvents = [];
	let inLatestEvents = false;
	let inLatestHermesEvents = false;
	let latestHermesRecordedEvents = 0;

	for (const line of lines) {
		const kv = parseKeyValue(line);
		if (!kv) {
			if (line === "- latest events:") {
				inLatestEvents = true;
				inLatestHermesEvents = false;
			}
			if (line === "- latest Hermes recorded events:") {
				inLatestHermesEvents = true;
				inLatestEvents = false;
			}
			if (line === "- storage: .pi/rck") {
				inLatestEvents = false;
				inLatestHermesEvents = false;
			}
			if (inLatestEvents && line.startsWith("-") === false && line.startsWith("RCK ") === false && line.startsWith("  - ")) {
				latestEvents.push(line);
			}
			if (inLatestHermesEvents && line.startsWith("  - ")) {
				latestHermesRecordedEvents += 1;
			}
			continue;
		}

		if (kv.key === "current trace") {
			const traceMatch = kv.value.match(/traceId=([^,\s]+)/);
			if (traceMatch) {
				traceId = traceMatch[1];
				summaryParts.push(`traceId=${traceMatch[1]}`);
			}
			continue;
		}

		if (["states", "context packs", "anchors", "events"].includes(kv.key)) {
			const normalizedKey = kv.key === "context packs" ? "contextPacks" : kv.key;
			counts[normalizedKey] = Number.parseInt(kv.value, 10);
			summaryParts.push(`${normalizedKey}=${kv.value}`);
			continue;
		}

		if (kv.key === "latest state" || kv.key === "latest context pack" || kv.key === "latest anchor") {
			summaryParts.push(`${kv.key.replace(/\s+/g, "") }=${kv.value}`);
			continue;
		}

		if (kv.key === "latest Hermes") {
			latestHermes = kv.value;
			summaryParts.push(`latestHermes=${kv.value}`);
		}
	}

	if (latestHermesRecordedEvents > 0) {
		counts.latestHermesRecordedEvents = latestHermesRecordedEvents;
	}
	if (latestEvents.length > 0) {
		counts.latestEvents = latestEvents.length;
	}

	return {
		traceId,
		counts,
		latestHermes,
		summary: summaryParts.length > 0 ? summaryParts.join(" | ") : lines.join(" | "),
	};
}

function parseSupervisionContent(content) {
	const lines = normalizeLines(content);
	const summaryParts = [];
	const result = {
		level: "unknown",
		reason: null,
		recommendedAction: null,
		needsAttention: false,
		traceId: null,
		latestRunId: null,
		latestEventId: null,
		signals: {},
	};

	for (const line of lines) {
		const kv = parseKeyValue(line);
		if (!kv) {
			continue;
		}
		switch (kv.key) {
			case "level":
				result.level = kv.value;
				summaryParts.push(`level=${kv.value}`);
				break;
			case "needs attention":
				result.needsAttention = kv.value === "yes";
				summaryParts.push(`needsAttention=${kv.value}`);
				break;
			case "reason":
				result.reason = kv.value;
				summaryParts.push(`reason=${kv.value}`);
				break;
			case "recommended action":
				result.recommendedAction = kv.value;
				break;
			case "trace":
				result.traceId = kv.value !== "missing" ? kv.value : null;
				break;
			case "latest run":
				result.latestRunId = kv.value !== "missing" ? kv.value : null;
				break;
			case "latest event":
				result.latestEventId = kv.value !== "missing" ? kv.value : null;
				break;
			case "signals": {
				const signals = {};
				for (const token of kv.value.split(/\s+/).filter(Boolean)) {
					const [key, ...rest] = token.split("=");
					if (!key) {
						continue;
					}
					signals[key] = rest.join("=") || true;
				}
				result.signals = signals;
				break;
			}
			default:
				break;
		}
	}

	return {
		...result,
		summary: summaryParts.length > 0 ? summaryParts.join(" | ") : lines.join(" | "),
	};
}

function parseHermesContent(content) {
	const lines = normalizeLines(content);
	const summaryParts = [];
	let mode = null;
	let status = null;
	let evidenceRefPresent = false;
	let runId = null;
	let latestEventId = null;

	for (const line of lines) {
		const kv = parseKeyValue(line);
		if (!kv) {
			continue;
		}
		if (kv.key === "latest Hermes") {
			const modeMatch = kv.value.match(/mode=([^,\s]+)/);
			const statusMatch = kv.value.match(/status=([^,\s]+)/);
			if (modeMatch) {
				mode = modeMatch[1];
			}
			if (statusMatch) {
				status = statusMatch[1];
			}
			if (/evidence=(stdout|stderr|stdout\/stderr)/.test(kv.value)) {
				evidenceRefPresent = true;
			}
			summaryParts.push(kv.value);
		}
		if (kv.key === "latest Hermes recorded events") {
			summaryParts.push(kv.value);
		}
		if (kv.key === "latest run") {
			runId = kv.value !== "missing" ? kv.value : null;
		}
		if (kv.key === "latest event") {
			latestEventId = kv.value !== "missing" ? kv.value : null;
		}
	}

	if (!mode && !status) {
		return {
			available: false,
			mode: null,
			status: null,
			evidenceRefPresent: false,
			runId: null,
			latestEventId: null,
			summary: lines.join(" | "),
		};
	}

	return {
		available: true,
		mode,
		status,
		evidenceRefPresent,
		runId,
		latestEventId,
		summary: summaryParts.length > 0 ? summaryParts.join(" | ") : lines.join(" | "),
	};
}

function createAction(id, label, command, kind, dangerLevel, requiresConfirmation, enabled, disabledReason) {
	return {
		id,
		label,
		command,
		kind,
		dangerLevel,
		requiresConfirmation,
		enabled,
		...(disabledReason ? { disabledReason } : {}),
	};
}

function buildFallbackSummary(name, reason) {
	return {
		available: false,
		summary: reason,
		source: name,
	};
}

async function main() {
	if (!existsSync(piTestPath)) {
		throw new Error(`Missing runner: ${piTestPath}`);
	}

	const repo = getRepoInfo();
	const child = spawn(piTestPath, rpcArgs, {
		cwd: repoRoot,
		env: process.env,
		stdio: ["pipe", "pipe", "pipe"],
		detached: true,
	});

	const responses = new Map();
	const stderrTail = [];
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

	child.stderr.setEncoding("utf8");
	child.stderr.on("data", (chunk) => {
		for (const line of String(chunk).split(/\r?\n/)) {
			if (line) {
				stderrTail.push(line);
				while (stderrTail.length > 20) {
					stderrTail.shift();
				}
			}
		}
	});
	child.once("close", (code, signal) => {
		childExit = { code, signal };
	});

	const deadline = Date.now() + globalTimeoutMs;
	const timeout = setTimeout(() => {
		void stopChild();
	}, globalTimeoutMs);

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

	const getMessages = async (id) => {
		await sendJson({ id, type: "get_messages" });
		const response = await waitForResponse(id, "get_messages response");
		if (!response.success) {
			throw new Error(`get_messages failed: ${JSON.stringify(response)}`);
		}
		return response.data?.messages ?? [];
	};

	const runPrompt = async (id, message, label) => {
		await sendJson({ id, type: "prompt", message });
		const response = await waitForResponse(id, `${label} response`);
		if (!response.success) {
			throw new Error(`${label} failed: ${JSON.stringify(response)}`);
		}
		return response;
	};

	const result = {
		schemaVersion: "rck.cockpit/v0.1",
		generatedAtUtc: nowUtc(),
		repo: {
			path: repo.path,
			branch: repo.branch,
			dirty: repo.dirty,
		},
		trace: {
			traceId: null,
			healthLevel: "unknown",
			needsAttention: false,
		},
		status: {
			available: false,
			summary: "",
			source: "/rck status",
		},
		supervision: {
			available: false,
			level: "unknown",
			reason: "",
			recommendedAction: "",
			needsAttention: false,
			source: "/rck supervise",
			signals: {},
		},
		inventory: {
			available: false,
			summary: "",
			counts: {},
			source: "/rck list",
		},
		latestHermes: {
			available: false,
			mode: null,
			status: null,
			evidenceRefPresent: false,
			runId: null,
			latestEventId: null,
			summary: "",
		},
		actions: [
			createAction("state", "Capture state", "/state", "action", "low", false, true),
			createAction("rck-inject", "Inject context", "/rck inject", "action", "low", false, true),
			createAction("rck-anchor", "Anchor current state", "/rck anchor", "action", "low", false, true),
			createAction("rck-status", "Refresh status", "/rck status", "read-only", "low", false, true),
			createAction("rck-list", "Refresh inventory", "/rck list", "read-only", "low", false, true),
			createAction("rck-supervise", "Run supervision check", "/rck supervise", "read-only", "low", false, true),
			createAction("hermes-fake", "Inspect fake Hermes bridge", "/hermes inspect fake bridge", "action", "low", false, true),
			createAction(
				"hermes-real-gated",
				"Run Hermes real (gated)",
				"/hermes --mode real <prompt>",
				"action",
				"high",
				true,
				true,
			),
		],
		capabilities: {
			canObserve: true,
			canAct: true,
			canGateRealHermes: true,
			canResolveTrace: true,
			canListArtifacts: true,
		},
		safety: {
			realHermesGated: true,
			rawEvidenceExposed: false,
			readOnly: true,
			warnings: [],
		},
		safeMessages: {
			commands: "",
			status: "",
			list: "",
			supervise: "",
		},
	};

	try {
		await sendJson({ id: "1", type: "get_commands" });
		const commandsResponse = await waitForResponse("1", "get_commands response");
		if (!commandsResponse.success) {
			throw new Error(`get_commands failed: ${JSON.stringify(commandsResponse)}`);
		}
		const commands = commandsResponse.data?.commands ?? [];
		const extensionCommands = commands.filter((command) => command.source === "extension").map((command) => command.name);
		result.safeMessages.commands = extensionCommands.length > 0 ? extensionCommands.join(", ") : commands.map((command) => command.name).join(", ");
		if (!commands.some((command) => ["state", "rck", "hermes"].includes(command.name))) {
			result.safety.warnings.push("Expected commands missing from RPC bridge response");
		}

		const statusMessage = await (async () => {
			try {
				await runPrompt("2", "/rck status", "status");
				const messages = await getMessages("3");
				return getLatestCustomMessage(messages, "rck-bridge-status", ["RCK status", "No RCK storage found."]);
			} catch (error) {
				result.safety.warnings.push(`status command failed: ${error instanceof Error ? error.message : String(error)}`);
				return undefined;
			}
		})();

		const listMessage = await (async () => {
			try {
				await runPrompt("4", "/rck list", "list");
				const messages = await getMessages("5");
				return getLatestCustomMessage(messages, "rck-bridge-status", ["RCK list", "No RCK storage found."]);
			} catch (error) {
				result.safety.warnings.push(`list command failed: ${error instanceof Error ? error.message : String(error)}`);
				return undefined;
			}
		})();

		const superviseMessage = await (async () => {
			try {
				await runPrompt("6", "/rck supervise", "supervise");
				const messages = await getMessages("7");
				return getLatestCustomMessage(messages, "rck-bridge-status", ["RCK supervise", "No RCK storage found."]);
			} catch (error) {
				result.safety.warnings.push(`supervise command failed: ${error instanceof Error ? error.message : String(error)}`);
				return undefined;
			}
		})();

		if (statusMessage?.content) {
			result.safeMessages.status = String(statusMessage.content);
			const parsedStatus = parseStatusContent(statusMessage.content);
			result.status.available = true;
			result.status.summary = parsedStatus.summary;
			result.trace.traceId = parsedStatus.traceId;
			if (parsedStatus.latestHermes) {
				result.latestHermes.available = true;
				result.latestHermes.summary = parsedStatus.latestHermes;
				const latestHermesMode = parsedStatus.latestHermes.match(/mode=([^,\s]+)/)?.[1] ?? null;
				const latestHermesStatus = parsedStatus.latestHermes.match(/status=([^,\s]+)/)?.[1] ?? null;
				const evidenceRefPresent = /evidence=(stdout|stderr|stdout\/stderr)/.test(parsedStatus.latestHermes);
				result.latestHermes.mode = latestHermesMode;
				result.latestHermes.status = latestHermesStatus;
				result.latestHermes.evidenceRefPresent = evidenceRefPresent;
			}
		}

		if (listMessage?.content) {
			result.safeMessages.list = String(listMessage.content);
			const parsedList = parseListContent(listMessage.content);
			result.inventory.available = true;
			result.inventory.counts = parsedList.counts;
			result.inventory.summary = parsedList.summary;
			if (!result.trace.traceId && parsedList.traceId) {
				result.trace.traceId = parsedList.traceId;
			}
			if (parsedList.latestHermes && !result.latestHermes.available) {
				const latestHermesMode = parsedList.latestHermes.match(/mode=([^,\s]+)/)?.[1] ?? null;
				const latestHermesStatus = parsedList.latestHermes.match(/status=([^,\s]+)/)?.[1] ?? null;
				result.latestHermes.available = Boolean(latestHermesMode || latestHermesStatus);
				result.latestHermes.mode = latestHermesMode;
				result.latestHermes.status = latestHermesStatus;
				result.latestHermes.evidenceRefPresent = /evidence=(stdout|stderr|stdout\/stderr)/.test(parsedList.latestHermes);
				result.latestHermes.summary = parsedList.latestHermes;
			}
		}

		if (superviseMessage?.content) {
			result.safeMessages.supervise = String(superviseMessage.content);
			const parsedSupervision = parseSupervisionContent(superviseMessage.content);
			result.supervision.available = true;
			result.supervision.level = parsedSupervision.level;
			result.supervision.reason = parsedSupervision.reason ?? String(superviseMessage.content);
			result.supervision.recommendedAction = parsedSupervision.recommendedAction ?? (String(superviseMessage.content).includes("Run /state") ? "Run /state first." : "No action needed");
			result.supervision.needsAttention = parsedSupervision.needsAttention;
			result.supervision.signals = parsedSupervision.signals;
			if (!result.trace.traceId && parsedSupervision.traceId) {
				result.trace.traceId = parsedSupervision.traceId;
			}
			result.trace.healthLevel = parsedSupervision.level;
			result.trace.needsAttention = parsedSupervision.needsAttention;
			result.supervision.summary = parsedSupervision.summary;
			if (parsedSupervision.latestEventId) {
				result.latestHermes.latestEventId = parsedSupervision.latestEventId;
			}
			if (parsedSupervision.latestRunId) {
				result.latestHermes.runId = parsedSupervision.latestRunId;
			}
		}

		if (!result.latestHermes.available && result.inventory.summary.includes("latestHermes=")) {
			result.latestHermes.available = true;
		}

		if (!result.status.summary) {
			result.status.summary = statusMessage?.content ? String(statusMessage.content) : "No status message returned";
		}
		if (!result.inventory.summary) {
			result.inventory.summary = listMessage?.content ? String(listMessage.content) : "No inventory message returned";
		}
		if (!result.supervision.summary) {
			result.supervision.summary = superviseMessage?.content ? String(superviseMessage.content) : "No supervision message returned";
		}

		if (result.trace.traceId === null) {
			result.trace.healthLevel = result.supervision.available ? result.supervision.level : "unknown";
			result.trace.needsAttention = result.supervision.available ? result.supervision.needsAttention : false;
		}

		if (!result.latestHermes.available) {
			result.latestHermes.mode = null;
			result.latestHermes.status = null;
			result.latestHermes.evidenceRefPresent = false;
		}

		if (!result.supervision.available) {
			result.supervision = {
				...result.supervision,
				level: "unknown",
				reason: result.supervision.reason || "No supervision message returned",
				recommendedAction: result.supervision.recommendedAction || "No action needed",
			};
		}

		if (result.trace.traceId === null) {
			result.trace.traceId = null;
		}

		process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
		return 0;
	} catch (error) {
		const message = error instanceof Error ? error.message : String(error);
		process.stderr.write(`RPC cockpit failed: ${message}\n`);
		if (stderrTail.length > 0) {
			process.stderr.write(`stderr tail:\n${stderrTail.slice(-10).map((line) => `  ${line}`).join("\n")}\n`);
		}
		const fallback = {
			schemaVersion: "rck.cockpit/v0.1",
			generatedAtUtc: nowUtc(),
			repo: {
				path: repo.path,
				branch: repo.branch,
				dirty: repo.dirty,
			},
			trace: {
				traceId: null,
				healthLevel: "unknown",
				needsAttention: false,
			},
			status: buildFallbackSummary("/rck status", message),
			supervision: {
				...buildFallbackSummary("/rck supervise", message),
				level: "unknown",
				recommendedAction: "No action needed",
				needsAttention: false,
				signals: {},
			},
			inventory: {
				...buildFallbackSummary("/rck list", message),
				counts: {},
			},
			latestHermes: {
				available: false,
				mode: null,
				status: null,
				evidenceRefPresent: false,
				runId: null,
				latestEventId: null,
				summary: "",
			},
			actions: [],
			capabilities: {
				canObserve: true,
				canAct: true,
				canGateRealHermes: true,
				canResolveTrace: true,
				canListArtifacts: true,
			},
			safety: {
				realHermesGated: true,
				rawEvidenceExposed: false,
				readOnly: true,
				warnings: [message],
			},
			safeMessages: {
				commands: "",
				status: message,
				list: message,
				supervise: message,
			},
		};
		process.stdout.write(`${JSON.stringify(fallback, null, 2)}\n`);
		return 1;
	} finally {
		clearTimeout(timeout);
		await stopChild();
	}
}

main().then((code) => {
	process.exitCode = code;
});
