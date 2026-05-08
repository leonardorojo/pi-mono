import type { ExtensionAPI } from "@mariozechner/pi-coding-agent";

type RckEventType =
	| "HermesRunRequested"
	| "HermesRunRecorded"
	| "StatePackCreated"
	| "ContextPackInjected";

type RckActor = "user" | "pi" | "extension";

type BaseEvent = {
	eventId: string;
	eventType: RckEventType;
	schemaVersion: 1;
	timestamp: string;
	actor: RckActor;
	summary: string;
	tags?: string[];
	traceId: string;
	branchId?: string;
	piSessionId?: string;
	piEntryId?: string;
	parentPiEntryId?: string;
};

type HermesRunRequestedEvent = BaseEvent & {
	eventType: "HermesRunRequested";
	command: string;
	promptSummary: string;
};

type HermesRunRecordedEvent = BaseEvent & {
	eventType: "HermesRunRecorded";
	requestEventId: string;
	command: string;
	resultSummary: string;
};

type StatePackCreatedEvent = BaseEvent & {
	eventType: "StatePackCreated";
	stateId: string;
	stateSummary: string;
};

type ContextPackInjectedEvent = BaseEvent & {
	eventType: "ContextPackInjected";
	contextPackId: string;
	contextSummary: string;
};

function createId(prefix: string): string {
	return `${prefix}_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
}

function nowUtc(): string {
	return new Date().toISOString();
}

function createBaseEvent(
	eventType: RckEventType,
	summary: string,
	actor: RckActor,
	overrides: Partial<BaseEvent> = {},
): BaseEvent {
	return {
		eventId: createId("evt"),
		eventType,
		schemaVersion: 1,
		timestamp: nowUtc(),
		actor,
		summary,
		traceId: overrides.traceId ?? createId("trace"),
		branchId: overrides.branchId,
		piSessionId: overrides.piSessionId,
		piEntryId: overrides.piEntryId,
		parentPiEntryId: overrides.parentPiEntryId,
		tags: overrides.tags,
	};
}

function appendCustom(pi: ExtensionAPI, customType: string, data: unknown): void {
	pi.appendEntry(customType, data);
}

function appendCustomMessage(pi: ExtensionAPI, customType: string, content: string, details?: unknown): void {
	pi.sendMessage({
		customType,
		content,
		display: true,
		details,
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
			const requestEvent = createBaseEvent("HermesRunRequested", promptSummary, "user", {
				tags: ["rck-bridge", "mock", "hermes"],
				piSessionId: ctx.sessionManager.getSessionId(),
			});

			const requested: HermesRunRequestedEvent = {
				...requestEvent,
				eventType: "HermesRunRequested",
				command: "/hermes",
				promptSummary,
			};
			appendCustom(pi, "rck-bridge.hermes.requested", requested);

			const recorded: HermesRunRecordedEvent = {
				...createBaseEvent("HermesRunRecorded", "Mock Hermes run recorded", "extension", {
					traceId: requested.traceId,
					branchId: requested.branchId,
					piSessionId: requested.piSessionId,
					piEntryId: requested.piEntryId,
					parentPiEntryId: requested.parentPiEntryId,
					tags: ["rck-bridge", "mock", "hermes", "recorded"],
				}),
				eventType: "HermesRunRecorded",
				requestEventId: requested.eventId,
				command: "/hermes",
				resultSummary: `Mock Hermes completed: ${promptSummary.slice(0, 120) || "inspection requested"}`,
			};
			appendCustom(pi, "rck-bridge.hermes.recorded", recorded);
			appendCustomMessage(
				pi,
				"rck-bridge-status",
				`Mock Hermes completed: emitted HermesRunRequested and HermesRunRecorded`,
				{ eventType: recorded.eventType, mock: true, requestEventId: requested.eventId },
			);

			ctx.ui.notify("Mock /hermes recorded in Pi custom entries", "info");
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
					piSessionId: (ctx as { sessionManager?: { getSessionId?: () => string } }).sessionManager?.getSessionId?.(),
				}),
				eventType: "StatePackCreated",
				stateId: createId("state"),
				stateSummary: summary,
			};
			appendCustom(pi, "rck-bridge.state.created", stateEvent);
			appendCustomMessage(pi, "rck-bridge-status", `Mock state created: ${summary.slice(0, 100)}`, {
				eventType: stateEvent.eventType,
				mock: true,
			});
			ctx.ui.notify("Mock /state recorded in Pi custom entries", "info");
		},
	});

	pi.registerCommand("rck", {
		description: "Mock RCK bridge commands (POC only)",
		handler: async (args, ctx) => {
			const { command, payload } = parseArgs(args);
			if (command !== "inject") {
				ctx.ui.notify("Usage: /rck inject <context summary>", "warning");
				return;
			}

			const contextSummary = payload || "Mock safe context pack for the next turn";
			const event: ContextPackInjectedEvent = {
				...createBaseEvent("ContextPackInjected", contextSummary, "extension", {
					tags: ["rck-bridge", "mock", "context"],
					piSessionId: (ctx as { sessionManager?: { getSessionId?: () => string } }).sessionManager?.getSessionId?.(),
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
			appendCustom(pi, "rck-bridge.context.injected.record", event);
			ctx.ui.notify("Mock /rck inject written as custom_message", "info");
		},
	});
}
