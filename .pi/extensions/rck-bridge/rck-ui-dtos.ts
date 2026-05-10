import type { HermesRunStatus } from "./rck-hermes.js";
import type {
	CurrentTraceIndexPayload,
	HermesEvidenceRef,
	LatestAnchorIndexPayload,
	LatestContextPackIndexPayload,
	LatestStateIndexPayload,
	RckAnchorPayload,
	RckContextPackPayload,
	RckEventPayload,
	RckStatePayload,
} from "./rck-storage.js";
import type { RckSupervisionEvaluation, RckSupervisionSignals } from "./rck-supervision.js";

export type EvidenceDisplayPolicy = "reference-only" | "summary-only" | "hidden";

export interface EvidenceRefDto {
	kind: HermesEvidenceRef["kind"];
	refId: string;
	path: string;
	isRaw: boolean;
	displayPolicy: EvidenceDisplayPolicy;
}

export interface RckCurrentTraceDto {
	traceId: string;
	headAnchorId: string | null;
	anchorCount: number;
	createdAtUtc?: string;
	updatedAtUtc?: string;
}

export interface RckLatestStateDto {
	stateId: string;
	statePath: string;
	eventId: string;
	traceId: string;
	updatedAt: string;
	safeSummary?: string;
}

export interface RckLatestContextPackDto {
	contextPackId: string;
	contextPackPath: string;
	eventId: string;
	stateId: string;
	statePath: string;
	traceId: string;
	updatedAt: string;
	safeSummary?: string;
}

export interface RckLatestAnchorDto {
	anchorId: string;
	anchorPath: string;
	eventId: string;
	traceId: string;
	updatedAt: string;
	label?: string;
	safeSummary?: string;
}

export interface RckEventSummaryDto {
	eventId: string;
	eventType: string;
	traceId: string;
	createdAt: string;
	summary: string;
}

export interface RckStatusDto {
	traceId: string | null;
	currentTrace: RckCurrentTraceDto | null;
	latestState: RckLatestStateDto | null;
	latestContextPack: RckLatestContextPackDto | null;
	latestAnchor: RckLatestAnchorDto | null;
	latestHermesRun: HermesRunResultDto | null;
	generatedAt: string;
}

export interface RckInventoryCountsDto {
	states: number;
	contextPacks: number;
	anchors: number;
	events: number;
	hermesRuns: number;
}

export interface RckInventoryDto {
	traceId: string | null;
	counts: RckInventoryCountsDto;
	latestEvents: RckEventSummaryDto[];
	latestHermesRun: HermesRunResultDto | null;
	generatedAt: string;
}

export interface RckSupervisionDto {
	traceId: string | null;
	level: RckSupervisionEvaluation["level"];
	reason: string;
	recommendedAction: string;
	needsAttention: boolean;
	latestRunId: string | null;
	latestEventId: string | null;
	signals: RckSupervisionSignals;
	generatedAt: string;
}

export interface CreateStateResultDto {
	traceId: string;
	stateId: string;
	eventId: string;
	safeSummary: string;
	generatedAt: string;
}

export interface CreateInjectContextResultDto {
	traceId: string;
	contextPackId: string;
	eventId: string;
	safeSummary: string;
	generatedAt: string;
}

export interface CreateAnchorResultDto {
	traceId: string;
	anchorId: string;
	eventId: string;
	label: string;
	safeSummary: string;
	generatedAt: string;
}

export interface HermesRunResultDto {
	traceId: string;
	runId: string;
	requestedEventId: string | null;
	recordedEventId: string;
	status: HermesRunStatus | string;
	exitCode: number | null;
	durationMs: number | null;
	safeSummary: string;
	evidenceRefs: EvidenceRefDto[];
	generatedAt: string;
}

export type RckAdapterErrorCode =
	| "BRIDGE_UNAVAILABLE"
	| "COMMAND_FAILED"
	| "INVALID_RESPONSE"
	| "SUPERVISION_ATTENTION"
	| "HERMES_REAL_NOT_ALLOWED"
	| "STORAGE_UNAVAILABLE"
	| "UNKNOWN";

