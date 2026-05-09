#!/usr/bin/env node
import { existsSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { spawn } from "node:child_process";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const repoRoot = resolve(__dirname, "..");
const piTestPath = join(repoRoot, "pi-test.sh");
const rckRoot = join(repoRoot, ".pi", "rck");
const rpcArgs = ["--offline", "--mode", "rpc", "--no-tools", "--no-extensions", "--extension", ".pi/extensions/rck-bridge/index.ts"];
const globalTimeoutMs = Number.parseInt(process.env.RCK_BRIDGE_RPC_SMOKE_TIMEOUT_MS ?? "45000", 10);
const wantRealRun = process.env.RCK_BRIDGE_RPC_SMOKE_REAL === "1" && process.env.RCK_BRIDGE_ALLOW_REAL_HERMES === "1";

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

function cleanupArtifacts() {
	rmSync(rckRoot, { recursive: true, force: true });
}

function loadJsonRecords(relativeDir) {
	const dirPath = join(repoRoot, relativeDir);
	if (!existsSync(dirPath)) {
		return [];
	}
	return readdirSync(dirPath).map((file) => ({
		file,
		path: join(dirPath, file),
		json: JSON.parse(readFileSync(join(dirPath, file), "utf8")),
	}));
}

function parseJsonMaybe(line) {
	try {
		return JSON.parse(line);
	} catch {
		return null;
	}
}

function createLineSplitter(onLine) {
	let buffer = "";
	return {
		push(chunk) {
			buffer += chunk;
			for (;;) {
				const index = buffer.indexOf("\n");
				if (index === -1) {
					return;
				}
				const line = buffer.slice(0, index).replace(/\r$/, "");
				buffer = buffer.slice(index + 1);
				onLine(line);
			}
		},
		flush() {
			const remainder = buffer.replace(/\r$/, "");
			buffer = "";
			if (remainder) {
				onLine(remainder);
			}
		},
	};
}

function hasExpectedCommands(commandsResponse) {
	const names = new Set((commandsResponse?.data?.commands ?? []).map((command) => command?.name));
	return ["state", "rck", "hermes"].every((name) => names.has(name));
}

async function main() {
	if (!existsSync(piTestPath)) {
		throw new Error(`Missing runner: ${piTestPath}`);
	}

	cleanupArtifacts();

	const child = spawn(piTestPath, rpcArgs, {
		cwd: repoRoot,
		env: process.env,
		stdio: ["pipe", "pipe", "pipe"],
		detached: true,
	});

	const responses = new Map();
	const stdoutTail = [];
	const stderrTail = [];
	const debugLines = [];
	let childExit = null;

	const pushTail = (tail, line, max = 30) => {
		tail.push(line);
		while (tail.length > max) {
			tail.shift();
		}
	};

	const stopChild = async () => {
		try {
			child.stdin.end();
		} catch {
			// ignore
		}
		try {
			if (child.pid) {
				try {
					process.kill(-child.pid, "SIGTERM");
				} catch {
					child.kill("SIGTERM");
				}
			}
		} catch {
			// ignore
		}
		await sleep(250);
		try {
			if (child.pid) {
				try {
					process.kill(-child.pid, "SIGKILL");
				} catch {
					child.kill("SIGKILL");
				}
			}
		} catch {
			// ignore
		}
	};

	child.stdout.setEncoding("utf8");
	const stdoutSplitter = createLineSplitter((line) => {
		if (!line.trim()) {
			return;
		}
		pushTail(stdoutTail, line);
		const parsed = parseJsonMaybe(line);
		if (!parsed) {
			debugLines.push(line);
			return;
		}
		if (parsed.type === "response" && parsed.id) {
			responses.set(parsed.id, parsed);
		}
	});
	child.stdout.on("data", (chunk) => stdoutSplitter.push(chunk));
	child.stdout.once("end", () => stdoutSplitter.flush());

	child.stderr.setEncoding("utf8");
	child.stderr.on("data", (chunk) => {
		for (const line of String(chunk).split(/\r?\n/)) {
			if (line) {
				pushTail(stderrTail, line);
			}
		}
	});
	child.once("close", (code, signal) => {
		childExit = { code, signal };
	});

	const deadline = Date.now() + globalTimeoutMs;
	const timeout = setTimeout(() => {
		void stopChild();
	}, globalTimeoutMs);

	const sendJson = async (payload) => {
		const line = `${JSON.stringify(payload)}\n`;
		if (!child.stdin.write(line)) {
			await new Promise((resolve) => child.stdin.once("drain", resolve));
		}
	};

	const waitFor = async (predicate, label) => {
		while (Date.now() < deadline) {
			const value = predicate();
			if (value) {
				return value;
			}
			if (childExit && childExit.code !== 0) {
				throw new Error(`RPC child exited early while waiting for ${label}: ${JSON.stringify(childExit)}`);
			}
			await sleep(50);
		}
		throw new Error(`Timed out waiting for ${label}`);
	};

	const waitForResponse = async (id, label) => {
		return await waitFor(() => responses.get(id), label);
	};

	const runPrompt = async (id, message, label) => {
		await sendJson({ id, type: "prompt", message });
		const response = await waitForResponse(id, `${label} response`);
		if (!response.success) {
			throw new Error(`${label} failed: ${JSON.stringify(response)}`);
		}
	};

	const waitForFile = async (relativePath, label) => {
		await waitFor(() => existsSync(join(repoRoot, relativePath)), label);
	};

	try {
		await sendJson({ id: "1", type: "get_commands" });
		const commandsResponse = await waitForResponse("1", "get_commands response");
		if (!commandsResponse.success) {
			throw new Error(`get_commands failed: ${JSON.stringify(commandsResponse)}`);
		}
		if (!hasExpectedCommands(commandsResponse)) {
			throw new Error(`get_commands missing expected commands: ${JSON.stringify(commandsResponse?.data?.commands ?? [], null, 2)}`);
		}

		await runPrompt("2", "/state", "state");
		await waitForFile(".pi/rck/indexes/latest-state.json", "latest-state index");
		await waitForFile(".pi/rck/indexes/current-trace.json", "current-trace index");

		await runPrompt("3", "/rck inject", "inject");
		await waitForFile(".pi/rck/indexes/latest-context-pack.json", "latest-context-pack index");

		await runPrompt("4", "/rck anchor smoke-anchor", "anchor");
		await waitForFile(".pi/rck/indexes/latest-anchor.json", "latest-anchor index");

		const hermesMessage = wantRealRun
			? "/hermes --mode real Respond only with: HERMES_REAL_OK"
			: "/hermes inspect fake bridge";
		await runPrompt("5", hermesMessage, wantRealRun ? "hermes real" : "hermes fake");
		await runPrompt("6", "/rck status", "status");

		const eventRecords = await waitFor(() => {
			const records = loadJsonRecords(".pi/rck/events");
			const hasRequested = records.some((entry) => entry.json?.eventType === "HermesRunRequested");
			const hasRecorded = records.some((entry) => entry.json?.eventType === "HermesRunRecorded");
			return hasRequested && hasRecorded ? records : null;
		}, "Hermes run events");
		const requestedEvent = eventRecords.find((entry) => entry.json?.eventType === "HermesRunRequested")?.json;
		const recordedEvent = eventRecords.find((entry) => entry.json?.eventType === "HermesRunRecorded")?.json;
		if (!requestedEvent || !recordedEvent) {
			throw new Error("Hermes request/recorded events were not both present");
		}

		const currentTrace = JSON.parse(readFileSync(join(rckRoot, "indexes", "current-trace.json"), "utf8"));
		const latestStateIndex = JSON.parse(readFileSync(join(rckRoot, "indexes", "latest-state.json"), "utf8"));
		const latestContextIndex = JSON.parse(readFileSync(join(rckRoot, "indexes", "latest-context-pack.json"), "utf8"));
		const latestAnchorIndex = JSON.parse(readFileSync(join(rckRoot, "indexes", "latest-anchor.json"), "utf8"));
		if (requestedEvent.traceId !== currentTrace.traceId || recordedEvent.traceId !== currentTrace.traceId) {
			throw new Error(`Hermes traceId mismatch: current=${currentTrace.traceId}, requested=${requestedEvent.traceId}, recorded=${recordedEvent.traceId}`);
		}
		if (latestStateIndex.traceId !== currentTrace.traceId || latestContextIndex.traceId !== currentTrace.traceId || latestAnchorIndex.traceId !== currentTrace.traceId) {
			throw new Error(
				`Current trace mismatch across indexes: current=${currentTrace.traceId}, state=${latestStateIndex.traceId}, context=${latestContextIndex.traceId}, anchor=${latestAnchorIndex.traceId}`,
			);
		}

		const stdoutDir = join(rckRoot, "evidence", "hermes", "stdout");
		const stderrDir = join(rckRoot, "evidence", "hermes", "stderr");
		const stdoutFiles = existsSync(stdoutDir) ? readdirSync(stdoutDir) : [];
		const stderrFiles = existsSync(stderrDir) ? readdirSync(stderrDir) : [];
		if (stdoutFiles.length === 0) {
			throw new Error("Expected Hermes stdout evidence, but no stdout evidence files were created");
		}
		if (stderrFiles.length > 1) {
			throw new Error(`Expected at most one stderr evidence file, found ${stderrFiles.length}`);
		}

		const stdoutEvidencePath = join(stdoutDir, stdoutFiles[0]);
		const stdoutEvidence = readFileSync(stdoutEvidencePath, "utf8");
		if (wantRealRun) {
			if (!stdoutEvidence.includes("HERMES_REAL_OK")) {
				throw new Error(`Expected HERMES_REAL_OK in stdout evidence: ${stdoutEvidencePath}`);
			}
			if (String(recordedEvent?.payload?.safeSummary ?? "").includes("HERMES_REAL_OK")) {
				throw new Error("HERMES_REAL_OK leaked into safeSummary/custom_message path");
			}
		} else {
			if (!stdoutEvidence.includes("mock-hermes-stdout")) {
				throw new Error(`Expected fake stdout evidence in ${stdoutEvidencePath}`);
			}
			if (/mock-hermes-stdout|mock-hermes-stderr/i.test(String(recordedEvent?.payload?.safeSummary ?? ""))) {
				throw new Error("Fake Hermes evidence markers leaked into safeSummary/custom_message path");
			}
		}

		if (recordedEvent?.payload?.mode !== (wantRealRun ? "real" : "fake")) {
			throw new Error(`Unexpected Hermes mode in recorded event: ${recordedEvent?.payload?.mode}`);
		}
		if (recordedEvent?.payload?.status !== "succeeded") {
			throw new Error(`Unexpected Hermes status in recorded event: ${recordedEvent?.payload?.status}`);
		}
		if (requestedEvent?.eventType !== "HermesRunRequested" || recordedEvent?.eventType !== "HermesRunRecorded") {
			throw new Error("Hermes event types were not correct");
		}

		console.log(`commands OK: ${commandsResponse.data.commands.map((command) => command.name).join(", ")}`);
		console.log("state OK");
		console.log("inject OK");
		console.log("anchor OK");
		console.log(wantRealRun ? "hermes real OK" : "hermes fake OK");
		console.log("cleanup OK");
		return 0;
	} catch (error) {
		const message = error instanceof Error ? error.message : String(error);
		process.stderr.write(`RPC smoke failed: ${message}\n`);
		if (debugLines.length > 0) {
			process.stderr.write(`stdout debug tail:\n${debugLines.slice(-10).map((line) => `  ${line}`).join("\n")}\n`);
		}
		if (stderrTail.length > 0) {
			process.stderr.write(`stderr tail:\n${stderrTail.slice(-10).map((line) => `  ${line}`).join("\n")}\n`);
		}
		return 1;
	} finally {
		clearTimeout(timeout);
		await stopChild();
		cleanupArtifacts();
	}
}

main().then((code) => {
	process.exitCode = code;
});
