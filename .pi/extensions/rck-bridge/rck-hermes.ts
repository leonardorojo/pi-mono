import { spawn } from "node:child_process";

export type HermesRunMode = "fake" | "real";

export type HermesRunStatus = "succeeded" | "failed" | "timed_out" | "aborted";
export type HermesErrorKind =
	| "real_disabled"
	| "hermes_not_found"
	| "spawn_error"
	| "timeout"
	| "non_zero_exit"
	| "output_truncated";

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
	status?: HermesRunStatus;
	errorKind?: HermesErrorKind;
	blockedReason?: string;
	stdoutTruncated?: boolean;
	stderrTruncated?: boolean;
	stdoutByteLength?: number;
	stderrByteLength?: number;
}

export type HermesExecRunner =
	(request: HermesRunRequest) => Promise<HermesExecResult> | HermesExecResult;

export interface HermesRunResult {
	request: HermesRunRequest;
	mode: HermesRunMode;
	status: HermesRunStatus;
	exitCode: number;
	timedOut: boolean;
	errorKind?: HermesErrorKind;
	blockedReason?: string;
	durationMs?: number;
	stdout?: string;
	stderr?: string;
	stdoutTruncated?: boolean;
	stderrTruncated?: boolean;
	stdoutByteLength?: number;
	stderrByteLength?: number;
	safeSummary: string;
}

export interface RunHermesExecutionOptions {
	allowRealExecution?: boolean;
}

export interface HermesRealRunnerOptions {
	cwd?: string;
	env?: NodeJS.ProcessEnv;
	command?: string;
	defaultTimeoutMs?: number;
	maxOutputBytes?: number;
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

function normalizeRealHermesGate(value: unknown): boolean {
	if (typeof value !== "string") {
		return false;
	}

	return ["1", "true", "yes", "on"].includes(value.trim().toLowerCase());
}

function signalToExitCode(signal: NodeJS.Signals | string | null | undefined): number {
	switch (signal) {
		case "SIGINT":
			return 130;
		case "SIGTERM":
			return 143;
		case "SIGKILL":
			return 137;
		case "SIGQUIT":
			return 131;
		default:
			return 1;
	}
}

function createLimitedOutputCollector(maxBytes: number): {
	push: (chunk: Buffer | string) => void;
	finish: () => { text: string; byteLength: number; truncated: boolean };
} {
	const limit = Number.isFinite(maxBytes) && maxBytes >= 0 ? Math.floor(maxBytes) : 0;
	const chunks: Buffer[] = [];
	let storedBytes = 0;
	let totalBytes = 0;
	let truncated = false;

	return {
		push(chunk: Buffer | string) {
			const buffer = Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk);
			totalBytes += buffer.length;
			if (storedBytes >= limit) {
				truncated = true;
				return;
			}

			const remaining = limit - storedBytes;
			if (buffer.length > remaining) {
				chunks.push(buffer.subarray(0, remaining));
				storedBytes += remaining;
				truncated = true;
				return;
			}

			chunks.push(buffer);
			storedBytes += buffer.length;
		},
		finish() {
			return {
				text: chunks.length > 0 ? Buffer.concat(chunks).toString("utf8") : "",
				byteLength: totalBytes,
				truncated,
			};
		},
	};
}

function normalizeRunnerError(error: unknown): {
	errorKind: HermesErrorKind;
	exitCode: number;
	blockedReason?: string;
	status: HermesRunStatus;
	stderr: string;
} {
	const errorRecord = error as { code?: string; message?: string } | undefined;
	const code = errorRecord?.code;
	const message = error instanceof Error ? error.message : String(error);

	if (code === "ENOENT") {
		return {
			errorKind: "hermes_not_found",
			exitCode: 127,
			status: "aborted",
			blockedReason: "hermes-not-found",
			stderr: message,
		};
	}

	return {
		errorKind: "spawn_error",
		exitCode: 1,
		status: "failed",
		stderr: message,
	};
}



export function getAllowRealHermesFromEnv(env: NodeJS.ProcessEnv = process.env): boolean {
	return normalizeRealHermesGate(env.RCK_BRIDGE_ALLOW_REAL_HERMES);
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

	if (result.errorKind) {
		pieces.push(`errorKind=${result.errorKind}`);
	}

	if (result.blockedReason) {
		pieces.push(`blockedReason=${result.blockedReason}`);
	}

	if (typeof result.stdoutByteLength === "number") {
		pieces.push(`stdoutByteLength=${result.stdoutByteLength}`);
	}

	if (typeof result.stderrByteLength === "number") {
		pieces.push(`stderrByteLength=${result.stderrByteLength}`);
	}

	if (result.stdoutTruncated) {
		pieces.push("stdoutTruncated=true");
	}

	if (result.stderrTruncated) {
		pieces.push("stderrTruncated=true");
	}

	if (promptPreview.length > 0) {
		pieces.push(`prompt="${promptPreview}"`);
	}

	return `Hermes run: ${pieces.join(" | ")}`;
}