export interface RckAdapterErrorDto {
	code: RckAdapterErrorCode;
	message: string;
	source: string;
	command: string;
	recoverable: boolean;
	recommendedAction: string;
	generatedAt: string;
}

export interface CreateStateResultInput {
	traceId: string;
	stateId: string;
	eventId: string;
	safeSummary: string;
	generatedAt?: string;
}

export interface CreateInjectContextResultInput {
	traceId: string;
	contextPackId: string;
	eventId: string;
	safeSummary: string;
	generatedAt?: string;
}

export interface CreateAnchorResultInput {
	traceId: string;
	anchorId: string;
	eventId: string;
	label: string;
	safeSummary: string;
	generatedAt?: string;
}

export interface RckAdapterErrorInput {
	code: RckAdapterErrorCode;
	message: string;
	source: string;
	command: string;
	recoverable: boolean;
	recommendedAction: string;
	generatedAt?: string;
}

export interface NormalizeRckStatusInput {
	currentTrace?: CurrentTraceIndexPayload;
	latestStateIndex?: LatestStateIndexPayload;
	latestState?: RckStatePayload;
	latestContextPackIndex?: LatestContextPackIndexPayload;
	latestContextPack?: RckContextPackPayload;
	latestAnchorIndex?: LatestAnchorIndexPayload;
	latestAnchor?: RckAnchorPayload;
	latestHermesRun?: RckEventPayload;
	generatedAt?: string;
}

export interface NormalizeRckInventoryInput {
	traceId?: string | null;
	counts: {
		states: number;
		contextPacks: number;
		anchors: number;
		events: number;
		hermesRuns?: number;
	};
	latestEvents?: RckEventPayload[];
	latestHermesRun?: RckEventPayload;
	generatedAt?: string;
}

export interface NormalizeRckSupervisionInput {
	evaluation: RckSupervisionEvaluation;
	generatedAt?: string;
}

function isoNow(generatedAt?: string | Date): string {
	if (typeof generatedAt === "string") {
		return generatedAt;
	}
	if (generatedAt instanceof Date) {
		return generatedAt.toISOString();
	}
	return new Date().toISOString();
}

function sanitizeSafeSummary(summary: string): string {
	return summary.replace(/\b(stdout|stderr|diff|log|logs)\b/gi, "[redacted]");
}

function pickTraceId(...values: Array<string | null | undefined>): string | null {
	for (const value of values) {
		if (value) {
			return value;
		}
	}
	return null;
}

export function normalizeEvidenceRefDto(ref: HermesEvidenceRef | undefined): EvidenceRefDto | undefined {
	if (!ref) {
		return undefined;
	}
	return {
		kind: ref.kind,
		refId: ref.artifactId,
		path: ref.path,
		isRaw: false,
		displayPolicy: "reference-only",
	};
}

function normalizeCurrentTraceDto(currentTrace?: CurrentTraceIndexPayload): RckCurrentTraceDto | null {
	if (!currentTrace) {
		return null;
	}
	return {
		traceId: currentTrace.traceId,
		headAnchorId: currentTrace.headAnchorId,
		anchorCount: currentTrace.anchorCount,
		createdAtUtc: currentTrace.createdAtUtc,
		updatedAtUtc: currentTrace.updatedAtUtc,
	};
}

function normalizeLatestStateDto(
	latestStateIndex?: LatestStateIndexPayload,
	latestState?: RckStatePayload,
): RckLatestStateDto | null {
	if (!latestStateIndex && !latestState) {
		return null;
	}
	const stateId = latestState?.stateId ?? latestStateIndex?.currentStateId;
	const statePath = latestStateIndex?.currentStatePath ?? (latestState ? `.pi/rck/states/${latestState.stateId}` : undefined);
	const traceId = pickTraceId(latestState?.traceId, latestStateIndex?.traceId);
	if (!stateId || !statePath || !traceId) {
		return null;
	}
	return {
		stateId,
		statePath,
		eventId: latestState?.source.eventId ?? latestStateIndex.currentEventId,
		traceId,
		updatedAt: latestStateIndex.updatedAt,
		safeSummary: latestState ? sanitizeSafeSummary(`${latestState.stateSummary.title}: ${latestState.stateSummary.objective} | scope=${latestState.stateSummary.scope} | next=${latestState.stateSummary.nextAction}`) : undefined,
	};
}

