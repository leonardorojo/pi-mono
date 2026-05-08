import { randomUUID } from "node:crypto";
import { mkdirSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { basename, dirname, join, relative, sep } from "node:path";

export type RckActor = "user" | "pi" | "extension";

export interface RckCorrelationRef {
	traceId: string;
	requestEventId?: string;
	parentEventId?: string;
}

export interface RckArtifactRef {
	kind: "file";
	id: string;
	path: string;
	createdAt: string;
}

export interface RckStorageBase {
	schemaVersion: "0.1";
	artifactType: string;
	id: string;
	traceId: string;
	createdAt: string;
	repoPath: string;
	cwd: string;
	piSessionId: string;
	piEntryId: string | null;
	parentPiEntryId: string | null;
	branchId: string | null;
	summary: string;
	actor: RckActor;
	tags?: string[];
	correlation: RckCorrelationRef;
	piWriteTarget: "session" | "entry" | "custom_message";
	rckWriteTarget: "pi" | "rck" | "llm";
	llmInjectionPolicy: "none" | "safe-summary" | "safe-context";
}

export interface RckStatePayload extends RckStorageBase {
	artifactType: "rck.state";
	stateId: string;
	stateType: "operational";
	stateSummary: {
		title: string;
		objective: string;
		scope: string;
		nextAction: string;
	};
	source: {
		eventId: string;
		command: "/state";
	};
}

export interface RckEventPayload extends RckStorageBase {
	artifactType: "rck.event";
	eventId: string;
	eventType: "StatePackCreated";
	payload: {
		stateId: string;
		statePath: string;
		stateEventId: string;
	};
}

export interface LatestStateIndexPayload {
	schemaVersion: "0.1";
	artifactType: "rck.index.latest-state";
	currentStateId: string;
	currentStatePath: string;
	currentEventId: string;
	currentEventPath: string;
	traceId: string;
	updatedAt: string;
	updatedByEventId: string;
}

export function getRckRoot(cwd: string): string {
	return join(cwd, ".pi", "rck");
}

export function ensureRckStorage(root: string): void {
	for (const dir of ["events", "states", "context-packs", "indexes"]) {
		mkdirSync(join(root, dir), { recursive: true });
	}
}

export function safeFileTimestamp(date: Date): string {
	const year = String(date.getUTCFullYear()).padStart(4, "0");
	const month = String(date.getUTCMonth() + 1).padStart(2, "0");
	const day = String(date.getUTCDate()).padStart(2, "0");
	const hours = String(date.getUTCHours()).padStart(2, "0");
	const minutes = String(date.getUTCMinutes()).padStart(2, "0");
	const seconds = String(date.getUTCSeconds()).padStart(2, "0");
	const millis = String(date.getUTCMilliseconds()).padStart(3, "0");
	return `${year}${month}${day}T${hours}${minutes}${seconds}${millis}Z`;
}

export function writeJsonAtomic(filePath: string, data: unknown): void {
	const dir = dirname(filePath);
	mkdirSync(dir, { recursive: true });

	const tempPath = join(dir, `.${basename(filePath)}.${process.pid}.${randomUUID().replace(/-/g, "")}.tmp`);
	writeFileSync(tempPath, `${JSON.stringify(data, null, 2)}\n`, "utf-8");
	rmSync(filePath, { force: true });
	renameSync(tempPath, filePath);
}

function toArtifactPath(cwd: string, absolutePath: string): string {
	return relative(cwd, absolutePath).split(sep).join("/");
}

function buildArtifactRef(kind: RckArtifactRef["kind"], id: string, createdAt: string, cwd: string, absolutePath: string): RckArtifactRef {
	return {
		kind,
		id,
		createdAt,
		path: toArtifactPath(cwd, absolutePath),
	};
}

function makeArtifactFileName(timestamp: string, id: string): string {
	return `${timestamp}_${id}.json`;
}

function artifactTimestamp(createdAt: string): string {
	return safeFileTimestamp(new Date(createdAt));
}

export function writeRckState(root: string, state: RckStatePayload): RckArtifactRef {
	const filePath = join(root, "states", makeArtifactFileName(artifactTimestamp(state.createdAt), state.stateId));
	writeJsonAtomic(filePath, state);
	return buildArtifactRef("file", state.stateId, state.createdAt, state.cwd, filePath);
}

export function writeRckEvent(root: string, event: RckEventPayload): RckArtifactRef {
	const filePath = join(root, "events", makeArtifactFileName(artifactTimestamp(event.createdAt), event.eventId));
	writeJsonAtomic(filePath, event);
	return buildArtifactRef("file", event.eventId, event.createdAt, event.cwd, filePath);
}

export function updateLatestStateIndex(root: string, stateRef: RckArtifactRef, eventRef: RckArtifactRef, traceId: string): void {
	const indexPath = join(root, "indexes", "latest-state.json");
	const updatedAt = new Date().toISOString();
	const payload: LatestStateIndexPayload = {
		schemaVersion: "0.1",
		artifactType: "rck.index.latest-state",
		currentStateId: stateRef.id,
		currentStatePath: stateRef.path,
		currentEventId: eventRef.id,
		currentEventPath: eventRef.path,
		traceId,
		updatedAt,
		updatedByEventId: eventRef.id,
	};
	writeJsonAtomic(indexPath, payload);
}

export function createStateId(): string {
	return `state_${randomUUID().replace(/-/g, "")}`;
}

export function createEventId(): string {
	return `evt_${randomUUID().replace(/-/g, "")}`;
}
