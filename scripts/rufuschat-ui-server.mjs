#!/usr/bin/env node
import { createServer } from "node:http";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { spawn } from "node:child_process";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const repoRoot = resolve(__dirname, "..");
const rckRoot = join(repoRoot, ".pi", "rck");
const defaultPort = Number.parseInt(process.env.RUFUSCHAT_UI_PORT ?? "8787", 10);
const host = process.env.RUFUSCHAT_UI_HOST ?? "127.0.0.1";
const piTestPath = join(repoRoot, "pi-test.sh");
const rpcArgs = ["--offline", "--mode", "rpc", "--no-tools", "--no-extensions", "--extension", ".pi/extensions/rck-bridge/index.ts"];
const actionTimeoutMs = Number.parseInt(process.env.RUFUSCHAT_UI_ACTION_TIMEOUT_MS ?? "45000", 10);

function safeReadJson(filePath) {
	try {
		return JSON.parse(readFileSync(filePath, "utf8"));
	} catch {
		return undefined;
	}
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
			path: join(dirPath, file),
			json: safeReadJson(join(dirPath, file)),
		}))
		.filter((record) => record.json !== undefined);
}

function sleep(ms) {
	return new Promise((resolve) => setTimeout(resolve, ms));
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

function parseJsonMaybe(line) {
	try {
		return JSON.parse(line);
	} catch {
		return null;
	}
}

function latestEvent(events, eventType) {
	for (let index = events.length - 1; index >= 0; index -= 1) {
		if (events[index]?.eventType === eventType) {
			return events[index];
		}
	}
	return undefined;
}

async function waitForCondition(predicate, label, deadline) {
	while (Date.now() < deadline) {
		const value = predicate();
		if (value) {
			return value;
		}
		await sleep(50);
	}
	throw new Error(`Timed out waiting for ${label}`);
}

function stopChildProcess(child) {
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
}

function pickTraceId(...values) {
	for (const value of values) {
		if (typeof value === "string" && value.trim()) {
			return value;
		}
	}
	return null;
}

function sanitizeSafeSummary(summary) {
	return String(summary ?? "").replace(/\b(stdout|stderr|diff|log|logs)\b/gi, "[redacted]");
}

function normalizeEventSummary(event) {
	return {
		eventId: event.eventId ?? event.id ?? "unknown",
		eventType: event.eventType ?? event.type ?? "unknown",
		traceId: event.traceId ?? null,
		createdAt: event.createdAt ?? event.timestamp ?? new Date().toISOString(),
		summary: sanitizeSafeSummary(event.safeSummary ?? event.resultSummary ?? event.summary ?? "No summary available"),
	};
}

function normalizeEvidenceRef(ref) {
	if (!ref?.artifactId || !ref?.path) {
		return undefined;
	}
	return {
		kind: ref.kind ?? "unknown",
		refId: ref.artifactId,
		path: ref.path,
		isRaw: false,
		displayPolicy: "reference-only",
	};
}

function normalizeHermesRun(event) {
	if (!event) {
		return null;
	}
	const requestEventId = event.requestEventId ?? event.payload?.requestEventId ?? null;
	const runId = event.payload?.runId ?? requestEventId ?? event.eventId ?? "unknown";
	const evidenceRefs = [
		normalizeEvidenceRef(event.stdoutRef ?? event.payload?.stdoutRef),
		normalizeEvidenceRef(event.stderrRef ?? event.payload?.stderrRef),
	].filter(Boolean);

	return {
		traceId: event.traceId ?? "missing-trace",
		runId,
		requestedEventId: requestEventId,
		recordedEventId: event.eventId ?? "unknown",
		status: event.status ?? event.payload?.status ?? "unknown",
		exitCode: event.exitCode ?? event.payload?.exitCode ?? null,
		durationMs: event.durationMs ?? event.payload?.durationMs ?? null,
		safeSummary: sanitizeSafeSummary(event.safeSummary ?? event.resultSummary ?? event.summary ?? "No safe summary available"),
		evidenceRefs,
		generatedAt: event.createdAt ?? new Date().toISOString(),
	};
}

function evaluateSupervision({ latestHermes, currentTrace, latestEventId }) {
	const traceId = currentTrace?.traceId ?? latestHermes?.traceId ?? null;
	const resolvedLatestEventId = latestEventId ?? latestHermes?.recordedEventId ?? latestHermes?.eventId ?? null;

	if (!latestHermes) {
		return {
			level: "info",
			reason: "No Hermes run recorded yet",
			recommendedAction: "No action needed",
			needsAttention: false,
			traceId,
			latestEventId: resolvedLatestEventId,
			signals: {},
		};
	}

	const signals = {
		status: latestHermes.status,
		errorKind: latestHermes.errorKind,
		timedOut: latestHermes.timedOut,
		durationMs: latestHermes.durationMs,
		stdoutTruncated: latestHermes.stdoutTruncated,
		stderrTruncated: latestHermes.stderrTruncated,
		stdoutByteLength: latestHermes.stdoutByteLength,
		stderrByteLength: latestHermes.stderrByteLength,
		blockedReason: latestHermes.blockedReason,
	};

	if (latestHermes.timedOut) {
		return {
			level: "blocking",
			reason: "Latest Hermes run timed out",
			recommendedAction: "Request checkpoint or stop/retry manually",
			needsAttention: true,
			traceId,
			latestRunId: latestHermes.runId ?? latestHermes.eventId ?? null,
			latestEventId: resolvedLatestEventId,
			signals,
		};
	}

	if (latestHermes.errorKind === "hermes_not_found" || latestHermes.errorKind === "spawn_error") {
		return {
			level: "error",
			reason: "Hermes environment could not be started",
			recommendedAction: "Fix Hermes environment before retrying",
			needsAttention: true,
			traceId,
			latestRunId: latestHermes.runId ?? latestHermes.eventId ?? null,
			latestEventId: resolvedLatestEventId,
			signals,
		};
	}

	if (latestHermes.stdoutTruncated || latestHermes.stderrTruncated) {
		return {
			level: "warning",
			reason: "Hermes output was truncated",
			recommendedAction: "Request partial summary or inspect evidence manually",
			needsAttention: true,
			traceId,
			latestRunId: latestHermes.runId ?? latestHermes.eventId ?? null,
			latestEventId: resolvedLatestEventId,
			signals,
		};
	}

	if (latestHermes.errorKind === "non_zero_exit" || latestHermes.status === "failed") {
		return {
			level: "warning",
			reason: "Latest Hermes run failed",
			recommendedAction: "Review failure summary and evidence refs",
			needsAttention: true,
			traceId,
			latestRunId: latestHermes.runId ?? latestHermes.eventId ?? null,
			latestEventId: resolvedLatestEventId,
			signals,
		};
	}

	if ((latestHermes.durationMs ?? 0) > 60000) {
		return {
			level: "warning",
			reason: "Latest Hermes run is long-running",
			recommendedAction: "Consider checkpoint if the run continues too long",
			needsAttention: true,
			traceId,
			latestRunId: latestHermes.runId ?? latestHermes.eventId ?? null,
			latestEventId: resolvedLatestEventId,
			signals,
		};
	}

	if (latestHermes.blockedReason === "real-mode-disabled") {
		return {
			level: "info",
			reason: "Real Hermes execution is disabled",
			recommendedAction: "Enable RCK_BRIDGE_ALLOW_REAL_HERMES=1 only if real execution is intended",
			needsAttention: false,
			traceId,
			latestRunId: latestHermes.runId ?? latestHermes.eventId ?? null,
			latestEventId: resolvedLatestEventId,
			signals,
		};
	}

	if (latestHermes.status === "succeeded") {
		return {
			level: "ok",
			reason: "Latest Hermes run succeeded without supervision flags",
			recommendedAction: "No action needed",
			needsAttention: false,
			traceId,
			latestRunId: latestHermes.runId ?? latestHermes.eventId ?? null,
			latestEventId: resolvedLatestEventId,
			signals,
		};
	}

	return {
		level: "info",
		reason: "Latest Hermes run did not require attention",
		recommendedAction: "No action needed",
		needsAttention: false,
		traceId,
		latestRunId: latestHermes.runId ?? latestHermes.eventId ?? null,
		latestEventId: resolvedLatestEventId,
		signals,
	};
}

function readArtifact(relativePath) {
	if (!relativePath) {
		return undefined;
	}
	const absolutePath = join(repoRoot, relativePath);
	return safeReadJson(absolutePath);
}

function normalizeStateDto(latestStateIndex, latestState) {
	if (!latestStateIndex && !latestState) {
		return null;
	}
	const stateId = latestState?.stateId ?? latestStateIndex?.currentStateId ?? null;
	const statePath = latestStateIndex?.currentStatePath ?? (latestState ? `.pi/rck/states/${latestState.stateId}.json` : null);
	const traceId = pickTraceId(latestState?.traceId, latestStateIndex?.traceId);
	if (!stateId || !statePath || !traceId) {
		return null;
	}
	return {
		stateId,
		statePath,
		eventId: latestState?.source?.eventId ?? latestStateIndex?.currentEventId ?? "unknown",
		traceId,
		updatedAt: latestStateIndex?.updatedAt ?? latestState?.createdAt ?? new Date().toISOString(),
		safeSummary: latestState
			? sanitizeSafeSummary(`${latestState.stateSummary?.title ?? "State"}: ${latestState.stateSummary?.objective ?? ""} | scope=${latestState.stateSummary?.scope ?? ""} | next=${latestState.stateSummary?.nextAction ?? ""}`.trim())
			: undefined,
	};
}

function normalizeContextPackDto(latestContextPackIndex, latestContextPack) {
	if (!latestContextPackIndex && !latestContextPack) {
		return null;
	}
	const contextPackId = latestContextPack?.contextPackId ?? latestContextPackIndex?.currentContextPackId ?? null;
	const contextPackPath = latestContextPackIndex?.currentContextPackPath ?? (latestContextPack ? `.pi/rck/context-packs/${latestContextPack.contextPackId}.json` : null);
	const traceId = pickTraceId(latestContextPack?.traceId, latestContextPackIndex?.traceId);
	const stateId = latestContextPack?.stateId ?? latestContextPackIndex?.stateId ?? null;
	const statePath = latestContextPack?.statePath ?? latestContextPackIndex?.statePath ?? null;
	if (!contextPackId || !contextPackPath || !traceId || !stateId || !statePath) {
		return null;
	}
	return {
		contextPackId,
		contextPackPath,
		eventId: latestContextPackIndex?.currentEventId ?? latestContextPack?.correlation?.requestEventId ?? "unknown",
		stateId,
		statePath,
		traceId,
		updatedAt: latestContextPackIndex?.updatedAt ?? latestContextPack?.createdAt ?? new Date().toISOString(),
		safeSummary: latestContextPack ? sanitizeSafeSummary(`${latestContextPack.summary ?? "Context pack"} | state=${latestContextPack.stateSummary?.title ?? "unknown"}`) : undefined,
	};
}

function normalizeAnchorDto(latestAnchorIndex, latestAnchor) {
	if (!latestAnchorIndex && !latestAnchor) {
		return null;
	}
	const anchorId = latestAnchor?.anchorId ?? latestAnchorIndex?.currentAnchorId ?? null;
	const anchorPath = latestAnchorIndex?.currentAnchorPath ?? (latestAnchor ? `.pi/rck/anchors/${latestAnchor.anchorId}.json` : null);
	const traceId = pickTraceId(latestAnchor?.traceId, latestAnchorIndex?.traceId);
	if (!anchorId || !anchorPath || !traceId) {
		return null;
	}
	return {
		anchorId,
		anchorPath,
		eventId: latestAnchorIndex?.currentEventId ?? "unknown",
		traceId,
		updatedAt: latestAnchorIndex?.updatedAt ?? latestAnchor?.createdAt ?? new Date().toISOString(),
		label: latestAnchor?.anchorName,
		safeSummary: latestAnchor ? sanitizeSafeSummary(latestAnchor.summary ?? "Anchor registered") : undefined,
	};
}

function buildSnapshot() {
	const storageExists = existsSync(rckRoot);
	const currentTrace = safeReadJson(join(rckRoot, "indexes", "current-trace.json"));
	const latestStateIndex = safeReadJson(join(rckRoot, "indexes", "latest-state.json"));
	const latestContextPackIndex = safeReadJson(join(rckRoot, "indexes", "latest-context-pack.json"));
	const latestAnchorIndex = safeReadJson(join(rckRoot, "indexes", "latest-anchor.json"));
	const latestState = readArtifact(latestStateIndex?.currentStatePath);
	const latestContextPack = readArtifact(latestContextPackIndex?.currentContextPackPath);
	const latestAnchor = readArtifact(latestAnchorIndex?.currentAnchorPath);
	const eventRecords = loadJsonRecords(".pi/rck/events").map((record) => record.json);
	const latestHermesRecordedEvent = [...eventRecords].reverse().find((event) => event?.eventType === "HermesRunRecorded");
	const latestHermesRun = normalizeHermesRun(latestHermesRecordedEvent);
	const traceId = pickTraceId(
		currentTrace?.traceId,
		latestStateIndex?.traceId,
		latestContextPackIndex?.traceId,
		latestAnchorIndex?.traceId,
		latestHermesRun?.traceId,
	);
	const statusDto = {
		traceId,
		currentTrace: currentTrace
			? {
				traceId: currentTrace.traceId ?? traceId,
				headAnchorId: currentTrace.headAnchorId ?? null,
				anchorCount: currentTrace.anchorCount ?? 0,
				createdAtUtc: currentTrace.createdAtUtc,
				updatedAtUtc: currentTrace.updatedAtUtc,
			}
			: null,
		latestState: normalizeStateDto(latestStateIndex, latestState),
		latestContextPack: normalizeContextPackDto(latestContextPackIndex, latestContextPack),
		latestAnchor: normalizeAnchorDto(latestAnchorIndex, latestAnchor),
		latestHermesRun,
		generatedAt: new Date().toISOString(),
	};
	const supervisionEvaluation = evaluateSupervision({
		latestHermes: latestHermesRun
			? {
				...latestHermesRun,
				eventId: latestHermesRun.recordedEventId,
			}
			: undefined,
		currentTrace: traceId ? { traceId } : undefined,
		latestEventId: latestHermesRun?.recordedEventId ?? null,
	});
	const supervisionDto = {
		traceId: supervisionEvaluation.traceId ?? traceId,
		level: supervisionEvaluation.level,
		reason: supervisionEvaluation.reason,
		recommendedAction: supervisionEvaluation.recommendedAction,
		needsAttention: supervisionEvaluation.needsAttention,
		latestRunId: supervisionEvaluation.latestRunId ?? null,
		latestEventId: supervisionEvaluation.latestEventId ?? null,
		signals: supervisionEvaluation.signals,
		generatedAt: new Date().toISOString(),
	};
	const latestEvents = eventRecords.slice(-5).map(normalizeEventSummary);
	const inventoryDto = {
		traceId,
		counts: {
			states: existsSync(join(rckRoot, "states")) ? readdirSync(join(rckRoot, "states")).filter((file) => file.endsWith(".json")).length : 0,
			contextPacks: existsSync(join(rckRoot, "context-packs")) ? readdirSync(join(rckRoot, "context-packs")).filter((file) => file.endsWith(".json")).length : 0,
			anchors: existsSync(join(rckRoot, "anchors")) ? readdirSync(join(rckRoot, "anchors")).filter((file) => file.endsWith(".json")).length : 0,
			events: eventRecords.length,
			hermesRuns: eventRecords.filter((event) => event?.eventType === "HermesRunRecorded").length,
		},
		latestEvents,
		latestHermesRun,
		generatedAt: new Date().toISOString(),
	};
	return {
		storageExists,
		traceId,
		statusDto,
		supervisionDto,
		inventoryDto,
		health: {
			ok: true,
			traceId,
			storageExists,
			generatedAt: new Date().toISOString(),
		},
	};
}

function jsonResponse(res, statusCode, payload) {
	const body = `${JSON.stringify(payload, null, 2)}\n`;
	res.writeHead(statusCode, {
		"content-type": "application/json; charset=utf-8",
		"cache-control": "no-store",
		"content-length": Buffer.byteLength(body, "utf8"),
	});
	res.end(body);
}

function escapeHtml(value) {
	return String(value ?? "").replace(/[&<>"']/g, (character) => ({
		"&": "&amp;",
		"<": "&lt;",
		">": "&gt;",
		'"': "&quot;",
		"'": "&#39;",
	}[character]));
}

