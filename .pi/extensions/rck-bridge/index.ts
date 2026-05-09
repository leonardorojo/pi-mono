import { existsSync, readdirSync } from "node:fs";
import type { ExtensionAPI } from "@mariozechner/pi-coding-agent";
import type {
	AnchorRegisteredEvent,
	ContextPackInjectedEvent,
	HermesRunRecordedEvent,
	HermesRunRequestedEvent,
	RckOperationalEvent,
	RckEventBase,
	StatePackCreatedEvent,
} from "./rck-events.js";
import {
	createContextPackId,
	createEventId,
	createStateId,
	ensureRckStorage,
	getRckRoot,
	readCurrentTraceIndex,
	getOrCreateCurrentTrace,
	readJson,
	readLatestRckState,
	updateCurrentTraceIndex,
	updateLatestAnchorIndex,
	updateLatestContextPackIndex,
	updateLatestStateIndex,
	writeHermesEvidence,
	writeRckAnchor,
	writeRckContextPack,
	writeRckEvent,
	writeRckState,
	type CurrentTraceIndexPayload,
	type LatestAnchorIndexPayload,
	type LatestContextPackIndexPayload,
	type LatestStateIndexPayload,
	type RckAnchorPayload,
	type RckContextPackPayload,
	type RckEventPayload,
	type RckStatePayload,
} from "./rck-storage.js";
import {
	createHermesRealRunner,
	getAllowRealHermesFromEnv,
	parseHermesArgs,
	runHermesExecution,
} from "./rck-hermes.js";

const SCHEMA_VERSION = 1 as const;
const STORAGE_SCHEMA_VERSION = "0.1" as const;

