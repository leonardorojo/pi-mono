import { spawn } from "node:child_process";

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

export interface HermesRealRunnerOptions {
	cwd?: string;
	env?: NodeJS.ProcessEnv;
	command?: string;
	defaultTimeoutMs?: number;
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

function buildRunnerFailure(error: unknown): {
	blockedReason: string;
	exitCode: number;
	status: HermesRunStatus;
	stderr: string;
} {
	const errorRecord = error as { code?: string; message?: string } | undefined;
	const code = errorRecord?.code;
	const message = error instanceof Error ? error.message : String(error);

	if (code === "ENOENT") {
		return {
			blockedReason: "hermes-not-found",
			exitCode: 127,
			status: "aborted",
			stderr: message,
		};
	}

	return {
		blockedReason: "spawn-error",
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

	if (result.blockedReason) {
		pieces.push(`blockedReason=${result.blockedReason}`);
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
		const startedAt = Date.now();
		const stdoutChunks: string[] = [];
		const stderrChunks: string[] = [];

		return new Promise<HermesExecResult>((resolve, reject) => {
			const child = spawn(command, ["-z", request.prompt], {
				cwd: options.cwd,
				env: options.env,
				stdio: ["ignore", "pipe", "pipe"],
			});

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

			const fail = (error: unknown): void => {
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
				reject(error);
			};

			child.stdout?.setEncoding("utf8");
			child.stdout?.on("data", (chunk: string) => {
				stdoutChunks.push(chunk);
			});

			child.stderr?.setEncoding("utf8");
			child.stderr?.on("data", (chunk: string) => {
				stderrChunks.push(chunk);
			});

			child.once("error", fail);
			child.once("close", (code, signal) => {
				const exitCode = timedOut
					? 124
					: typeof code === "number"
						? code
						: signalToExitCode(signal);

				settle({
					exitCode,
					timedOut,
					stdout: stdoutChunks.join(""),
					stderr: stderrChunks.join(""),
					durationMs: Date.now() - startedAt,
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
		const result: HermesRunResult = {
			request,
			mode: request.mode,
			status: mapHermesStatus(execution.exitCode, execution.timedOut),
			exitCode: execution.exitCode,
			timedOut: execution.timedOut,
			durationMs,
			stdout: execution.stdout,
			stderr: execution.stderr,
			blockedReason: execution.timedOut ? "timeout" : undefined,
			safeSummary: "",
		};

		result.safeSummary = createSafeHermesSummary(result);
		return result;
	} catch (error) {
		const failure = buildRunnerFailure(error);
		const durationMs = Date.now() - startedAt;
		const result: HermesRunResult = {
			request,
			mode: request.mode,
			status: failure.status,
			exitCode: failure.exitCode,
			timedOut: false,
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
