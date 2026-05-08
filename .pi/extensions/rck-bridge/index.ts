import type { ExtensionAPI } from "@mariozechner/pi-coding-agent";
import type {
	ContextPackInjectedEvent,
	HermesRunRecordedEvent,
	HermesRunRequestedEvent,
	RckOperationalEvent,
	RckEventBase,
	StatePackCreatedEvent,
} from "./rck-events.js";

const SCHEMA_VERSION = 1 as const;

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

export default function registerRckBridge(pi: ExtensionAPI) {
	pi.registerCommand("hermes", {
		description: "Mock Hermes bridge (POC only)",
		handler: async (args, ctx) => {
			const { payload } = parseArgs(args);
			const promptSummary = payload || "Mock Hermes inspection request";
			const requestEvent: HermesRunRequestedEvent = {
				...createBaseEvent("HermesRunRequested", promptSummary, "user", {
					tags: ["rck-bridge", "mock", "hermes"],
					piSessionId: ctx.sessionManager.getSessionId(),
				}),
				eventType: "HermesRunRequested",
				command: { name: "/hermes", args: payload || undefined },
				promptSummary,
			};

			appendMockEvent(pi, "rck-bridge.hermes.requested", requestEvent);

			const recorded: HermesRunRecordedEvent = {
				...createBaseEvent("HermesRunRecorded", "Mock Hermes run recorded", "extension", {
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
				command: { name: "/hermes", args: payload || undefined },
				resultSummary: `Mock Hermes completed: ${promptSummary.slice(0, 120) || "inspection requested"}`,
				exitCode: 0,
			};
			appendMockEvent(pi, "rck-bridge.hermes.recorded", recorded);
			appendCustomMessage(
				pi,
				"rck-bridge-status",
				`Mock Hermes completed: emitted HermesRunRequested and HermesRunRecorded`,
				{ eventType: recorded.eventType, mock: true, requestEventId: requestEvent.eventId },
			);

			notify(pi, "Mock /hermes recorded in Pi custom entries", "info");
		},
	});

	pi.registerCommand("state", {
		description: "Mock state pack creation (POC only)",
		handler: async (args, ctx) => {
			const payload = args.trim();
			const summary = payload || "Mock state snapshot for current branch";
			const stateEvent: StatePackCreatedEvent = {
				...createBaseEvent("StatePackCreated", summary, "pi", {
					tags: ["rck-bridge", "mock", "state"],
					piSessionId: ctx.sessionManager.getSessionId(),
					llmInjectionPolicy: "safe-summary",
					piWriteTarget: "entry",
					rckWriteTarget: "pi",
				}),
				eventType: "StatePackCreated",
				stateId: createId("state"),
				stateSummary: summary,
			};
			appendMockEvent(pi, "rck-bridge.state.created", stateEvent);
			appendCustomMessage(pi, "rck-bridge-status", `Mock state created: ${summary.slice(0, 100)}`, {
				eventType: stateEvent.eventType,
				mock: true,
			});
			notify(pi, "Mock /state recorded in Pi custom entries", "info");
		},
	});

	pi.registerCommand("rck", {
		description: "Mock RCK bridge commands (POC only)",
		handler: async (args, ctx) => {
			const { command, payload } = parseArgs(args);
			if (command !== "inject") {
				notify(pi, "Usage: /rck inject <context summary>", "warning");
				return;
			}

			const contextSummary = payload || "Mock safe context pack for the next turn";
			const event: ContextPackInjectedEvent = {
				...createBaseEvent("ContextPackInjected", contextSummary, "extension", {
					tags: ["rck-bridge", "mock", "context"],
					piSessionId: ctx.sessionManager.getSessionId(),
					llmInjectionPolicy: "safe-context",
					piWriteTarget: "custom_message",
					rckWriteTarget: "llm",
				}),
				eventType: "ContextPackInjected",
				contextPackId: createId("pack"),
				contextSummary,
			};

			appendCustomMessage(
				pi,
				"rck-bridge.context.injected",
				`MOCK CONTEXT PACK: ${contextSummary.slice(0, 140)}`,
				{ eventType: event.eventType, mock: true },
			);
			appendMockEvent(pi, "rck-bridge.context.injected.record", event);
			notify(pi, "Mock /rck inject written as custom_message", "info");
		},
	});
}
