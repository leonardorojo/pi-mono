import { randomUUID } from "node:crypto";
import { mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { basename, dirname, join, relative, sep } from "node:path";
import type { HermesRunMode, HermesRunStatus } from "./rck-hermes.js";

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

export interface HermesEvidenceRef {
	artifactId: string;
	kind: "stdout" | "stderr";
	path: string;
	byteLength: number;
}

export interface HermesEvidenceWriteResult {
	stdoutRef?: HermesEvidenceRef;
	stderrRef?: HermesEvidenceRef;
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
	eventType:
		| "StatePackCreated"
		| "ContextPackInjected"
		| "AnchorRegistered"
		| "HermesRunRequested"
		| "HermesRunRecorded";
	command?: {
		name: string;
		args?: string;
	};
	requestEventId?: string;
	resultSummary?: string;
	exitCode?: number;
	mode?: HermesRunMode;
	status?: HermesRunStatus;
	timedOut?: boolean;
	durationMs?: number;
	blockedReason?: string;
	stdout?: string;
	stderr?: string;
	stdoutRef?: HermesEvidenceRef;
	stderrRef?: HermesEvidenceRef;
	safeSummary?: string;
	payload: {
		stateId?: string;
		statePath?: string;
		stateEventId?: string;
		contextPackId?: string;
		contextPackPath?: string;
		contextEventId?: string;
		anchorId?: string;
		anchorPath?: string;
		anchorEventId?: string;
		runId?: string;
		requestEventId?: string;
		mode?: HermesRunMode;
		promptSummary?: string;
		timeoutMs?: number;
		status?: HermesRunStatus;
		exitCode?: number;
		timedOut?: boolean;
		durationMs?: number;
		blockedReason?: string;
		stdoutRef?: HermesEvidenceRef;
		stderrRef?: HermesEvidenceRef;
		safeSummary?: string;
	};
}

export interface RckContextPackPayload extends RckStorageBase {
	artifactType: "rck.context-pack";
	contextPackId: string;
	contextPackType: "safe-summary";
	stateId: string;
	statePath: string;
	allowedToInject: true;
	stateSummary: RckStatePayload["stateSummary"];
	contextSummary: RckStatePayload["stateSummary"];
}

export interface RckAnchorPayload extends RckStorageBase {
	artifactType: "rck.anchor";
	anchorId: string;
	anchorName: string;
	stateId?: string;
	statePath?: string;
}

export interface LatestAnchorIndexPayload {
	schemaVersion: "0.1";
	artifactType: "rck.index.latest-anchor";
	currentAnchorId: string;
	currentAnchorPath: string;
	currentEventId: string;
	currentEventPath: string;
	traceId: string;
	updatedAt: string;
	updatedByEventId: string;
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

export interface LatestContextPackIndexPayload {
	schemaVersion: "0.1";
	artifactType: "rck.index.latest-context-pack";
	currentContextPackId: string;
	currentContextPackPath: string;
	currentEventId: string;
	currentEventPath: string;
	stateId: string;
	statePath: string;
	traceId: string;
	updatedAt: string;
	updatedByEventId: string;
}

export function getRckRoot(cwd: string): string {
	return join(cwd, ".pi", "rck");
}

export function ensureRckStorage(root: string): void {
	for (const dir of ["events", "states", "context-packs", "anchors", "indexes"]) {
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

export function writeTextAtomic(filePath: string, data: string): void {
	const dir = dirname(filePath);
	mkdirSync(dir, { recursive: true });

	const tempPath = join(dir, `.${basename(filePath)}.${process.pid}.${randomUUID().replace(/-/g, "")}.tmp`);
	writeFileSync(tempPath, `${data}`, "utf-8");
	rmSync(filePath, { force: true });
	renameSync(tempPath, filePath);
}

export function writeHermesEvidence(
	root: string,
	runId: string,
	recordedAt: string,
	stdout: string,
	stderr: string,
): HermesEvidenceWriteResult {
	const cwd = dirname(dirname(root));
	const timestamp = safeFileTimestamp(new Date(recordedAt));
	const result: HermesEvidenceWriteResult = {};

	const writeEvidence = (kind: "stdout" | "stderr", content: string): HermesEvidenceRef | undefined => {
		if (content.length === 0) {
			return undefined;
		}

		const dir = join(root, "evidence", "hermes", kind);
		mkdirSync(dir, { recursive: true });
		const artifactId = `${timestamp}_hermes_${runId}_${kind}`;
		const filePath = join(dir, `${artifactId}.log`);
		writeTextAtomic(filePath, content);
		return {
			artifactId,
			kind,
			path: toArtifactPath(cwd, filePath),
			byteLength: Buffer.byteLength(content, "utf8"),
		};
	};

	result.stdoutRef = writeEvidence("stdout", stdout);
	result.stderrRef = writeEvidence("stderr", stderr);
	return result;
}

export function readJson<T>(filePath: string): T | undefined {
	try {
		return JSON.parse(readFileSync(filePath, "utf-8")) as T;
	} catch {
		return undefined;
	}
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
	const filePath = join(root, "events", makeArtifactFileName(artifactTimestamp(event.createdAt), event.id));
	writeJsonAtomic(filePath, event);
	return buildArtifactRef("file", event.id, event.createdAt, event.cwd, filePath);
}

export function writeRckContextPack(root: string, pack: RckContextPackPayload): RckArtifactRef {
	const filePath = join(root, "context-packs", makeArtifactFileName(artifactTimestamp(pack.createdAt), pack.contextPackId));
	writeJsonAtomic(filePath, pack);
	return buildArtifactRef("file", pack.contextPackId, pack.createdAt, pack.cwd, filePath);
}

export function writeRckAnchor(root: string, anchor: RckAnchorPayload): RckArtifactRef {
	const filePath = join(root, "anchors", makeArtifactFileName(artifactTimestamp(anchor.createdAt), anchor.anchorId));
	writeJsonAtomic(filePath, anchor);
	return buildArtifactRef("file", anchor.anchorId, anchor.createdAt, anchor.cwd, filePath);
}

export function updateLatestAnchorIndex(
	root: string,
	anchorRef: RckArtifactRef,
	eventRef: RckArtifactRef,
	traceId: string,
): void {
	const indexPath = join(root, "indexes", "latest-anchor.json");
	const updatedAt = new Date().toISOString();
	const payload: LatestAnchorIndexPayload = {
		schemaVersion: "0.1",
		artifactType: "rck.index.latest-anchor",
		currentAnchorId: anchorRef.id,
		currentAnchorPath: anchorRef.path,
		currentEventId: eventRef.id,
		currentEventPath: eventRef.path,
		traceId,
		updatedAt,
		updatedByEventId: eventRef.id,
	};
	writeJsonAtomic(indexPath, payload);
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

export function updateLatestContextPackIndex(
	root: string,
	packRef: RckArtifactRef,
	eventRef: RckArtifactRef,
	traceId: string,
	stateId: string,
	statePath: string,
): void {
	const indexPath = join(root, "indexes", "latest-context-pack.json");
	const updatedAt = new Date().toISOString();
	const payload: LatestContextPackIndexPayload = {
		schemaVersion: "0.1",
		artifactType: "rck.index.latest-context-pack",
		currentContextPackId: packRef.id,
		currentContextPackPath: packRef.path,
		currentEventId: eventRef.id,
		currentEventPath: eventRef.path,
		stateId,
		statePath,
		traceId,
		updatedAt,
		updatedByEventId: eventRef.id,
	};
	writeJsonAtomic(indexPath, payload);
}

export function readLatestRckState(root: string): RckStatePayload | undefined {
	const index = readJson<LatestStateIndexPayload>(join(root, "indexes", "latest-state.json"));
	if (!index) return undefined;
	const repoRoot = dirname(dirname(root));
	return readJson<RckStatePayload>(join(repoRoot, index.currentStatePath));
}

export function createStateId(): string {
	return `state_${randomUUID().replace(/-/g, "")}`;
}

export function createEventId(): string {
	return `evt_${randomUUID().replace(/-/g, "")}`;
}

export function createContextPackId(): string {
	return `pack_${randomUUID().replace(/-/g, "")}`;
}
