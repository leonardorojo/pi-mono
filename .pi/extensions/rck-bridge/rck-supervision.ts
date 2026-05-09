import type { CurrentTraceIndexPayload } from "./rck-storage.js";

export type RckSupervisionLevel = "ok" | "info" | "warning" | "error" | "blocking";

export interface RckSupervisionHermesInput {
	eventId?: string;
	runId?: string;
	traceId?: string;
	mode?: string;
	status?: string;
	errorKind?: string;
	timedOut?: boolean;
	durationMs?: number;
	stdoutByteLength?: number;
	stderrByteLength?: number;
	stdoutTruncated?: boolean;
	stderrTruncated?: boolean;
	blockedReason?: string;
	safeSummary?: string;
}

export interface RckSupervisionInput {
	latestHermes?: RckSupervisionHermesInput;
	currentTrace?: Pick<CurrentTraceIndexPayload, "traceId"> | undefined;
	latestEventId?: string;
}

export interface RckSupervisionSignals {
	status?: string;
	errorKind?: string;
	timedOut?: boolean;
	durationMs?: number;
	stdoutTruncated?: boolean;
	stderrTruncated?: boolean;
	stdoutByteLength?: number;
	stderrByteLength?: number;
	blockedReason?: string;
}

export interface RckSupervisionEvaluation {
	level: RckSupervisionLevel;
	reason: string;
	recommendedAction: string;
	needsAttention: boolean;
	traceId?: string;
	latestRunId?: string;
	latestEventId?: string;
	signals: RckSupervisionSignals;
}

function collectSignals(latestHermes: RckSupervisionHermesInput): RckSupervisionSignals {
	return {
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
}

function hasOutputTruncation(latestHermes: RckSupervisionHermesInput): boolean {
	return Boolean(latestHermes.stdoutTruncated || latestHermes.stderrTruncated);
}

export function evaluateRckSupervision(input: RckSupervisionInput): RckSupervisionEvaluation {
	const traceId = input.currentTrace?.traceId ?? input.latestHermes?.traceId;
	const latestEventId = input.latestEventId ?? input.latestHermes?.eventId;

	if (!input.latestHermes) {
		return {
			level: "info",
			reason: "No Hermes run recorded yet",
			recommendedAction: "No action needed",
			needsAttention: false,
			traceId,
			latestEventId,
			signals: {},
		};
	}

	const latestRunId = input.latestHermes.runId ?? input.latestHermes.eventId;
	const signals = collectSignals(input.latestHermes);

	if (input.latestHermes.timedOut) {
		return {
			level: "blocking",
			reason: "Latest Hermes run timed out",
			recommendedAction: "Request checkpoint or stop/retry manually",
			needsAttention: true,
			traceId,
			latestRunId,
			latestEventId,
			signals,
		};
	}

	if (input.latestHermes.errorKind === "hermes_not_found" || input.latestHermes.errorKind === "spawn_error") {
		return {
			level: "error",
			reason: "Hermes environment could not be started",
			recommendedAction: "Fix Hermes environment before retrying",
			needsAttention: true,
			traceId,
			latestRunId,
			latestEventId,
			signals,
		};
	}

	if (hasOutputTruncation(input.latestHermes)) {
		return {
			level: "warning",
			reason: "Hermes output was truncated",
			recommendedAction: "Request partial summary or inspect evidence manually",
			needsAttention: true,
			traceId,
			latestRunId,
			latestEventId,
			signals,
		};
	}

	if (input.latestHermes.errorKind === "non_zero_exit" || input.latestHermes.status === "failed") {
		return {
			level: "warning",
			reason: "Latest Hermes run failed",
			recommendedAction: "Review failure summary and evidence refs",
			needsAttention: true,
			traceId,
			latestRunId,
			latestEventId,
			signals,
		};
	}

	if ((input.latestHermes.durationMs ?? 0) > 60000) {
		return {
			level: "warning",
			reason: "Latest Hermes run is long-running",
			recommendedAction: "Consider checkpoint if the run continues too long",
			needsAttention: true,
			traceId,
			latestRunId,
			latestEventId,
			signals,
		};
	}

	if (input.latestHermes.blockedReason === "real-mode-disabled") {
		return {
			level: "info",
			reason: "Real Hermes execution is disabled",
			recommendedAction: "Enable RCK_BRIDGE_ALLOW_REAL_HERMES=1 only if real execution is intended",
			needsAttention: false,
			traceId,
			latestRunId,
			latestEventId,
			signals,
		};
	}

	if (input.latestHermes.status === "succeeded") {
		return {
			level: "ok",
			reason: "Latest Hermes run succeeded without supervision flags",
			recommendedAction: "No action needed",
			needsAttention: false,
			traceId,
			latestRunId,
			latestEventId,
			signals,
		};
	}

	return {
		level: "info",
		reason: "Latest Hermes run did not require attention",
		recommendedAction: "No action needed",
		needsAttention: false,
		traceId,
		latestRunId,
		latestEventId,
		signals,
	};
}

function formatSignalFlags(signals: RckSupervisionSignals): string {
	const pieces = [
		signals.status ? `status=${signals.status}` : null,
		signals.errorKind ? `errorKind=${signals.errorKind}` : null,
		typeof signals.timedOut === "boolean" ? `timedOut=${signals.timedOut ? "yes" : "no"}` : null,
		typeof signals.durationMs === "number" ? `durationMs=${signals.durationMs}` : null,
		typeof signals.stdoutTruncated === "boolean" ? `stdoutTruncated=${signals.stdoutTruncated ? "yes" : "no"}` : null,
		typeof signals.stderrTruncated === "boolean" ? `stderrTruncated=${signals.stderrTruncated ? "yes" : "no"}` : null,
		typeof signals.stdoutByteLength === "number" ? `stdoutBytes=${signals.stdoutByteLength}` : null,
		typeof signals.stderrByteLength === "number" ? `stderrBytes=${signals.stderrByteLength}` : null,
		signals.blockedReason ? `blockedReason=${signals.blockedReason}` : null,
	].filter((value): value is string => Boolean(value));

	return pieces.length > 0 ? pieces.join(" ") : "none";
}

export function formatRckSupervisionLines(evaluation: RckSupervisionEvaluation): string[] {
	return [
		"RCK supervise",
		`- level: ${evaluation.level}`,
		`- needs attention: ${evaluation.needsAttention ? "yes" : "no"}`,
		`- reason: ${evaluation.reason}`,
		`- recommended action: ${evaluation.recommendedAction}`,
		`- trace: ${evaluation.traceId ?? "missing"}`,
		`- latest run: ${evaluation.latestRunId ?? "missing"}`,
		`- latest event: ${evaluation.latestEventId ?? "missing"}`,
		`- signals: ${formatSignalFlags(evaluation.signals)}`,
	];
}
