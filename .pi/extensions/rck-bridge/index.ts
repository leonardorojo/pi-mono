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
	readJson,
	readLatestRckState,
	updateLatestAnchorIndex,
	updateLatestContextPackIndex,
	updateLatestStateIndex,
	writeRckAnchor,
	writeRckContextPack,
	writeRckEvent,
	writeRckState,
	type LatestStateIndexPayload,
	type RckAnchorPayload,
	type RckContextPackPayload,
	type RckEventPayload,
	type RckStatePayload,
} from "./rck-storage.js";
import { parseHermesArgs, runHermesExecution } from "./rck-hermes.js";

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

export default function registerRckBridge(pi: ExtensionAPI) {
	pi.registerCommand("hermes", {
		description: "Mock Hermes bridge (POC only)",
		handler: async (args, ctx) => {
			const request = parseHermesArgs(args);
			const promptSummary = request.prompt || "Mock Hermes inspection request";
			const requestEvent: HermesRunRequestedEvent = {
				...createBaseEvent("HermesRunRequested", promptSummary, "user", {
					tags: ["rck-bridge", "mock", "hermes"],
					piSessionId: ctx.sessionManager.getSessionId(),
				}),
				eventType: "HermesRunRequested",
				command: { name: "/hermes", args: request.rawArgs || undefined },
				promptSummary,
			};

			appendMockEvent(pi, "rck-bridge.hermes.requested", requestEvent);

			const result = await runHermesExecution(request, async (runRequest) => {
				const preview = runRequest.prompt.slice(0, 120) || "inspection requested";
				return {
					exitCode: 0,
					timedOut: false,
					stdout: `mock-hermes-stdout: ${preview}`,
					stderr: `mock-hermes-stderr: ${preview}`,
					durationMs: 0,
				};
			});

			const recorded = {
				...createBaseEvent("HermesRunRecorded", result.safeSummary, "extension", {
					traceId: requestEvent.traceId,
					branchId: requestEvent.branchId,
					piSessionId: requestEvent.piSessionId,
					piEntryId: requestEvent.piEntryId,
					parentPiEntryId: requestEvent.parentPiEntryId,
					tags: ["rck-bridge", "mock", "hermes", "recorded"],
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
			};
			appendMockEvent(pi, "rck-bridge.hermes.recorded", recorded);
			appendCustomMessage(
				pi,
				"rck-bridge-status",
				result.safeSummary,
				{
					eventType: recorded.eventType,
					mock: true,
					requestEventId: requestEvent.eventId,
					mode: result.mode,
					status: result.status,
					blockedReason: result.blockedReason,
					safeSummary: result.safeSummary,
				},
			);

			notify(pi, "Mock /hermes recorded in Pi custom entries", "info");
		},
	});

	pi.registerCommand("state", {
		description: "Mock state pack creation (POC only)",
		handler: async (args, ctx) => {
			const payload = args.trim();
			const stateId = createStateId();
			const eventId = createEventId();
			const traceId = createId("trace");
			const cwd = ctx.cwd;
			const repoPath = cwd;
			const root = getRckRoot(cwd);
			ensureRckStorage(root);

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
				const traceId = latestState.traceId;
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
				const traceId = latestState?.traceId ?? createId("trace");
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
