#!/usr/bin/env node
import { createServer } from "node:http";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const repoRoot = resolve(__dirname, "..");
const rckRoot = join(repoRoot, ".pi", "rck");
const defaultPort = Number.parseInt(process.env.RUFUSCHAT_UI_PORT ?? "8787", 10);
const host = process.env.RUFUSCHAT_UI_HOST ?? "127.0.0.1";

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

function renderValue(value, fallback = "—") {
	if (value === null || value === undefined || value === "") {
		return fallback;
	}
	return String(value);
}

function renderBadge(level) {
	return `<span class="badge badge-${String(level ?? "unknown")}">${renderValue(level, "unknown")}</span>`;
}

function renderKeyValue(label, value) {
	return `<div class="kv"><div class="k">${label}</div><div class="v">${renderValue(value)}</div></div>`;
}

function renderList(items) {
	if (!items.length) {
		return "<div class=\"empty\">No items.</div>";
	}
	return `<ul class="list">${items.map((item) => `<li>${item}</li>`).join("")}</ul>`;
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
.toolbar, .grid, .messages { display: grid; gap: 12px; }
.toolbar { grid-template-columns: repeat(auto-fit, minmax(150px, max-content)); }
button { border: 1px solid var(--border); background: linear-gradient(180deg, #1a2550, #111a36); color: var(--text); padding: 10px 14px; border-radius: 10px; cursor: pointer; font-weight: 600; }
button:hover:not(:disabled) { border-color: var(--accent); }
button:disabled { opacity: 0.45; cursor: not-allowed; }
.grid { grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); }
.card { border: 1px solid var(--border); background: linear-gradient(180deg, rgba(18, 26, 51, 0.96), rgba(12, 18, 39, 0.96)); border-radius: 16px; padding: 16px; box-shadow: 0 10px 32px rgba(0,0,0,.18); }
.card h2 { margin: 0 0 10px; font-size: 16px; }
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
    <button disabled title="Deferred to 8B">Create State</button>
    <button disabled title="Deferred to 8B">Inject Context</button>
    <button disabled title="Deferred to 8B">Create Anchor</button>
    <button disabled title="Deferred to 8B">Run Hermes Fake</button>
  </div>

  <div class="notice" id="status-message">${messages[0]}</div>

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
        ${renderKeyValue("evidenceRefs", latestHermesRun?.evidenceRefs?.length ? JSON.stringify(latestHermesRun.evidenceRefs, null, 2) : "[]")}
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
};

function setStatusMessage(text) {
  document.getElementById('status-message').textContent = text;
}

async function fetchJson(url) {
  const response = await fetch(url, { headers: { 'accept': 'application/json' } });
  if (!response.ok) {
    throw new Error(url + ' failed with ' + response.status);
  }
  return response.json();
}

async function refresh() {
  const [health, status, supervision, inventory] = await Promise.all([
    fetchJson(endpoints.health),
    fetchJson(endpoints.status),
    fetchJson(endpoints.supervision),
    fetchJson(endpoints.inventory),
  ]);
  document.querySelector('header .sub').innerHTML =
    '<span>traceId: <span class="mono">' + (status.traceId ?? 'null') + '</span></span>' +
    '<span>health: <span class="badge badge-' + (health.ok ? 'ok' : 'error') + '">' + (health.ok ? 'ok' : 'error') + '</span></span>' +
    '<span>storage: ' + (health.storageExists ? 'present' : 'absent') + '</span>' +
    '<span>generatedAt: <span class="mono">' + (status.generatedAt ?? health.generatedAt ?? '') + '</span></span>';
  setStatusMessage('Supervision: ' + supervision.level + ' — ' + supervision.reason);
}

document.getElementById('refresh').addEventListener('click', () => {
  refresh().catch((error) => setStatusMessage(error.message));
});
refresh().catch((error) => setStatusMessage(error.message));
</script>
</body>
</html>`;
}

function handleRequest(req, res) {
	const url = new URL(req.url ?? "/", `http://${req.headers.host ?? `${host}:${defaultPort}`}`);
	const snapshot = buildSnapshot();

	if (req.method === "GET" && (url.pathname === "/" || url.pathname === "/index.html")) {
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
		jsonResponse(res, 200, snapshot.health);
		return;
	}

	if (req.method === "GET" && url.pathname === "/api/status") {
		jsonResponse(res, 200, snapshot.statusDto);
		return;
	}

	if (req.method === "GET" && url.pathname === "/api/supervision") {
		jsonResponse(res, 200, snapshot.supervisionDto);
		return;
	}

	if (req.method === "GET" && url.pathname === "/api/inventory") {
		jsonResponse(res, 200, snapshot.inventoryDto);
		return;
	}

	jsonResponse(res, 404, {
		error: "not_found",
		path: url.pathname,
	});
}

const server = createServer(handleRequest);
server.listen(defaultPort, host, () => {
	process.stdout.write(`RufusChat UI server listening at http://${host}:${defaultPort}\n`);
});

process.on("SIGINT", () => {
	server.close(() => process.exit(0));
});

process.on("SIGTERM", () => {
	server.close(() => process.exit(0));
});