function normalizeLatestContextPackDto(
	latestContextPackIndex?: LatestContextPackIndexPayload,
	latestContextPack?: RckContextPackPayload,
): RckLatestContextPackDto | null {
	if (!latestContextPackIndex && !latestContextPack) {
		return null;
	}
	const contextPackId = latestContextPack?.contextPackId ?? latestContextPackIndex?.currentContextPackId;
	const contextPackPath = latestContextPackIndex?.currentContextPackPath ?? (latestContextPack ? `.pi/rck/context-packs/${latestContextPack.contextPackId}` : undefined);
	const traceId = pickTraceId(latestContextPack?.traceId, latestContextPackIndex?.traceId);
	const stateId = latestContextPack?.stateId ?? latestContextPackIndex?.stateId;
	const statePath = latestContextPack?.statePath ?? latestContextPackIndex?.statePath;
	if (!contextPackId || !contextPackPath || !traceId || !stateId || !statePath) {
		return null;
	}
	return {
		contextPackId,
		contextPackPath,
		eventId: latestContextPackIndex.currentEventId ?? latestContextPack?.correlation.requestEventId,
		stateId,
		statePath,
		traceId,
		updatedAt: latestContextPackIndex.updatedAt,
		safeSummary: latestContextPack ? sanitizeSafeSummary(`${latestContextPack.summary} | state=${latestContextPack.stateSummary.title}`) : undefined,
	};
}

function normalizeLatestAnchorDto(
	latestAnchorIndex?: LatestAnchorIndexPayload,
	latestAnchor?: RckAnchorPayload,
): RckLatestAnchorDto | null {
	if (!latestAnchorIndex && !latestAnchor) {
		return null;
	}
	const anchorId = latestAnchor?.anchorId ?? latestAnchorIndex?.currentAnchorId;
	const anchorPath = latestAnchorIndex?.currentAnchorPath ?? (latestAnchor ? `.pi/rck/anchors/${latestAnchor.anchorId}` : undefined);
	const traceId = pickTraceId(latestAnchor?.traceId, latestAnchorIndex?.traceId);
	if (!anchorId || !anchorPath || !traceId) {
		return null;
	}
	return {
		anchorId,
		anchorPath,
		eventId: latestAnchorIndex.currentEventId,
		traceId,
		updatedAt: latestAnchorIndex.updatedAt,
		label: latestAnchor?.anchorName,
		safeSummary: latestAnchor ? sanitizeSafeSummary(latestAnchor.summary) : undefined,
	};
}

function normalizeEventSummaryDto(event: RckEventPayload): RckEventSummaryDto {
	return {
		eventId: event.eventId,
		eventType: event.eventType,
		traceId: event.traceId,
		createdAt: event.createdAt,
		summary: sanitizeSafeSummary(event.safeSummary ?? event.resultSummary ?? event.summary),
	};
}

export function normalizeHermesRunResultDto(event: RckEventPayload): HermesRunResultDto {
	const requestEventId = event.requestEventId ?? event.payload?.requestEventId ?? null;
	const runId = event.payload?.runId ?? requestEventId ?? event.eventId;
	const evidenceRefs = [
		normalizeEvidenceRefDto(event.stdoutRef ?? event.payload?.stdoutRef),
		normalizeEvidenceRefDto(event.stderrRef ?? event.payload?.stderrRef),
	].filter((value): value is EvidenceRefDto => value !== undefined);

	return {
		traceId: event.traceId,
		runId,
		requestedEventId: requestEventId,
		recordedEventId: event.eventId,
		status: event.status ?? event.payload?.status ?? "unknown",
		exitCode: event.exitCode ?? event.payload?.exitCode ?? null,
		durationMs: event.durationMs ?? event.payload?.durationMs ?? null,
		safeSummary: sanitizeSafeSummary(event.safeSummary ?? event.resultSummary ?? event.summary),
		evidenceRefs,
		generatedAt: event.createdAt,
	};
}