function renderValue(value, fallback = "—") {
	if (value === null || value === undefined || value === "") {
		return fallback;
	}
	return escapeHtml(value);
}

function renderBadge(level) {
	return `<span class="badge badge-${String(level ?? "unknown")}">${renderValue(level, "unknown")}</span>`;
}

function renderKeyValue(label, value) {
	return `<div class="kv"><div class="k">${escapeHtml(label)}</div><div class="v">${renderValue(value)}</div></div>`;
}

function renderList(items) {
	if (!items.length) {
		return "<div class=\"empty\">No items.</div>";
	}
	return `<ul class="list">${items.map((item) => `<li>${escapeHtml(item)}</li>`).join("")}</ul>`;
}

function renderHtml(snapshot) {
	const { statusDto, supervisionDto, inventoryDto, health } = snapshot;
	const latestState = statusDto.latestState;
	const latestContextPack = statusDto.latestContextPack;
	const latestAnchor = statusDto.latestAnchor;
	const latestHermesRun = statusDto.latestHermesRun ?? inventoryDto.latestHermesRun;
	const messages = [
		`Status: ${statusDto.traceId ? `trace ${statusDto.traceId}` : "no trace"}`,
		`Supervision: ${supervisionDto.level} — ${supervisionDto.reason}`,
		`Action: ${supervisionDto.recommendedAction}`,
	];
	const recentEvents = inventoryDto.latestEvents.map((event) => `${event.eventType} · ${event.summary}`);
	return `<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>RufusChat Local UI Shell</title>
<style>
:root { color-scheme: dark; --bg: #0b1020; --panel: #121a33; --panel2: #0f1730; --text: #e7ecff; --muted: #98a4d4; --border: #27345e; --accent: #8cc7ff; --ok: #67d7a3; --warn: #f5c96b; --err: #ff8b8b; }
* { box-sizing: border-box; }
body { margin: 0; font: 14px/1.5 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; background: radial-gradient(circle at top, #162043 0%, var(--bg) 55%); color: var(--text); }
header { padding: 20px 24px 12px; border-bottom: 1px solid var(--border); background: rgba(7, 11, 24, 0.7); position: sticky; top: 0; backdrop-filter: blur(10px); }
h1 { margin: 0 0 8px; font-size: 20px; }
.sub { color: var(--muted); display: flex; flex-wrap: wrap; gap: 12px; }
main { padding: 20px 24px 28px; display: grid; gap: 16px; }
.toolbar, .grid, .messages, .activity-list { display: grid; gap: 12px; }
.toolbar { grid-template-columns: repeat(auto-fit, minmax(150px, max-content)); }
button { border: 1px solid var(--border); background: linear-gradient(180deg, #1a2550, #111a36); color: var(--text); padding: 10px 14px; border-radius: 10px; cursor: pointer; font-weight: 600; }
button:hover:not(:disabled) { border-color: var(--accent); }
button:disabled { opacity: 0.45; cursor: not-allowed; }
.grid { grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); }
.card { border: 1px solid var(--border); background: linear-gradient(180deg, rgba(18, 26, 51, 0.96), rgba(12, 18, 39, 0.96)); border-radius: 16px; padding: 16px; box-shadow: 0 10px 32px rgba(0,0,0,.18); }
.card h2 { margin: 0; font-size: 16px; }
.card-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 10px; }
.card-head h2 { margin: 0; }
.card-head .small { margin-left: auto; }
.activity-card { display: grid; gap: 12px; }
.activity-list { max-height: 340px; overflow: auto; padding-right: 4px; }
.activity-entry { border: 1px solid rgba(255,255,255,.06); border-radius: 12px; padding: 12px; background: rgba(10, 15, 31, 0.72); }
.activity-entry + .activity-entry { margin-top: 10px; }
.activity-top { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 8px; }
.activity-time { color: var(--muted); font-size: 12px; margin-left: auto; }
.activity-message { margin-top: 8px; color: var(--text); }
.activity-meta { color: var(--muted); margin-top: 6px; word-break: break-word; white-space: pre-wrap; }
.activity-empty { color: var(--muted); font-style: italic; }
.kv { padding: 8px 0; border-top: 1px solid rgba(255,255,255,.05); }
.kv:first-of-type { border-top: 0; padding-top: 0; }
.k { color: var(--muted); font-size: 12px; text-transform: uppercase; letter-spacing: .08em; }
.v { margin-top: 3px; white-space: pre-wrap; word-break: break-word; }
.badge { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 999px; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: .06em; border: 1px solid var(--border); }
.badge-ok { color: var(--ok); }
.badge-info { color: var(--accent); }
.badge-warning { color: var(--warn); }
.badge-error, .badge-blocking { color: var(--err); }
.badge-unknown { color: var(--muted); }
.badge-read-only { color: var(--accent); }
.badge-mutating { color: var(--warn); }
.badge-cancelled { color: var(--muted); }
.list { margin: 8px 0 0; padding-left: 18px; color: var(--text); }
.list li { margin: 6px 0; }
.empty { color: var(--muted); font-style: italic; }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
.small { color: var(--muted); font-size: 12px; }
.stack { display: grid; gap: 10px; }
.notice { border: 1px solid rgba(140, 199, 255, 0.25); background: rgba(12, 20, 42, 0.8); border-radius: 14px; padding: 14px 16px; }
footer { padding: 0 24px 20px; color: var(--muted); }
</style>
</head>
<body>
<header>
  <h1>RufusChat Local Web UI Shell</h1>
  <div class="sub">
    <span>traceId: <span class="mono">${renderValue(statusDto.traceId)}</span></span>
    <span>health: ${renderBadge(health.ok ? "ok" : "error")}</span>
    <span>storage: ${snapshot.storageExists ? "present" : "absent"}</span>
    <span>generatedAt: <span class="mono">${renderValue(statusDto.generatedAt)}</span></span>
  </div>
</header>
<main>
  <div class="toolbar">
    <button id="refresh">Refresh</button>
    <button id="create-state">Create State</button>
    <button id="inject-context">Inject Context</button>
    <button id="create-anchor">Create Anchor</button>
    <button id="run-hermes-fake">Run Hermes Fake</button>
  </div>

  <div class="notice" id="status-message">${messages[0]}</div>

  <section class="card activity-card">
    <div class="card-head">
      <h2>Activity</h2>
      <button id="clear-activity" type="button">Clear Activity</button>
    </div>
    <div id="activity-panel" class="activity-list"></div>
  </section>

  <section class="grid">
    <article class="card">
      <h2>Health / Supervision</h2>
      <div class="stack">
        ${renderKeyValue("needsAttention", supervisionDto.needsAttention ? "yes" : "no")}
        <div class="kv"><div class="k">level</div><div class="v">${renderBadge(supervisionDto.level)}</div></div>
        ${renderKeyValue("reason", supervisionDto.reason)}
        ${renderKeyValue("recommendedAction", supervisionDto.recommendedAction)}
        ${renderKeyValue("latestRunId", supervisionDto.latestRunId)}
        ${renderKeyValue("latestEventId", supervisionDto.latestEventId)}
        ${renderKeyValue("signals", JSON.stringify(supervisionDto.signals, null, 2))}
      </div>
    </article>

    <article class="card">
      <h2>Latest State / Context / Anchor</h2>
      <div class="stack">
        ${renderKeyValue("latest state", latestState ? `${latestState.stateId}\n${latestState.safeSummary ?? ""}` : "No state")}
        ${renderKeyValue("latest context pack", latestContextPack ? `${latestContextPack.contextPackId}\n${latestContextPack.safeSummary ?? ""}` : "No context pack")}
        ${renderKeyValue("latest anchor", latestAnchor ? `${latestAnchor.anchorId}\n${latestAnchor.safeSummary ?? ""}` : "No anchor")}
      </div>
    </article>

    <article class="card">
      <h2>Latest Hermes Run</h2>
      <div class="stack">
        ${renderKeyValue("runId", latestHermesRun?.runId)}
        ${renderKeyValue("status", latestHermesRun?.status)}
        ${renderKeyValue("exitCode", latestHermesRun?.exitCode)}
        ${renderKeyValue("durationMs", latestHermesRun?.durationMs)}
        ${renderKeyValue("safeSummary", latestHermesRun?.safeSummary)}
        ${renderKeyValue("evidenceRefs", latestHermesRun?.evidenceRefs?.length ? `${latestHermesRun.evidenceRefs.length} reference-only evidence refs stored` : "[]")}
      </div>
    </article>

    <article class="card">
      <h2>Inventory</h2>
      <div class="stack">
        ${renderKeyValue("states", inventoryDto.counts.states)}
        ${renderKeyValue("context packs", inventoryDto.counts.contextPacks)}
        ${renderKeyValue("anchors", inventoryDto.counts.anchors)}
        ${renderKeyValue("events", inventoryDto.counts.events)}
        ${renderKeyValue("Hermes runs", inventoryDto.counts.hermesRuns)}
        ${renderKeyValue("generatedAt", inventoryDto.generatedAt)}
      </div>
    </article>

    <article class="card">
      <h2>Action / Status Messages</h2>
      ${renderList([...messages, ...recentEvents])}
    </article>
  </section>
</main>
<footer>
  API: <span class="mono">/health</span>, <span class="mono">/api/status</span>, <span class="mono">/api/supervision</span>, <span class="mono">/api/inventory</span>
</footer>
<script>
const endpoints = {
  health: '/health',
  status: '/api/status',
  supervision: '/api/supervision',
  inventory: '/api/inventory',
  state: '/api/state',
  inject: '/api/inject',
  anchor: '/api/anchor',
  hermesFake: '/api/hermes/fake',
};
const flashKey = 'rufuschat-ui-flash';
const activityKey = 'rufuschat-ui-activity';
const maxActivityEntries = 50;

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, (character) => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;',
  }[character]));
}

function setStatusMessage(text) {
  document.getElementById('status-message').textContent = text;
}

function setFlash(text) {
  sessionStorage.setItem(flashKey, text);
}

function takeFlash() {
  const text = sessionStorage.getItem(flashKey);
  if (text) {
    sessionStorage.removeItem(flashKey);
  }
  return text;
}

function getActivityEntries() {
  try {
    const raw = sessionStorage.getItem(activityKey);
    if (!raw) {
      return [];
    }
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed.filter((entry) => entry && typeof entry === 'object') : [];
  } catch {
    return [];
  }
}

function setActivityEntries(entries) {
  sessionStorage.setItem(activityKey, JSON.stringify(entries.slice(0, maxActivityEntries)));
}

function formatActivityField(value, fallback = '—') {
  if (value === null || value === undefined || value === '') {
    return fallback;
  }
  return escapeHtml(value);
}

function renderActivityPanel() {
  const panel = document.getElementById('activity-panel');
  if (!panel) {
    return;
  }
  const entries = getActivityEntries();
  if (!entries.length) {
    panel.innerHTML = '<div class="activity-empty">No activity yet.</div>';
    return;
  }
  panel.innerHTML = entries.map((entry) => {
    const kindClass = entry.kind === 'mutating' ? 'badge-mutating' : entry.kind === 'read-only' ? 'badge-read-only' : 'badge-unknown';
    const statusClass = entry.status === 'ok' ? 'badge-ok' : entry.status === 'error' ? 'badge-error' : entry.status === 'cancelled' ? 'badge-cancelled' : 'badge-unknown';
    const details = [];
    if (entry.id !== null && entry.id !== undefined && entry.id !== '') {
      details.push('<div class="activity-meta"><span class="mono">id</span>: ' + formatActivityField(entry.id) + '</div>');
    }
    if (entry.traceId) {
      details.push('<div class="activity-meta"><span class="mono">traceId</span>: ' + formatActivityField(entry.traceId) + '</div>');
    }
    if (entry.safeSummary) {
      details.push('<div class="activity-meta"><span class="mono">safeSummary</span>: ' + formatActivityField(entry.safeSummary) + '</div>');
    }
    if (entry.recommendedAction) {
      details.push('<div class="activity-meta"><span class="mono">recommendedAction</span>: ' + formatActivityField(entry.recommendedAction) + '</div>');
    }
    if (typeof entry.needsAttention === 'boolean') {
      details.push('<div class="activity-meta"><span class="mono">needsAttention</span>: ' + (entry.needsAttention ? 'yes' : 'no') + '</div>');
    }
    if (typeof entry.evidenceRefsCount === 'number') {
      details.push('<div class="activity-meta"><span class="mono">evidenceRefs</span>: ' + entry.evidenceRefsCount + ' stored</div>');
    }
    return '<div class="activity-entry">' +
      '<div class="activity-top">' +
        '<span class="badge ' + kindClass + '">' + formatActivityField(entry.kind ?? 'unknown') + '</span>' +
        '<span class="badge ' + statusClass + '">' + formatActivityField(entry.status ?? 'unknown') + '</span>' +
        '<span class="mono">' + formatActivityField(entry.actionName ?? 'unknown') + '</span>' +
        '<span class="activity-time">' + formatActivityField(entry.timestamp ?? '—') + '</span>' +
      '</div>' +
      details.join('') +
      '<div class="activity-message">' + formatActivityField(entry.message ?? 'No message') + '</div>' +
    '</div>';
  }).join('');
}

function appendActivityEntry(entry) {
  const normalized = {
    timestamp: new Date().toLocaleString(),
    actionName: entry.actionName ?? 'unknown',
    kind: entry.kind ?? 'read-only',
    status: entry.status ?? 'ok',
    traceId: entry.traceId ?? null,
    id: entry.id ?? null,
    safeSummary: entry.safeSummary ?? null,
    recommendedAction: entry.recommendedAction ?? null,
    needsAttention: typeof entry.needsAttention === 'boolean' ? entry.needsAttention : null,
    evidenceRefsCount: typeof entry.evidenceRefsCount === 'number' ? entry.evidenceRefsCount : null,
    message: entry.message ?? '',
  };
  const nextEntries = [normalized, ...getActivityEntries()].slice(0, maxActivityEntries);
  setActivityEntries(nextEntries);
  renderActivityPanel();
}

function recordActionSuccess(actionName, kind, response, fallbackMessage) {
  const statusDto = response?.statusDto ?? {};
  const supervisionDto = response?.supervisionDto ?? {};
  const result = response?.result ?? {};
  const latestHermesRun = statusDto.latestHermesRun ?? {};
  const latestState = statusDto.latestState ?? {};
  const latestContextPack = statusDto.latestContextPack ?? {};
  const latestAnchor = statusDto.latestAnchor ?? {};
  const safeSummary = result.safeSummary ?? latestHermesRun.safeSummary ?? latestState.safeSummary ?? latestContextPack.safeSummary ?? latestAnchor.safeSummary ?? null;
  const recommendedAction = supervisionDto.recommendedAction ?? null;
  const traceId = response?.traceId ?? statusDto.traceId ?? supervisionDto.traceId ?? null;
  const id = result.stateId ?? result.contextPackId ?? result.anchorId ?? result.runId ?? latestState.stateId ?? latestContextPack.contextPackId ?? latestAnchor.anchorId ?? latestHermesRun.runId ?? null;
  const evidenceRefsCount = Array.isArray(result.evidenceRefs) ? result.evidenceRefs.length : Array.isArray(latestHermesRun.evidenceRefs) ? latestHermesRun.evidenceRefs.length : null;
  appendActivityEntry({
    actionName,
    kind,
    status: 'ok',
    traceId,
    id,
    safeSummary,
    recommendedAction,
    needsAttention: supervisionDto.needsAttention,
    evidenceRefsCount,
    message: response?.message ?? fallbackMessage,
  });
}

function recordActionCancelled(actionName, kind, message) {
  appendActivityEntry({
    actionName,
    kind,
    status: 'cancelled',
    message,
  });
}

function recordActionError(actionName, kind, errorMessage, traceId) {
  appendActivityEntry({
    actionName,
    kind,
    status: 'error',
    traceId,
    message: errorMessage,
  });
}

async function fetchJson(url) {
  const response = await fetch(url, { headers: { 'accept': 'application/json' } });
  if (!response.ok) {
    throw new Error(url + ' failed with ' + response.status);
  }
  return response.json();
}

async function postJson(url, body) {
  const response = await fetch(url, {
    method: 'POST',
    headers: {
      'accept': 'application/json',
      'content-type': 'application/json',
    },
    body: JSON.stringify(body ?? {}),
  });
  const text = await response.text();
  const data = text ? JSON.parse(text) : {};
  if (!response.ok) {
    const error = new Error(data.message ?? data.error ?? (url + ' failed with ' + response.status));
    error.response = data;
    throw error;
  }
  return data;
}

function reloadWithFlash(message) {
  setFlash(message);
  location.reload();
}

function requirePrompt(message) {
  const value = window.prompt(message);
  if (value === null) {
    return null;
  }
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
}

function applyFlash() {
  const flash = takeFlash();
  if (flash) {
    setStatusMessage(flash);
  }
}

function ensureActivityLoaded() {
  if (!getActivityEntries().length) {
    appendActivityEntry({
      actionName: 'UI loaded',
      kind: 'read-only',
      status: 'ok',
      message: 'Initial UI loaded.',
    });
    return;
  }
  renderActivityPanel();
}

function doRefresh() {
  appendActivityEntry({
    actionName: 'Refresh',
    kind: 'read-only',
    status: 'ok',
    message: 'Refresh requested.',
  });
  location.reload();
}

async function doState() {
  const actionName = 'Create State';
  const kind = 'mutating';
  if (!window.confirm('Create State will create RCK artifacts/events. Continue?')) {
    recordActionCancelled(actionName, kind, 'Create State cancelled.');
    setStatusMessage('Create State cancelled.');
    return;
  }
  try {
    const response = await postJson(endpoints.state, {});
    recordActionSuccess(actionName, kind, response, 'Create State completed.');
    reloadWithFlash(response.message ?? 'Create State completed.');
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    const traceId = error?.response?.traceId ?? error?.response?.statusDto?.traceId ?? null;
    recordActionError(actionName, kind, message, traceId);
    setStatusMessage(message);
  }
}

async function doInject() {
  const actionName = 'Inject Context';
  const kind = 'mutating';
  if (!window.confirm('Inject Context will create a context pack. Continue?')) {
    recordActionCancelled(actionName, kind, 'Inject Context cancelled.');
    setStatusMessage('Inject Context cancelled.');
    return;
  }
  try {
    const response = await postJson(endpoints.inject, {});
    recordActionSuccess(actionName, kind, response, 'Inject Context completed.');
    reloadWithFlash(response.message ?? 'Inject Context completed.');
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    const traceId = error?.response?.traceId ?? error?.response?.statusDto?.traceId ?? null;
    recordActionError(actionName, kind, message, traceId);
    setStatusMessage(message);
  }
}

async function doAnchor() {
  const actionName = 'Create Anchor';
  const kind = 'mutating';
  const label = requirePrompt('Create Anchor label:');
  if (!label) {
    recordActionCancelled(actionName, kind, 'Create Anchor cancelled: label is required.');
    setStatusMessage('Create Anchor cancelled: label is required.');
    return;
  }
  if (!window.confirm('Create Anchor will create an anchor. Continue?')) {
    recordActionCancelled(actionName, kind, 'Create Anchor cancelled.');
    setStatusMessage('Create Anchor cancelled.');
    return;
  }
  try {
    const response = await postJson(endpoints.anchor, { label });
    recordActionSuccess(actionName, kind, response, 'Create Anchor completed.');
    reloadWithFlash(response.message ?? ('Create Anchor completed: ' + label));
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    const traceId = error?.response?.traceId ?? error?.response?.statusDto?.traceId ?? null;
    recordActionError(actionName, kind, message, traceId);
    setStatusMessage(message);
  }
}

async function doHermesFake() {
  const actionName = 'Run Hermes Fake';
  const kind = 'mutating';
  const promptText = requirePrompt('Run Hermes Fake prompt:');
  if (!promptText) {
    recordActionCancelled(actionName, kind, 'Run Hermes Fake cancelled: prompt is required.');
    setStatusMessage('Run Hermes Fake cancelled: prompt is required.');
    return;
  }
  if (!window.confirm('Run Hermes Fake will record a Hermes fake run. Continue?')) {
    recordActionCancelled(actionName, kind, 'Run Hermes Fake cancelled.');
    setStatusMessage('Run Hermes Fake cancelled.');
    return;
  }
  try {
    const response = await postJson(endpoints.hermesFake, { prompt: promptText });
    recordActionSuccess(actionName, kind, response, 'Run Hermes Fake completed.');
    reloadWithFlash(response.message ?? 'Run Hermes Fake completed.');
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    const traceId = error?.response?.traceId ?? error?.response?.statusDto?.traceId ?? null;
    recordActionError(actionName, kind, message, traceId);
    setStatusMessage(message);
  }
}

document.getElementById('refresh').addEventListener('click', () => {
  doRefresh();
});
document.getElementById('create-state').addEventListener('click', () => {
  void doState();
});
document.getElementById('inject-context').addEventListener('click', () => {
  void doInject();
});
document.getElementById('create-anchor').addEventListener('click', () => {
  void doAnchor();
});
document.getElementById('run-hermes-fake').addEventListener('click', () => {
  void doHermesFake();
});
document.getElementById('clear-activity').addEventListener('click', () => {
  sessionStorage.removeItem(activityKey);
  renderActivityPanel();
});
applyFlash();
ensureActivityLoaded();
</script>
</body>
</html>`;
}

