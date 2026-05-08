export type RckSchemaVersion = 1;

export type RckEventType =
	| "HermesRunRequested"
	| "HermesRunRecorded"
	| "StatePackCreated"
	| "ContextPackInjected";

export type RckActor = "user" | "pi" | "extension";
export type RckLlmInjectionPolicy = "none" | "safe-summary" | "safe-context";
export type RckPiWriteTarget = "session" | "entry" | "custom_message";
export type RckWriteTarget = "pi" | "rck" | "llm";

export interface RckArtifactRef {
	kind: "file" | "session" | "entry" | "command" | "git" | "url";
	reference: string;
}

export interface RckCommandRef {
	name: string;
	args?: string;
}

export interface RckGitRef {
	branch?: string;
	commit?: string;
	remote?: string;
}

export interface RckCorrelationRef {
	traceId: string;
	requestEventId?: string;
	parentEventId?: string;
}

export interface RckEventBase {
	eventId: string;
	eventType: RckEventType;
	schemaVersion: RckSchemaVersion;
	timestamp: string;
	actor: RckActor;
	summary: string;
	tags?: string[];
	correlation: RckCorrelationRef;
	piWriteTarget: RckPiWriteTarget;
	rckWriteTarget: RckWriteTarget;
	llmInjectionPolicy: RckLlmInjectionPolicy;
	artifacts?: RckArtifactRef[];
	command?: RckCommandRef;
	git?: RckGitRef;
}

export interface HermesRunRequestedEvent extends RckEventBase {
	eventType: "HermesRunRequested";
	command: RckCommandRef;
	promptSummary: string;
}

export interface HermesRunRecordedEvent extends RckEventBase {
	eventType: "HermesRunRecorded";
	requestEventId: string;
	command: RckCommandRef;
	resultSummary: string;
	exitCode: 0;
	stdout?: string;
	stderr?: string;
}

export interface StatePackCreatedEvent extends RckEventBase {
	eventType: "StatePackCreated";
	stateId: string;
	stateSummary: string;
}

export interface ContextPackInjectedEvent extends RckEventBase {
	eventType: "ContextPackInjected";
	contextPackId: string;
	contextSummary: string;
}

export type RckOperationalEvent =
	| HermesRunRequestedEvent
	| HermesRunRecordedEvent
	| StatePackCreatedEvent
	| ContextPackInjectedEvent;

export function getPiWriteTarget(event: RckOperationalEvent): RckPiWriteTarget {
	switch (event.eventType) {
		case "HermesRunRequested":
		case "HermesRunRecorded":
			return "entry";
		case "StatePackCreated":
			return "entry";
		case "ContextPackInjected":
			return "custom_message";
	}
}

export function getRckWriteTarget(event: RckOperationalEvent): RckWriteTarget {
	switch (event.eventType) {
		case "HermesRunRequested":
		case "HermesRunRecorded":
			return "rck";
		case "StatePackCreated":
			return "pi";
		case "ContextPackInjected":
			return "llm";
	}
}

export function getLlmInjectionPolicy(event: RckOperationalEvent): RckLlmInjectionPolicy {
	switch (event.eventType) {
		case "HermesRunRequested":
			return "safe-summary";
		case "HermesRunRecorded":
			return "none";
		case "StatePackCreated":
			return "safe-summary";
		case "ContextPackInjected":
			return "safe-context";
	}
}

export function shouldEnterLlmContext(event: RckOperationalEvent): boolean {
	return getLlmInjectionPolicy(event) !== "none";
}

export function validateBaseEvent(event: Partial<RckEventBase>): event is RckEventBase {
	if (!event) return false;
	return (
		typeof event.eventId === "string"
		&& typeof event.eventType === "string"
		&& event.schemaVersion === 1
		&& typeof event.timestamp === "string"
		&& typeof event.actor === "string"
		&& typeof event.summary === "string"
		&& event.correlation !== undefined
		&& typeof event.correlation.traceId === "string"
		&& typeof event.piWriteTarget === "string"
		&& typeof event.rckWriteTarget === "string"
		&& typeof event.llmInjectionPolicy === "string"
	);
}

export function validateEventSpecificFields(event: Partial<RckOperationalEvent>): boolean {
	if (!validateBaseEvent(event)) return false;

	switch (event.eventType) {
		case "HermesRunRequested":
			return typeof event.promptSummary === "string" && typeof event.command?.name === "string";
		case "HermesRunRecorded":
			return (
				typeof event.requestEventId === "string"
				&& typeof event.resultSummary === "string"
				&& event.exitCode === 0
				&& typeof event.command?.name === "string"
			);
		case "StatePackCreated":
			return typeof event.stateId === "string" && typeof event.stateSummary === "string";
		case "ContextPackInjected":
			return typeof event.contextPackId === "string" && typeof event.contextSummary === "string";
	}
}
