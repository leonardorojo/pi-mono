import { describe, expect, it, vi } from "vitest";
import {
	createSafeHermesSummary,
	getAllowRealHermesFromEnv,
	mapHermesStatus,
	parseHermesArgs,
	runHermesExecution,
	type HermesExecResult,
	type HermesExecRunner,
} from "./rck-hermes.js";

describe("rck-hermes", () => {
	it("parses default fake prompt", () => {
		const request = parseHermesArgs("hello hermes");

		expect(request).toEqual({
			prompt: "hello hermes",
			mode: "fake",
			rawArgs: "hello hermes",
		});
	});

	it("parses --mode real", () => {
		const request = parseHermesArgs("--mode real investigate bridge");

		expect(request.mode).toBe("real");
		expect(request.prompt).toBe("investigate bridge");
		expect(request.rawArgs).toBe("--mode real investigate bridge");
	});

	it("parses --timeout 15000", () => {
		const request = parseHermesArgs("--timeout 15000 inspect storage");

		expect(request.timeoutMs).toBe(15000);
		expect(request.prompt).toBe("inspect storage");
	});

	it("reads the real Hermes gate from env", () => {
		expect(getAllowRealHermesFromEnv({ RCK_BRIDGE_ALLOW_REAL_HERMES: "1" } as NodeJS.ProcessEnv)).toBe(true);
		expect(getAllowRealHermesFromEnv({ RCK_BRIDGE_ALLOW_REAL_HERMES: "true" } as NodeJS.ProcessEnv)).toBe(true);
		expect(getAllowRealHermesFromEnv({ RCK_BRIDGE_ALLOW_REAL_HERMES: "0" } as NodeJS.ProcessEnv)).toBe(false);
		expect(getAllowRealHermesFromEnv({ RCK_BRIDGE_ALLOW_REAL_HERMES: "false" } as NodeJS.ProcessEnv)).toBe(false);
		expect(getAllowRealHermesFromEnv({} as NodeJS.ProcessEnv)).toBe(false);
	});

	it("maps status success failure timeout and aborted", () => {
		expect(mapHermesStatus(0, false)).toBe("succeeded");
		expect(mapHermesStatus(2, false)).toBe("failed");
		expect(mapHermesStatus(0, true)).toBe("timed_out");
		expect(mapHermesStatus(130, false)).toBe("aborted");
		expect(mapHermesStatus(143, false)).toBe("aborted");
	});

	it("runs fake success and does not leak stdout into safe summary", async () => {
		const runner = vi.fn<HermesExecRunner>().mockResolvedValue({
			exitCode: 0,
			timedOut: false,
			stdout: "secret stdout",
			stderr: "",
			durationMs: 12,
		});

		const result = await runHermesExecution(
			{ prompt: "do it", mode: "fake", rawArgs: "do it" },
			runner,
		);

		expect(runner).toHaveBeenCalledTimes(1);
		expect(result.status).toBe("succeeded");
		expect(result.stdout).toBe("secret stdout");
		expect(result.safeSummary).not.toContain("secret stdout");
		expect(result.safeSummary).not.toContain("stdout");
	});

	it("runs fake failure and does not leak stderr into safe summary", async () => {
		const runner = vi.fn<HermesExecRunner>().mockResolvedValue({
			exitCode: 2,
			timedOut: false,
			stdout: "",
			stderr: "secret stderr",
			durationMs: 13,
		});

		const result = await runHermesExecution(
			{ prompt: "do it", mode: "fake", rawArgs: "do it" },
			runner,
		);

		expect(runner).toHaveBeenCalledTimes(1);
		expect(result.status).toBe("failed");
		expect(result.stderr).toBe("secret stderr");
		expect(result.safeSummary).not.toContain("secret stderr");
		expect(result.safeSummary).not.toContain("stderr");
	});

	it("runs fake timeout", async () => {
		const runner = vi.fn<HermesExecRunner>().mockResolvedValue({
			exitCode: 1,
			timedOut: true,
			stdout: "",
			stderr: "timed out",
			durationMs: 99,
		});

		const result = await runHermesExecution(
			{ prompt: "do it", mode: "fake", rawArgs: "do it" },
			runner,
		);

		expect(result.status).toBe("timed_out");
		expect(result.timedOut).toBe(true);
	});

	it("blocks real mode by default", async () => {
		const runner = vi.fn<HermesExecRunner>();

		const result = await runHermesExecution(
			{ prompt: "real run", mode: "real", rawArgs: "--mode real real run" },
			runner,
		);

		expect(runner).not.toHaveBeenCalled();
		expect(result.status).toBe("aborted");
		expect(result.exitCode).toBe(0); // blocked gracefully without invoking the runner
		expect(result.timedOut).toBe(false);
		expect(result.blockedReason).toBe("real-mode-disabled");
	});

	it("allows real mode when enabled and uses the runner result", async () => {
		const runner = vi.fn<HermesExecRunner>().mockResolvedValue({
			exitCode: 0,
			timedOut: false,
			stdout: "real stdout",
			stderr: "real stderr",
			durationMs: 17,
		});

		const result = await runHermesExecution(
			{ prompt: "real run", mode: "real", rawArgs: "--mode real real run" },
			runner,
			{ allowRealExecution: true },
		);

		expect(runner).toHaveBeenCalledTimes(1);
		expect(result.status).toBe("succeeded");
		expect(result.exitCode).toBe(0);
		expect(result.stdout).toBe("real stdout");
		expect(result.stderr).toBe("real stderr");
		expect(result.blockedReason).toBeUndefined();
		expect(result.safeSummary).not.toContain("real stdout");
		expect(result.safeSummary).not.toContain("real stderr");
	});

	it("handles real runner spawn errors without crashing", async () => {
		const runner = vi.fn<HermesExecRunner>().mockRejectedValue(Object.assign(new Error("spawn ENOENT hermes"), { code: "ENOENT" }));

		const result = await runHermesExecution(
			{ prompt: "real run", mode: "real", rawArgs: "--mode real real run" },
			runner,
			{ allowRealExecution: true },
		);

		expect(runner).toHaveBeenCalledTimes(1);
		expect(result.status).toBe("aborted");
		expect(result.blockedReason).toBe("hermes-not-found");
		expect(result.exitCode).toBe(127);
		expect(result.safeSummary).toContain("blockedReason=hermes-not-found");
	});

	it("safe summary does not leak stdout or stderr", () => {
		const result = {
			request: { prompt: "x", mode: "fake" as const, rawArgs: "x" },
			mode: "fake" as const,
			status: "failed" as const,
			exitCode: 2,
			timedOut: false,
			durationMs: 42,
			stdout: "top secret stdout",
			stderr: "top secret stderr",
		};

		const summary = createSafeHermesSummary(result);

		expect(summary).not.toContain("top secret stdout");
		expect(summary).not.toContain("top secret stderr");
		expect(summary).not.toContain("stdout");
		expect(summary).not.toContain("stderr");
	});
});
