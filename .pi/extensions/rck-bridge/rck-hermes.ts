export type HermesRunMode = "fake" | "real";

export type HermesRunStatus = "succeeded" | "failed" | "timed_out" | "aborted";

export interface HermesRunRequest {
	prompt: string;
	mode: HermesRunMode;
	timeoutMs?: number;
	rawArgs: string;
}

export interface HermesExecResult {
	exitCode: number;
	timedOut: boolean;
	stdout: string;
	stderr: string;
	durationMs?: number;
}

export type HermesExecRunner =
	(request: HermesRunRequest) => Promise<HermesExecResult> | HermesExecResult;

export interface HermesRunResult {
	request: HermesRunRequest;
	mode: HermesRunMode;
	status: HermesRunStatus;
	exitCode: number;
	timedOut: boolean;
	durationMs?: number;
	stdout?: string;
	stderr?: string;
	blockedReason?: string;
	safeSummary: string;
}

export interface RunHermesExecutionOptions {
	allowRealExecution?: boolean;
}

function normalizeWhitespace(input: string): string {
	return input.replace(/\s+/g, " ").trim();
}

function parseTimeoutValue(value: string): number | undefined {
	if (!value) {
		return undefined;
	}

	const secondsMatch = value.match(/^(\d+(?:\.\d+)?)s$/i);
	if (secondsMatch) {
		const seconds = Number(secondsMatch[1]);
		return Number.isFinite(seconds) ? Math.round(seconds * 1000) : undefined;
	}

	const numeric = Number(value);
	if (!Number.isFinite(numeric) || numeric < 0) {
		return undefined;
	}

	return Math.round(numeric);
}

export function parseHermesArgs(args: string): HermesRunRequest {
	const rawArgs = args ?? "";
	const tokens = rawArgs.trim().length === 0 ? [] : rawArgs.trim().split(/\s+/);
	const promptTokens: string[] = [];
	let mode: HermesRunMode = "fake";
	let timeoutMs: number | undefined;

	for (let index = 0; index < tokens.length; index += 1) {
		const token = tokens[index];

		if (token === "--mode") {
			const next = tokens[index + 1];
			if (next === "real" || next === "fake") {
				mode = next;
				index += 1;
				continue;
			}
			continue;
		}

		if (token === "--timeout") {
			const next = tokens[index + 1];
			const parsed = next ? parseTimeoutValue(next) : undefined;
			if (parsed !== undefined) {
				timeoutMs = parsed;
				index += 1;
			}
			continue;
		}

		promptTokens.push(token);
	}

	return {
		prompt: normalizeWhitespace(promptTokens.join(" ")),
		mode,
		timeoutMs,
		rawArgs,
	};
}

export function mapHermesStatus(exitCode: number, timedOut: boolean): HermesRunStatus {
	if (timedOut) {
		return "timed_out";
	}

	if (exitCode === 0) {
		return "succeeded";
	}

	if (exitCode === 130 || exitCode === 143) {
		return "aborted";
	}

	return "failed";
}

function sanitizePrompt(prompt: string): string {
	const normalized = normalizeWhitespace(prompt);
	if (normalized.length <= 120) {
		return normalized;
	}

	return `${normalized.slice(0, 117)}...`;
}

export function createSafeHermesSummary(result: Omit<HermesRunResult, "safeSummary">): string {
	const promptPreview = sanitizePrompt(result.request.prompt);
	const pieces = [
		`mode=${result.mode}`,
		`status=${result.status}`,
		`exitCode=${result.exitCode}`,
		`timedOut=${result.timedOut}`,
	];

	if (typeof result.durationMs === "number") {
		pieces.push(`durationMs=${result.durationMs}`);
	}

	if (result.blockedReason) {
		pieces.push(`blockedReason=${result.blockedReason}`);
	}

	if (promptPreview.length > 0) {
		pieces.push(`prompt="${promptPreview}"`);
	}

	return `Hermes run: ${pieces.join(" | ")}`;
}

export async function runHermesExecution(
	request: HermesRunRequest,
	runner: HermesExecRunner,
	options: RunHermesExecutionOptions = {},
): Promise<HermesRunResult> {
	if (request.mode === "real" && options.allowRealExecution !== true) {
		const blocked: HermesRunResult = {
			request,
			mode: request.mode,
			status: "aborted",
			exitCode: 0,
			timedOut: false,
			blockedReason: "real-mode-disabled",
			safeSummary: "",
		};

		blocked.safeSummary = createSafeHermesSummary(blocked);
		return blocked;
	}

	const startedAt = Date.now();
	const execution = await runner(request);
	const durationMs = typeof execution.durationMs === "number" ? execution.durationMs : Date.now() - startedAt;
	const status = mapHermesStatus(execution.exitCode, execution.timedOut);
	const result: HermesRunResult = {
		request,
		mode: request.mode,
		status,
		exitCode: execution.exitCode,
		timedOut: execution.timedOut,
		durationMs,
		stdout: execution.stdout,
		stderr: execution.stderr,
		safeSummary: "",
	};

	result.safeSummary = createSafeHermesSummary(result);
	return result;
}