function buildActionResponse(snapshot, actionName, command, result, message) {
	return {
		ok: true,
		action: {
			name: actionName,
			command,
		},
		message,
		traceId: snapshot.traceId,
		health: snapshot.health,
		statusDto: snapshot.statusDto,
		supervisionDto: snapshot.supervisionDto,
		inventoryDto: snapshot.inventoryDto,
		result,
	};
}

async function runRpcAction(command, actionName, waitForKind) {
	const child = spawn(piTestPath, rpcArgs, {
		cwd: repoRoot,
		env: process.env,
		stdio: ["pipe", "pipe", "pipe"],
		detached: true,
	});
	const responses = new Map();
	const deadline = Date.now() + actionTimeoutMs;
	let childExit = null;

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

	const sendJson = async (payload) => {
		const line = `${JSON.stringify(payload)}
`;
		if (!child.stdin.write(line)) {
			await new Promise((resolve) => child.stdin.once("drain", resolve));
		}
	};

	const waitForResponse = async (id, label) => {
		return await waitForCondition(() => {
			const response = responses.get(id);
			if (response) {
				return response;
			}
			if (childExit && childExit.code !== 0) {
				throw new Error(`rpc child exited early while waiting for ${label}: ${JSON.stringify(childExit)}`);
			}
			return undefined;
		}, label, deadline);
	};

	const stop = async () => {
		stopChildProcess(child);
		await sleep(200);
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

	const waitForLatestHermesEventId = async (previousEventId) => {
		await waitForCondition(() => {
			const records = loadJsonRecords(".pi/rck/events").map((record) => record.json);
			const latest = latestEvent(records, "HermesRunRecorded");
			if (!latest?.eventId || latest.eventId === previousEventId) {
				return undefined;
			}
			return latest.eventId;
		}, "HermesRunRecorded event", deadline);
	};

	try {
		await sendJson({ id: "1", type: "get_commands" });
		const commandsResponse = await waitForResponse("1", "get_commands response");
		if (!commandsResponse.success) {
			throw new Error(`get_commands failed: ${JSON.stringify({ id: commandsResponse.id, success: commandsResponse.success, error: commandsResponse.error ?? null })}`);
		}
		const commandNames = new Set((commandsResponse.data?.commands ?? []).map((commandRecord) => commandRecord?.name));
		for (const name of ["state", "rck", "hermes"]) {
			if (!commandNames.has(name)) {
				throw new Error(`Missing command: ${name}`);
			}
		}

		const beforeHermesEvents = loadJsonRecords(".pi/rck/events").map((record) => record.json);
		const previousHermesEventId = latestEvent(beforeHermesEvents, "HermesRunRecorded")?.eventId ?? null;

		await sendJson({ id: "2", type: "prompt", message: command });
		const response = await waitForResponse("2", `${actionName} response`);
		if (!response.success) {
			throw new Error(`${actionName} failed: ${JSON.stringify({ id: response.id, success: response.success, error: response.error ?? null })}`);
		}

		if (waitForKind === "state") {
			await waitForCondition(() => existsSync(join(rckRoot, "indexes", "latest-state.json")), "latest-state index", deadline);
		} else if (waitForKind === "inject") {
			await waitForCondition(() => existsSync(join(rckRoot, "indexes", "latest-context-pack.json")), "latest-context-pack index", deadline);
		} else if (waitForKind === "anchor") {
			await waitForCondition(() => existsSync(join(rckRoot, "indexes", "latest-anchor.json")), "latest-anchor index", deadline);
		} else if (waitForKind === "hermes-fake") {
			await waitForLatestHermesEventId(previousHermesEventId);
		}

		return buildSnapshot();
	} catch (error) {
		const message = error instanceof Error ? error.message : String(error);
		throw new Error(`UI action ${actionName} failed: ${message}`);
	} finally {
		await stop();
	}
}

async function readJsonBody(req) {
	const chunks = [];
	for await (const chunk of req) {
		chunks.push(typeof chunk === "string" ? chunk : chunk.toString("utf8"));
	}
	const raw = chunks.join("").trim();
	if (!raw) {
		return {};
	}
	return JSON.parse(raw);
}

async function handleRequest(req, res) {
	try {
		const url = new URL(req.url ?? "/", `http://${req.headers.host ?? `${host}:${defaultPort}`}`);

		if (req.method === "GET" && (url.pathname === "/" || url.pathname === "/index.html")) {
			const snapshot = buildSnapshot();
			const body = renderHtml(snapshot);
			res.writeHead(200, {
				"content-type": "text/html; charset=utf-8",
				"cache-control": "no-store",
				"content-length": Buffer.byteLength(body, "utf8"),
			});
			res.end(body);
			return;
		}

		if (req.method === "GET" && url.pathname === "/health") {
			jsonResponse(res, 200, buildSnapshot().health);
			return;
		}

		if (req.method === "GET" && url.pathname === "/api/status") {
			jsonResponse(res, 200, buildSnapshot().statusDto);
			return;
		}

		if (req.method === "GET" && url.pathname === "/api/supervision") {
			jsonResponse(res, 200, buildSnapshot().supervisionDto);
			return;
		}

		if (req.method === "GET" && url.pathname === "/api/inventory") {
			jsonResponse(res, 200, buildSnapshot().inventoryDto);
			return;
		}

		if (req.method === "POST" && url.pathname === "/api/state") {
			const snapshot = await runRpcAction("/state", "state", "state");
			jsonResponse(res, 200, buildActionResponse(snapshot, "state", "/state", snapshot.statusDto.latestState, "Create State completed."));
			return;
		}

		if (req.method === "POST" && url.pathname === "/api/inject") {
			const snapshot = await runRpcAction("/rck inject", "inject", "inject");
			jsonResponse(res, 200, buildActionResponse(snapshot, "inject", "/rck inject", snapshot.statusDto.latestContextPack, "Inject Context completed."));
			return;
		}

		if (req.method === "POST" && url.pathname === "/api/anchor") {
			let body;
			try {
				body = await readJsonBody(req);
			} catch {
				jsonResponse(res, 400, { ok: false, error: "invalid_json", message: "Anchor request body must be valid JSON." });
				return;
			}
			const label = typeof body?.label === "string" ? body.label.trim() : "";
			if (!label) {
				jsonResponse(res, 400, { ok: false, error: "missing_label", message: "Anchor label is required." });
				return;
			}
			const command = `/rck anchor ${label}`;
			const snapshot = await runRpcAction(command, "anchor", "anchor");
			jsonResponse(res, 200, buildActionResponse(snapshot, "anchor", command, snapshot.statusDto.latestAnchor ? { ...snapshot.statusDto.latestAnchor, label } : { label }, "Create Anchor completed."));
			return;
		}

		if (req.method === "POST" && url.pathname === "/api/hermes/fake") {
			let body;
			try {
				body = await readJsonBody(req);
			} catch {
				jsonResponse(res, 400, { ok: false, error: "invalid_json", message: "Hermes fake request body must be valid JSON." });
				return;
			}
			const prompt = typeof body?.prompt === "string" ? body.prompt.trim() : "";
			if (!prompt) {
				jsonResponse(res, 400, { ok: false, error: "missing_prompt", message: "Hermes fake prompt is required." });
				return;
			}
			const command = prompt.startsWith("/hermes ") ? prompt : `/hermes ${prompt}`;
			const snapshot = await runRpcAction(command, "hermes-fake", "hermes-fake");
			jsonResponse(res, 200, buildActionResponse(snapshot, "hermes-fake", command, snapshot.statusDto.latestHermesRun, "Run Hermes Fake completed."));
			return;
		}

		jsonResponse(res, 404, {
			error: "not_found",
			path: url.pathname,
		});
	} catch (error) {
		const message = error instanceof Error ? error.message : String(error);
		if (!res.headersSent) {
			jsonResponse(res, 500, {
				ok: false,
				error: "internal_error",
				message,
			});
			return;
		}
		res.destroy(error instanceof Error ? error : undefined);
	}
}

const server = createServer((req, res) => {
	void handleRequest(req, res);
});
server.listen(defaultPort, host, () => {
	process.stdout.write(`RufusChat UI server listening at http://${host}:${defaultPort}
`);
});

process.on("SIGINT", () => {
	server.close(() => process.exit(0));
});

process.on("SIGTERM", () => {
	server.close(() => process.exit(0));
});