export function normalizeRckStatusDto(input: NormalizeRckStatusInput): RckStatusDto {
	const traceId = pickTraceId(input.currentTrace?.traceId, input.latestStateIndex?.traceId, input.latestContextPackIndex?.traceId, input.latestAnchorIndex?.traceId, input.latestHermesRun?.traceId);
	const currentTrace = normalizeCurrentTraceDto(input.currentTrace);
	const latestState = normalizeLatestStateDto(input.latestStateIndex, input.latestState);
	const latestContextPack = normalizeLatestContextPackDto(input.latestContextPackIndex, input.latestContextPack);
	const latestAnchor = normalizeLatestAnchorDto(input.latestAnchorIndex, input.latestAnchor);
	const latestHermesRun = input.latestHermesRun ? normalizeHermesRunResultDto(input.latestHermesRun) : null;

	return {
		traceId,
		currentTrace,
		latestState,
		latestContextPack,
		latestAnchor,
		latestHermesRun,
		generatedAt: isoNow(input.generatedAt),
	};
}

export function normalizeRckInventoryDto(input: NormalizeRckInventoryInput): RckInventoryDto {
	const latestEvents = (input.latestEvents ?? []).slice(-5).map((event) => normalizeEventSummaryDto(event));
	const latestHermesRun = input.latestHermesRun ? normalizeHermesRunResultDto(input.latestHermesRun) : null;
	const traceId = pickTraceId(input.traceId, input.latestHermesRun?.traceId, ...latestEvents.map((event) => event.traceId));

	return {
		traceId,
		counts: {
			states: input.counts.states,
			contextPacks: input.counts.contextPacks,
			anchors: input.counts.anchors,
			events: input.counts.events,
			hermesRuns: input.counts.hermesRuns ?? (latestHermesRun ? 1 : 0),
		},
		latestEvents,
		latestHermesRun,
		generatedAt: isoNow(input.generatedAt),
	};
}

export function normalizeRckSupervisionDto(input: NormalizeRckSupervisionInput): RckSupervisionDto {
	return {
		traceId: input.evaluation.traceId ?? null,
		level: input.evaluation.level,
		reason: input.evaluation.reason,
		recommendedAction: input.evaluation.recommendedAction,
		needsAttention: input.evaluation.needsAttention,
		latestRunId: input.evaluation.latestRunId ?? null,
		latestEventId: input.evaluation.latestEventId ?? null,
		signals: input.evaluation.signals,
		generatedAt: isoNow(input.generatedAt),
	};
}

export function createStateResultDto(input: CreateStateResultInput): CreateStateResultDto {
	return {
		traceId: input.traceId,
		stateId: input.stateId,
		eventId: input.eventId,
		safeSummary: sanitizeSafeSummary(input.safeSummary),
		generatedAt: isoNow(input.generatedAt),
	};
}

export function createInjectContextResultDto(input: CreateInjectContextResultInput): CreateInjectContextResultDto {
	return {
		traceId: input.traceId,
		contextPackId: input.contextPackId,
		eventId: input.eventId,
		safeSummary: sanitizeSafeSummary(input.safeSummary),
		generatedAt: isoNow(input.generatedAt),
	};
}

export function createAnchorResultDto(input: CreateAnchorResultInput): CreateAnchorResultDto {
	return {
		traceId: input.traceId,
		anchorId: input.anchorId,
		eventId: input.eventId,
		label: input.label,
		safeSummary: sanitizeSafeSummary(input.safeSummary),
		generatedAt: isoNow(input.generatedAt),
	};
}

export function createRckAdapterErrorDto(input: RckAdapterErrorInput): RckAdapterErrorDto {
	return {
		code: input.code,
		message: input.message,
		source: input.source,
		command: input.command,
		recoverable: input.recoverable,
		recommendedAction: input.recommendedAction,
		generatedAt: isoNow(input.generatedAt),
	};
}