export function createHermesRealRunner(options: HermesRealRunnerOptions = {}): HermesExecRunner {
	return async (request: HermesRunRequest): Promise<HermesExecResult> => {
		const command = options.command ?? "hermes";
		const timeoutMs = request.timeoutMs ?? options.defaultTimeoutMs ?? 60000;
		const maxOutputBytes = options.maxOutputBytes ?? 8192;
		const startedAt = Date.now();
		const stdoutCollector = createLimitedOutputCollector(maxOutputBytes);
		const stderrCollector = createLimitedOutputCollector(maxOutputBytes);

		return new Promise<HermesExecResult>((resolve) => {
			let child: ReturnType<typeof spawn> | undefined;
			try {
				child = spawn(command, ["-z", request.prompt], {
					cwd: options.cwd,
					env: options.env,
					stdio: ["ignore", "pipe", "pipe"],
				});
			} catch (error) {
				const normalized = normalizeRunnerError(error);
				resolve({
					exitCode: normalized.exitCode,
					timedOut: false,
					stdout: "",
					stderr: normalized.stderr,
					durationMs: Date.now() - startedAt,
					status: normalized.status,
					errorKind: normalized.errorKind,
					blockedReason: normalized.blockedReason,
					stdoutTruncated: false,
					stderrTruncated: false,
					stdoutByteLength: 0,
					stderrByteLength: Buffer.byteLength(normalized.stderr, "utf8"),
				});
				return;
			}

			let settled = false;
			let timedOut = false;
			let timeoutHandle: NodeJS.Timeout | undefined;
			let timeoutKillHandle: NodeJS.Timeout | undefined;

			const settle = (result: HermesExecResult): void => {
				if (settled) {
					return;
				}

				settled = true;
				if (timeoutHandle) {
					clearTimeout(timeoutHandle);
				}
				if (timeoutKillHandle) {
					clearTimeout(timeoutKillHandle);
				}
				resolve(result);
			};

			child.stdout?.on("data", (chunk: Buffer) => {
				stdoutCollector.push(chunk);
			});

			child.stderr?.on("data", (chunk: Buffer) => {
				stderrCollector.push(chunk);
			});

			child.once("error", (error) => {
				const normalized = normalizeRunnerError(error);
				settle({
					exitCode: normalized.exitCode,
					timedOut: false,
					stdout: "",
					stderr: normalized.stderr,
					durationMs: Date.now() - startedAt,
					status: normalized.status,
					errorKind: normalized.errorKind,
					blockedReason: normalized.blockedReason,
					stdoutTruncated: false,
					stderrTruncated: false,
					stdoutByteLength: 0,
					stderrByteLength: Buffer.byteLength(normalized.stderr, "utf8"),
				});
			});

			child.once("close", (code, signal) => {
				const stdout = stdoutCollector.finish();
				const stderr = stderrCollector.finish();
				const exitCode = timedOut
					? 124
					: typeof code === "number"
						? code
						: signalToExitCode(signal);
				const status = timedOut ? "timed_out" : mapHermesStatus(exitCode, false);
				const errorKind = timedOut
					? "timeout"
					: exitCode === 0
						? (stdout.truncated || stderr.truncated ? "output_truncated" : undefined)
						: exitCode === 127
							? "hermes_not_found"
							: "non_zero_exit";

				settle({
					exitCode,
					timedOut,
					stdout: stdout.text,
					stderr: stderr.text,
					durationMs: Date.now() - startedAt,
					status,
					errorKind,
					stdoutTruncated: stdout.truncated,
					stderrTruncated: stderr.truncated,
					stdoutByteLength: stdout.byteLength,
					stderrByteLength: stderr.byteLength,
				});
			});

			if (typeof timeoutMs === "number" && timeoutMs > 0) {
				timeoutHandle = setTimeout(() => {
					if (settled) {
						return;
					}

					timedOut = true;
					child.kill("SIGTERM");
					timeoutKillHandle = setTimeout(() => {
						if (settled) {
							return;
						}

						child.kill("SIGKILL");
					}, 500);
				}, timeoutMs);
			}
		});
	};
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
			errorKind: "real_disabled",
			blockedReason: "real-mode-disabled",
			safeSummary: "",
		};

		blocked.safeSummary = createSafeHermesSummary(blocked);
		return blocked;
	}

	const startedAt = Date.now();

	try {
		const execution = await runner(request);
		const durationMs = typeof execution.durationMs === "number" ? execution.durationMs : Date.now() - startedAt;
		const status = execution.status ?? mapHermesStatus(execution.exitCode, execution.timedOut);
		const outputTruncated = Boolean(execution.stdoutTruncated || execution.stderrTruncated);
		const errorKind =
			execution.errorKind
				?? (execution.timedOut
					? "timeout"
					: status === "failed" && execution.exitCode !== 0
						? (execution.exitCode === 127 ? "hermes_not_found" : "non_zero_exit")
						: outputTruncated
							? "output_truncated"
							: undefined);
		const result: HermesRunResult = {
			request,
			mode: request.mode,
			status,
			exitCode: execution.exitCode,
			timedOut: execution.timedOut,
			errorKind,
			durationMs,
			stdout: execution.stdout,
			stderr: execution.stderr,
			stdoutTruncated: execution.stdoutTruncated,
			stderrTruncated: execution.stderrTruncated,
			stdoutByteLength: execution.stdoutByteLength,
			stderrByteLength: execution.stderrByteLength,
			blockedReason: execution.blockedReason,
			safeSummary: "",
		};

		result.safeSummary = createSafeHermesSummary(result);
		return result;
	} catch (error) {
		const failure = normalizeRunnerError(error);
		const durationMs = Date.now() - startedAt;
		const result: HermesRunResult = {
			request,
			mode: request.mode,
			status: failure.status,
			exitCode: failure.exitCode,
			timedOut: false,
			errorKind: failure.errorKind,
			durationMs,
			stdout: "",
			stderr: failure.stderr,
			blockedReason: failure.blockedReason,
			safeSummary: "",
		};

		result.safeSummary = createSafeHermesSummary(result);
		return result;
	}
}