function createId(prefix: string): string {
	return `${prefix}_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
}

function nowUtc(): string {
	return new Date().toISOString();
}

function createBaseEvent(
	eventType: RckOperationalEvent["eventType"],
	summary: string,
	actor: RckEventBase["actor"],
	overrides: Partial<RckEventBase> = {},
): RckEventBase {
	return {
		eventId: createId("evt"),
		eventType,
		schemaVersion: SCHEMA_VERSION,
		timestamp: nowUtc(),
		actor,
		summary,
		traceId: overrides.traceId ?? createId("trace"),
		branchId: overrides.branchId,
		piSessionId: overrides.piSessionId,
		piEntryId: overrides.piEntryId,
		parentPiEntryId: overrides.parentPiEntryId,
		tags: overrides.tags,
		correlation: overrides.correlation ?? { traceId: overrides.traceId ?? createId("trace") },
		piWriteTarget: overrides.piWriteTarget ?? "entry",
		rckWriteTarget: overrides.rckWriteTarget ?? "rck",
		llmInjectionPolicy: overrides.llmInjectionPolicy ?? "safe-summary",
	};
}

function appendMockEvent(pi: ExtensionAPI, customType: string, event: unknown): void {
	pi.appendEntry(customType, event);
}

function appendCustomMessage(pi: ExtensionAPI, customType: string, content: string, details?: unknown): void {
	pi.sendMessage({
		customType,
		content,
		display: true,
		details,
	});
}

function notify(pi: ExtensionAPI, message: string, type: "info" | "warning" | "error" = "info"): void {
	pi.sendMessage({
		customType: "rck-bridge-status",
		content: message,
		display: true,
		details: { mock: true, notifyType: type },
	});
}

function parseArgs(args: string): { command: string; payload: string } {
	const trimmed = args.trim();
	if (!trimmed) return { command: "", payload: "" };
	const [command, ...rest] = trimmed.split(/\s+/);
	return { command, payload: rest.join(" ").trim() };
}

function safeContextSummary(summary: string): string {
	return summary.replace(/\b(stdout|stderr|diff|log|logs)\b/gi, "[redacted]");
}

function stateSummaryToSafeText(state: RckStatePayload): string {
	return safeContextSummary(
		`${state.stateSummary.title}: ${state.stateSummary.objective} | scope=${state.stateSummary.scope} | next=${state.stateSummary.nextAction}`,
	);
}

function summarizeHermesEvidence(event: { stdoutRef?: RckEventPayload["stdoutRef"]; stderrRef?: RckEventPayload["stderrRef"]; payload?: { stdoutRef?: RckEventPayload["stdoutRef"]; stderrRef?: RckEventPayload["stderrRef"] } }): string {
	const stdoutRef = event.stdoutRef ?? event.payload?.stdoutRef;
	const stderrRef = event.stderrRef ?? event.payload?.stderrRef;
	const hasStdout = Boolean(stdoutRef);
	const hasStderr = Boolean(stderrRef);
	if (hasStdout && hasStderr) return "stdout/stderr";
	if (hasStdout) return "stdout";
	if (hasStderr) return "stderr";
	return "none";
}

function readLatestHermesRecordedEvent(root: string): RckEventPayload | undefined {
	const eventsDir = `${root}/events`;
	if (!existsSync(eventsDir)) {
		return undefined;
	}
	const files = readdirSync(eventsDir).filter((file) => file.endsWith(".json")).sort();
	for (let index = files.length - 1; index >= 0; index -= 1) {
		const event = readJson<RckEventPayload>(`${eventsDir}/${files[index]}`);
		if (event?.eventType === "HermesRunRecorded") {
			return event;
		}
	}
	return undefined;
}

function resolveCurrentTraceForStatus(root: string): CurrentTraceIndexPayload | undefined {
	const currentTrace = readCurrentTraceIndex(root);
	if (currentTrace) {
		return currentTrace;
	}

	const latestStateIndex = readJson<LatestStateIndexPayload>(`${root}/indexes/latest-state.json`);
	const latestContextIndex = readJson<LatestContextPackIndexPayload>(`${root}/indexes/latest-context-pack.json`);
	const latestAnchorIndex = readJson<LatestAnchorIndexPayload>(`${root}/indexes/latest-anchor.json`);
	const inferredTraceId = latestStateIndex?.traceId ?? latestContextIndex?.traceId ?? latestAnchorIndex?.traceId;
	if (!inferredTraceId) {
		return undefined;
	}

	const inferredAt = latestStateIndex?.updatedAt ?? latestContextIndex?.updatedAt ?? latestAnchorIndex?.updatedAt ?? new Date().toISOString();
	return {
		schemaVersion: "rck.current-trace/v0.1",
		artifactType: "rck.index.current-trace",
		traceId: inferredTraceId,
		createdAtUtc: inferredAt,
		updatedAtUtc: inferredAt,
		headAnchorId: latestAnchorIndex?.currentAnchorId ?? null,
		anchorCount: latestAnchorIndex ? 1 : 0,
	};
}

export default function registerRckBridge(pi: ExtensionAPI) {
	pi.registerCommand("hermes", {
		description: "Mock Hermes bridge (POC only)",
			handler: async (args, ctx) => {
				const cwd = ctx.cwd;
				const root = getRckRoot(cwd);
				ensureRckStorage(root);

				const request = parseHermesArgs(args);
				const promptSummary = request.prompt || "Mock Hermes inspection request";
				const currentTrace = getOrCreateCurrentTrace(root);
				const traceId = currentTrace.traceId;
				const requestEvent: HermesRunRequestedEvent = {
					...createBaseEvent("HermesRunRequested", promptSummary, "user", {
						traceId,
						tags: ["rck-bridge", request.mode, "hermes"],
						piSessionId: ctx.sessionManager.getSessionId(),
					}),
					eventType: "HermesRunRequested",
					command: { name: "/hermes", args: request.rawArgs || undefined },
					promptSummary,
				};

				appendMockEvent(pi, "rck-bridge.hermes.requested", requestEvent);
				const requestEventRecord: RckEventPayload = {
					schemaVersion: STORAGE_SCHEMA_VERSION,
					artifactType: "rck.event",
					id: requestEvent.eventId,
					traceId: requestEvent.traceId,
					createdAt: requestEvent.timestamp,
					repoPath: cwd,
					cwd,
					piSessionId: requestEvent.piSessionId ?? ctx.sessionManager.getSessionId(),
					piEntryId: requestEvent.piEntryId ?? null,
					parentPiEntryId: requestEvent.parentPiEntryId ?? null,
					branchId: requestEvent.branchId ?? null,
					summary: `Hermes run requested: ${promptSummary}`,
					actor: "user",
					tags: requestEvent.tags,
					correlation: requestEvent.correlation,
					piWriteTarget: requestEvent.piWriteTarget,
					rckWriteTarget: requestEvent.rckWriteTarget,
					llmInjectionPolicy: requestEvent.llmInjectionPolicy,
					eventId: requestEvent.eventId,
					eventType: "HermesRunRequested",
					command: { name: "/hermes", args: request.rawArgs || undefined },
					payload: {
						mode: request.mode,
						promptSummary,
						timeoutMs: request.timeoutMs,
					},
				};
				const requestEventRef = writeRckEvent(root, requestEventRecord);
				updateCurrentTraceIndex(root, { updatedAtUtc: requestEvent.timestamp });

				const allowRealExecution = getAllowRealHermesFromEnv();
				const fakeHermesRunner = async (runRequest: typeof request) => {
					const preview = runRequest.prompt.slice(0, 120) || "inspection requested";
					const failed = /\b(fail|error|stderr)\b/i.test(runRequest.prompt);
					if (failed) {
						return {
							exitCode: 1,
							timedOut: false,
							stdout: "",
							stderr: `mock-hermes-stderr: ${preview}`,
							durationMs: 0,
						};
					}

					return {
						exitCode: 0,
						timedOut: false,
						stdout: `mock-hermes-stdout: ${preview}`,
						stderr: "",
						durationMs: 0,
					};
				};
				const runner = request.mode === "real" && allowRealExecution
					? createHermesRealRunner({ cwd, env: process.env })
					: fakeHermesRunner;
				const result = await runHermesExecution(request, runner, { allowRealExecution });

				const recordedAt = nowUtc();
				const evidence = writeHermesEvidence(
					root,
					requestEvent.eventId,
					recordedAt,
					result.stdout ?? "",
					result.stderr ?? "",
				);

				const recorded: HermesRunRecordedEvent = {
					...createBaseEvent("HermesRunRecorded", result.safeSummary, "extension", {
						traceId: requestEvent.traceId,
						branchId: requestEvent.branchId,
						piSessionId: requestEvent.piSessionId,
						piEntryId: requestEvent.piEntryId,
						parentPiEntryId: requestEvent.parentPiEntryId,
						tags: ["rck-bridge", result.mode, "hermes", "recorded"],
						correlation: {
							traceId: requestEvent.traceId,
							requestEventId: requestEvent.eventId,
							parentEventId: requestEvent.eventId,
						},
						piWriteTarget: "entry",
						rckWriteTarget: "rck",
						llmInjectionPolicy: "none",
					}),
					eventType: "HermesRunRecorded",
					requestEventId: requestEvent.eventId,
					command: { name: "/hermes", args: request.rawArgs || undefined },
					resultSummary: result.safeSummary,
					exitCode: result.exitCode,
					mode: result.mode,
					status: result.status,
					timedOut: result.timedOut,
					durationMs: result.durationMs,
					blockedReason: result.blockedReason,
					safeSummary: result.safeSummary,
					stdout: undefined,
					stderr: undefined,
					stdoutRef: evidence.stdoutRef,
					stderrRef: evidence.stderrRef,
				};
				const recordedEventRecord: RckEventPayload = {
					schemaVersion: STORAGE_SCHEMA_VERSION,
					artifactType: "rck.event",
					id: recorded.eventId,
					traceId: recorded.traceId ?? requestEvent.traceId,
					createdAt: recorded.timestamp,
					repoPath: cwd,
					cwd,
					piSessionId: recorded.piSessionId ?? requestEvent.piSessionId ?? ctx.sessionManager.getSessionId(),
					piEntryId: recorded.piEntryId ?? null,
					parentPiEntryId: recorded.parentPiEntryId ?? null,
					branchId: recorded.branchId ?? null,
					summary: recorded.summary,
					actor: recorded.actor,
					tags: recorded.tags,
					correlation: recorded.correlation,
					piWriteTarget: recorded.piWriteTarget,
					rckWriteTarget: recorded.rckWriteTarget,
					llmInjectionPolicy: recorded.llmInjectionPolicy,
					eventId: recorded.eventId,
					eventType: "HermesRunRecorded",
					requestEventId: requestEvent.eventId,
					command: { name: "/hermes", args: request.rawArgs || undefined },
					resultSummary: recorded.resultSummary,
					exitCode: recorded.exitCode,
					stdout: undefined,
					stderr: undefined,
					payload: {
						runId: requestEvent.eventId,
						requestEventId: requestEvent.eventId,
						mode: result.mode,
						status: result.status,
						exitCode: result.exitCode,
						timedOut: result.timedOut,
						durationMs: result.durationMs,
						blockedReason: result.blockedReason,
						stdoutRef: evidence.stdoutRef,
						stderrRef: evidence.stderrRef,
						safeSummary: result.safeSummary,
					},
				};
				const recordedEventRef = writeRckEvent(root, recordedEventRecord);
				updateCurrentTraceIndex(root, { updatedAtUtc: recorded.timestamp });
				const visibleLabel = result.mode === "real" ? "Hermes real run recorded" : "Hermes fake run recorded";
				appendMockEvent(pi, "rck-bridge.hermes.recorded", recorded);
				appendCustomMessage(
					pi,
					"rck-bridge-status",
					`${visibleLabel}: ${result.safeSummary}`,
					{
						eventType: recorded.eventType,
						mock: result.mode !== "real",
						requestEventId: requestEvent.eventId,
						recordedEventId: recorded.eventId,
						requestEventRef,
						recordedEventRef,
						mode: result.mode,
						status: result.status,
						blockedReason: result.blockedReason,
						safeSummary: result.safeSummary,
						stdoutRef: evidence.stdoutRef,
						stderrRef: evidence.stderrRef,
						recordedAt,
					},
				);

				notify(pi, result.mode === "real" ? "Hermes real run recorded in Pi custom entries" : "Hermes fake run recorded in Pi custom entries", "info");
			},
});

	pi.registerCommand("state", {
		description: "Mock state pack creation (POC only)",
		handler: async (args, ctx) => {
			const payload = args.trim();
			const stateId = createStateId();
			const eventId = createEventId();
			const cwd = ctx.cwd;
			const repoPath = cwd;
			const root = getRckRoot(cwd);
			ensureRckStorage(root);
			const currentTrace = getOrCreateCurrentTrace(root);
			const traceId = currentTrace.traceId;

			const summary = payload || "Mock state snapshot for current branch";
			const sessionId = ctx.sessionManager.getSessionId();
			const stateEvent: StatePackCreatedEvent = {
				...createBaseEvent("StatePackCreated", summary, "pi", {
					tags: ["rck-bridge", "mock", "state"],
					traceId,
					piSessionId: sessionId,
					llmInjectionPolicy: "safe-summary",
					piWriteTarget: "entry",
					rckWriteTarget: "pi",
				}),
				eventType: "StatePackCreated",
				stateId,
				stateSummary: summary,
			};
			const statePayload: RckStatePayload = {
				schemaVersion: STORAGE_SCHEMA_VERSION,
				artifactType: "rck.state",
				id: stateId,
				stateId,
				stateType: "operational",
				traceId,
				createdAt: stateEvent.timestamp,
				repoPath,
				cwd,
				piSessionId: sessionId,
				piEntryId: null,
				parentPiEntryId: null,
				branchId: null,
				summary,
				actor: "pi",
				tags: ["rck-bridge", "mock", "state"],
				correlation: { traceId },
				piWriteTarget: "entry",
				rckWriteTarget: "rck",
				llmInjectionPolicy: "safe-summary",
				stateSummary: {
					title: "State snapshot",
					objective: summary,
					scope: "local bridge state",
					nextAction: "Review latest-state index",
				},
				source: {
					eventId,
					command: "/state",
				},
			};
			const stateRef = writeRckState(root, statePayload);
			const eventPayload: RckEventPayload = {
				schemaVersion: STORAGE_SCHEMA_VERSION,
				artifactType: "rck.event",
				id: eventId,
				eventId,
				eventType: "StatePackCreated",
				traceId,
				createdAt: stateEvent.timestamp,
				repoPath,
				cwd,
				piSessionId: sessionId,
				piEntryId: null,
				parentPiEntryId: null,
				branchId: null,
				summary,
				actor: "pi",
				tags: ["rck-bridge", "mock", "state"],
				correlation: { traceId },
				piWriteTarget: "entry",
				rckWriteTarget: "rck",
				llmInjectionPolicy: "safe-summary",
				payload: {
					stateId,
					statePath: stateRef.path,
					stateEventId: eventId,
				},
			};
			const eventRef = writeRckEvent(root, eventPayload);
			updateLatestStateIndex(root, stateRef, eventRef, traceId);
			updateCurrentTraceIndex(root, { updatedAtUtc: stateEvent.timestamp });
			appendMockEvent(pi, "rck-bridge.state.created", {
				...stateEvent,
				traceId,
				piSessionId: sessionId,
				artifacts: [{ kind: "file", reference: stateRef.path }],
			});
			appendCustomMessage(pi, "rck-bridge-status", `State stored: ${summary.slice(0, 100)}`, {
				eventType: stateEvent.eventType,
				mock: false,
				statePath: stateRef.path,
				latestStateIndexPath: "./.pi/rck/indexes/latest-state.json",
			});
			notify(pi, "RCK /state wrote local state, event, and latest-state index", "info");
		},
	});

	pi.registerCommand("rck", {
		description: "Mock RCK bridge commands (POC only)",
		handler: async (args, ctx) => {
			const { command, payload } = parseArgs(args);
			const cwd = ctx.cwd;
			const root = getRckRoot(cwd);

			if (command === "status") {
				if (!existsSync(root)) {
					appendCustomMessage(pi, "rck-bridge-status", "No RCK storage found. Run /state first.", {
						storage: "missing",
						root: ".pi/rck",
					});
					return;
				}

				const latestStateIndex = readJson<LatestStateIndexPayload>(`${root}/indexes/latest-state.json`);
				const latestContextIndex = readJson<LatestContextPackIndexPayload>(`${root}/indexes/latest-context-pack.json`);
				const latestAnchorIndex = readJson<LatestAnchorIndexPayload>(`${root}/indexes/latest-anchor.json`);
				const latestHermes = readLatestHermesRecordedEvent(root);
				const latestHermesMode = latestHermes?.mode ?? latestHermes?.payload?.mode ?? "unknown";
				const latestHermesStatus = latestHermes?.status ?? latestHermes?.payload?.status ?? "unknown";
				const latestHermesSafeSummary = latestHermes?.safeSummary ?? latestHermes?.payload?.safeSummary ?? null;
				const latestHermesRecordedAt = latestHermes?.createdAt ?? null;
				const latestHermesEvidence = latestHermes ? summarizeHermesEvidence(latestHermes) : "none";
				const currentTrace = resolveCurrentTraceForStatus(root);
				const statusLines = [
					"RCK status",
					currentTrace
						? `- current trace: traceId=${currentTrace.traceId}, headAnchorId=${currentTrace.headAnchorId ?? "null"}, anchorCount=${currentTrace.anchorCount}`
						: "- current trace: missing",
					`- state: ${latestStateIndex ? "present" : "missing"}`,
					`- context pack: ${latestContextIndex ? "present" : "missing"}`,
					`- anchor: ${latestAnchorIndex ? "present" : "missing"}`,
					latestHermes
						? `- latest Hermes: mode=${latestHermesMode}, status=${latestHermesStatus}, evidence=${latestHermesEvidence}, recordedAt=${latestHermesRecordedAt ?? "unknown"}, safeSummary=${safeContextSummary(String(latestHermesSafeSummary ?? ""))}`
						: "- latest Hermes: missing",
					`- storage: .pi/rck`,
				];
				appendCustomMessage(pi, "rck-bridge-status", statusLines.join("\n"), {
					storage: ".pi/rck",
					currentTrace: currentTrace
						? {
							traceId: currentTrace.traceId,
							headAnchorId: currentTrace.headAnchorId,
							anchorCount: currentTrace.anchorCount,
						}
						: null,
					state: latestStateIndex ? "present" : "missing",
					contextPack: latestContextIndex ? "present" : "missing",
					anchor: latestAnchorIndex ? "present" : "missing",
					latestHermes: latestHermes
						? {
							mode: latestHermesMode,
							status: latestHermesStatus,
							createdAt: latestHermesRecordedAt,
							stdoutRef: Boolean(latestHermes.stdoutRef ?? latestHermes.payload?.stdoutRef),
							stderrRef: Boolean(latestHermes.stderrRef ?? latestHermes.payload?.stderrRef),
							safeSummary: latestHermesSafeSummary,
						}
						: null,
				});
				return;
			}

			ensureRckStorage(root);

			if (command === "inject") {
				const latestIndex = readJson<LatestStateIndexPayload>(`${root}/indexes/latest-state.json`);
				const latestState = readLatestRckState(root);
				if (!latestIndex || !latestState) {
					appendCustomMessage(
						pi,
						"rck-bridge.context.missing",
						"No RCK state available for /rck inject. Run /state first.",
						{ eventType: "ContextPackInjected", mock: true, allowedToInject: false },
					);
					notify(pi, "No RCK state available for /rck inject. Run /state first.", "warning");
					return;
				}

				const contextPackId = createContextPackId();
				const contextEventId = createEventId();
				const currentTrace = getOrCreateCurrentTrace(root);
				const traceId = currentTrace.traceId;
				const sessionId = ctx.sessionManager.getSessionId();
				const safeSummaryText = stateSummaryToSafeText(latestState);
				const contextSummary = {
					title: latestState.stateSummary.title,
					objective: latestState.stateSummary.objective,
					scope: latestState.stateSummary.scope,
					nextAction: latestState.stateSummary.nextAction,
				};

				const contextPack: RckContextPackPayload = {
					schemaVersion: STORAGE_SCHEMA_VERSION,
					artifactType: "rck.context-pack",
					id: contextPackId,
					contextPackId,
					contextPackType: "safe-summary",
					traceId,
					createdAt: nowUtc(),
					repoPath: cwd,
					cwd,
					piSessionId: sessionId,
					piEntryId: null,
					parentPiEntryId: null,
					branchId: latestState.branchId,
					summary: "Safe RCK context pack synthesized from latest state",
					actor: "extension",
					tags: ["rck-bridge", "context", "safe-summary"],
					correlation: {
						traceId,
						requestEventId: latestIndex.currentEventId,
						parentEventId: latestIndex.currentEventId,
					},
					piWriteTarget: "custom_message",
					rckWriteTarget: "llm",
					llmInjectionPolicy: "safe-context",
					allowedToInject: true,
					stateId: latestState.stateId,
					statePath: latestIndex.currentStatePath,
					stateSummary: contextSummary,
					contextSummary,
				};

				const packRef = writeRckContextPack(root, contextPack);
				const contextEvent: ContextPackInjectedEvent = {
					...createBaseEvent("ContextPackInjected", "Safe RCK context pack created from latest state", "extension", {
						traceId,
						piSessionId: sessionId,
						piWriteTarget: "custom_message",
						rckWriteTarget: "llm",
						llmInjectionPolicy: "safe-context",
						tags: ["rck-bridge", "context", "safe-summary"],
						correlation: {
							traceId,
							requestEventId: latestIndex.currentEventId,
							parentEventId: latestIndex.currentEventId,
						},
					}),
					eventType: "ContextPackInjected",
					contextPackId,
					contextSummary: safeSummaryText,
					stateId: latestState.stateId,
					statePath: latestIndex.currentStatePath,
					allowedToInject: true,
				};
				const eventPayload: RckEventPayload = {
					schemaVersion: STORAGE_SCHEMA_VERSION,
					artifactType: "rck.event",
					id: contextEventId,
					eventId: contextEventId,
					eventType: "ContextPackInjected",
					traceId,
					createdAt: contextPack.createdAt,
					repoPath: cwd,
					cwd,
					piSessionId: sessionId,
					piEntryId: null,
					parentPiEntryId: null,
					branchId: latestState.branchId,
					summary: "Safe RCK context pack created from latest state",
					actor: "extension",
					tags: ["rck-bridge", "context", "safe-summary"],
					correlation: {
						traceId,
						requestEventId: latestIndex.currentEventId,
						parentEventId: latestIndex.currentEventId,
					},
					piWriteTarget: "custom_message",
					rckWriteTarget: "llm",
					llmInjectionPolicy: "safe-context",
					payload: {
						stateId: latestState.stateId,
						statePath: latestIndex.currentStatePath,
						contextPackId,
						contextPackPath: packRef.path,
						contextEventId,
					},
				};
				const eventRef = writeRckEvent(root, eventPayload);
				updateLatestContextPackIndex(root, packRef, eventRef, traceId, latestState.stateId, latestIndex.currentStatePath);
				updateCurrentTraceIndex(root, { updatedAtUtc: contextPack.createdAt });

				appendMockEvent(pi, "rck-bridge.context.injected.record", {
					...contextEvent,
					artifacts: [{ kind: "file", reference: packRef.path }],
				});
				appendCustomMessage(
					pi,
					"rck-bridge.context.injected",
					`RCK context pack ready: ${safeSummaryText}`,
					{
						eventType: "ContextPackInjected",
						allowedToInject: true,
						contextPackPath: packRef.path,
						latestContextPackIndexPath: "./.pi/rck/indexes/latest-context-pack.json",
					},
				);
				notify(pi, "RCK /rck inject wrote context pack, event, and latest-context-pack index", "info");
				return;
			}

			if (command === "anchor") {
				const anchorName = payload.trim();
				if (!anchorName) {
					notify(pi, "Usage: /rck anchor <name>", "warning");
					return;
				}

				const latestIndex = readJson<LatestStateIndexPayload>(`${root}/indexes/latest-state.json`);
				const latestState = readLatestRckState(root);
				const anchorId = createId("anchor");
				const eventId = createEventId();
				const currentTrace = getOrCreateCurrentTrace(root);
				const traceId = currentTrace.traceId;
				const sessionId = ctx.sessionManager.getSessionId();
				const hasState = Boolean(latestIndex && latestState);
				const stateId = hasState ? latestState!.stateId : undefined;
				const statePath = hasState ? latestIndex!.currentStatePath : undefined;
				const anchorSummary = hasState
					? `Anchor registered: ${anchorName} (state=${latestState!.stateSummary.title})`
					: `Anchor registered: ${anchorName}`;

				const anchorPayload: RckAnchorPayload = {
					schemaVersion: STORAGE_SCHEMA_VERSION,
					artifactType: "rck.anchor",
					id: anchorId,
					anchorId,
					anchorName,
					traceId,
					createdAt: nowUtc(),
					repoPath: cwd,
					cwd,
					piSessionId: sessionId,
					piEntryId: null,
					parentPiEntryId: null,
					branchId: latestState?.branchId ?? null,
					summary: anchorSummary,
					actor: "pi",
					tags: ["rck-bridge", "anchor"],
					correlation: hasState
						? {
							traceId,
							requestEventId: latestIndex!.currentEventId,
							parentEventId: latestIndex!.currentEventId,
						}
						: { traceId },
					piWriteTarget: "entry",
					rckWriteTarget: "rck",
					llmInjectionPolicy: "none",
					stateId,
					statePath,
				};

				const anchorRef = writeRckAnchor(root, anchorPayload);
				const anchorEvent: { anchorId: string; anchorName: string; stateId?: string; statePath?: string } = {
					anchorId,
					anchorName,
					stateId,
					statePath,
				};
				const anchorEventPayload: RckEventPayload = {
					schemaVersion: STORAGE_SCHEMA_VERSION,
					artifactType: "rck.event",
					id: eventId,
					eventId,
					eventType: "AnchorRegistered",
					traceId,
					createdAt: anchorPayload.createdAt,
					repoPath: cwd,
					cwd,
					piSessionId: sessionId,
					piEntryId: null,
					parentPiEntryId: null,
					branchId: latestState?.branchId ?? null,
					summary: anchorSummary,
					actor: "pi",
					tags: ["rck-bridge", "anchor"],
					correlation: hasState
						? {
							traceId,
							requestEventId: latestIndex!.currentEventId,
							parentEventId: latestIndex!.currentEventId,
						}
						: { traceId },
					piWriteTarget: "entry",
					rckWriteTarget: "rck",
					llmInjectionPolicy: "none",
					payload: {
						anchorId,
						anchorPath: anchorRef.path,
						anchorEventId: eventId,
						stateId,
						statePath,
					},
				};
				const eventRef = writeRckEvent(root, anchorEventPayload);
				updateLatestAnchorIndex(root, anchorRef, eventRef, traceId);
				updateCurrentTraceIndex(root, {
					headAnchorId: anchorId,
					anchorCount: currentTrace.anchorCount + 1,
					updatedAtUtc: anchorPayload.createdAt,
				});

				appendMockEvent(pi, "rck-bridge.anchor.registered", {
					eventType: "AnchorRegistered",
					traceId,
					anchorId,
					anchorName,
					stateId,
					statePath,
					artifacts: [{ kind: "file", reference: anchorRef.path }],
				});
				appendCustomMessage(
					pi,
					"rck-bridge.anchor.registered",
					hasState
						? `RCK anchor ready: ${anchorName} (linked to latest state)`
						: `RCK anchor ready: ${anchorName}`,
					{
						eventType: "AnchorRegistered",
						anchorPath: anchorRef.path,
						latestAnchorIndexPath: "./.pi/rck/indexes/latest-anchor.json",
						statePath,
					},
				);
				notify(pi, `RCK /rck anchor wrote anchor and latest-anchor index`, "info");
				return;
			}

			notify(pi, "Usage: /rck inject <context summary> | /rck anchor <name>", "warning");
		},
	});
}
