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
const globalTimeoutMs = Number.parseInt(process.env.RCK_BRIDGE_RPC_UI_DTO_SMOKE_TIMEOUT_MS ?? "45000", 10);

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

function cleanupArtifacts() {
	rmSync(rckRoot, { recursive: true, force: true });
}

function loadJsonRecords(relativeDir) {
	const dirPath = join(repoRoot, relativeDir);
	if (!existsSync(dirPath)) {
		return [];
	}
	return readdirSync(dirPath)
		.filter((file) => file.endsWith(".json"))
		.sort()
		.map((file) => ({
			file,
			path: join(dirPath, file),
			json: JSON.parse(readFileSync(join(dirPath, file), "utf8")),
		}));
}

function readJson(relativePath) {
	return JSON.parse(readFileSync(join(rckRoot, relativePath), "utf8"));
}

function readRepoJson(relativePath) {
	return JSON.parse(readFileSync(join(repoRoot, relativePath), "utf8"));
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

function countJsonFiles(relativeDir) {
	const dirPath = join(rckRoot, relativeDir);
	if (!existsSync(dirPath)) {
		return 0;
	}
	return readdirSync(dirPath).filter((file) => file.endsWith(".json")).length;
}

function findLatestEvent(events, eventType) {
	for (let index = events.length - 1; index >= 0; index -= 1) {
		if (events[index]?.eventType === eventType) {
			return events[index];
		}
	}
	return undefined;
}

async function main() {
	if (!existsSync(piTestPath)) {
		throw new Error(`Missing runner: ${piTestPath}`);
	}

	await import("tsx");
	const {
		normalizeHermesRunResultDto,
		normalizeRckInventoryDto,
		normalizeRckStatusDto,
		normalizeRckSupervisionDto,
	} = await import("../.pi/extensions/rck-bridge/rck-ui-dtos.ts");
	const { evaluateRckSupervision } = await import("../.pi/extensions/rck-bridge/rck-supervision.ts");

	cleanupArtifacts();

	const child = spawn(piTestPath, rpcArgs, {
		cwd: repoRoot,
		env: process.env,
		stdio: ["pipe", "pipe", "pipe"],
		detached: true,
	});

	const responses = new Map();
	let childExit = null;

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
		const parsed = parseJsonMaybe(line);
		if (parsed?.type === "response" && parsed.id) {
			responses.set(parsed.id, parsed);
		}
	});
	child.stdout.on("data", (chunk) => stdoutSplitter.push(chunk));
	child.stdout.once("end", () => stdoutSplitter.flush());
	child.stderr.resume();
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
			throw new Error(`${label} failed: ${JSON.stringify({ id: response.id, success: response.success, error: response.error ?? null })}`);
		}
	};

	const waitForFile = async (relativePath, label) => {
		await waitFor(() => existsSync(join(rckRoot, relativePath)), label);
	};

	try {
		await sendJson({ id: "1", type: "get_commands" });
		const commandsResponse = await waitForResponse("1", "get_commands response");
		if (!commandsResponse.success) {
			throw new Error(`get_commands failed: ${JSON.stringify({ id: commandsResponse.id, success: commandsResponse.success, error: commandsResponse.error ?? null })}`);
		}
		const commandNames = new Set((commandsResponse.data?.commands ?? []).map((command) => command?.name));
		for (const name of ["state", "rck", "hermes"]) {
			if (!commandNames.has(name)) {
				throw new Error(`Missing command: ${name}`);
			}
		}

		await runPrompt("2", "/state", "state");
		await waitForFile("indexes/latest-state.json", "latest-state index");

		await runPrompt("3", "/rck inject", "inject");
		await waitForFile("indexes/latest-context-pack.json", "latest-context-pack index");

		await runPrompt("4", "/rck anchor smoke-anchor", "anchor");
		await waitForFile("indexes/latest-anchor.json", "latest-anchor index");

		await runPrompt("5", "/hermes inspect fake bridge", "hermes fake");
		await runPrompt("6", "/rck status", "status");
		await runPrompt("7", "/rck supervise", "supervise");
		await runPrompt("8", "/rck list", "list");

		const currentTrace = readJson("indexes/current-trace.json");
		const latestStateIndex = readJson("indexes/latest-state.json");
		const latestContextPackIndex = readJson("indexes/latest-context-pack.json");
		const latestAnchorIndex = readJson("indexes/latest-anchor.json");
		const latestState = readRepoJson(latestStateIndex.currentStatePath);
		const latestContextPack = readRepoJson(latestContextPackIndex.currentContextPackPath);
		const latestAnchor = readRepoJson(latestAnchorIndex.currentAnchorPath);
		const events = loadJsonRecords(".pi/rck/events").map((record) => record.json);
		const latestHermesRecordedEvent = findLatestEvent(events, "HermesRunRecorded");
		if (!latestHermesRecordedEvent) {
			throw new Error("Missing HermesRunRecorded event");
		}

		const hermesRunDto = normalizeHermesRunResultDto(latestHermesRecordedEvent);
		if (Object.prototype.hasOwnProperty.call(hermesRunDto, "stdout") || Object.prototype.hasOwnProperty.call(hermesRunDto, "stderr")) {
			throw new Error("Hermes DTO exposed raw stdout/stderr");
		}
		if (!hermesRunDto.evidenceRefs.every((ref) => ref.displayPolicy === "reference-only" && ref.isRaw === false)) {
			throw new Error("Hermes DTO exposed unsafe evidence references");
		}

		const statusDto = normalizeRckStatusDto({
			currentTrace,
			latestStateIndex,
			latestState,
			latestContextPackIndex,
			latestContextPack,
			latestAnchorIndex,
			latestAnchor,
			latestHermesRun: latestHermesRecordedEvent,
			generatedAt: "2026-05-10T09:01:00.000Z",
		});
		if (statusDto.traceId !== currentTrace.traceId) {
			throw new Error("Status DTO traceId mismatch");
		}
		if (statusDto.latestHermesRun?.recordedEventId !== latestHermesRecordedEvent.eventId) {
			throw new Error("Status DTO Hermes run mismatch");
		}

		const inventoryDto = normalizeRckInventoryDto({
			traceId: currentTrace.traceId,
			counts: {
				states: countJsonFiles("states"),
				contextPacks: countJsonFiles("context-packs"),
				anchors: countJsonFiles("anchors"),
				events: events.length,
				hermesRuns: events.filter((event) => event?.eventType === "HermesRunRecorded").length,
			},
			latestEvents: events,
			latestHermesRun: latestHermesRecordedEvent,
			generatedAt: "2026-05-10T09:01:00.000Z",
		});
		if (inventoryDto.latestHermesRun?.evidenceRefs.some((ref) => ref.displayPolicy !== "reference-only")) {
			throw new Error("Inventory DTO exposed unsafe evidence policy");
		}
		if (inventoryDto.latestEvents.some((event) => /stdout|stderr/i.test(event.summary))) {
			throw new Error("Inventory DTO exposed raw evidence in event summaries");
		}

		const supervisionEvaluation = evaluateRckSupervision({
			currentTrace: { traceId: currentTrace.traceId },
			latestHermes: hermesRunDto,
			latestEventId: latestHermesRecordedEvent.eventId,
		});
		const supervisionDto = normalizeRckSupervisionDto({
			evaluation: supervisionEvaluation,
			generatedAt: "2026-05-10T09:01:00.000Z",
		});
		if (supervisionDto.traceId !== currentTrace.traceId) {
			throw new Error("Supervision DTO traceId mismatch");
		}
		if (supervisionDto.needsAttention) {
			throw new Error("Supervision DTO unexpectedly requires attention for the fake Hermes flow");
		}

		if (statusDto.latestHermesRun?.evidenceRefs.length !== hermesRunDto.evidenceRefs.length) {
			throw new Error("Status DTO Hermes evidence refs mismatch");
		}
		if (inventoryDto.latestHermesRun?.recordedEventId !== latestHermesRecordedEvent.eventId) {
			throw new Error("Inventory DTO Hermes run mismatch");
		}

		console.log("commands OK");
		console.log("state DTO OK");
		console.log("inventory DTO OK");
		console.log("supervision DTO OK");
		console.log("Hermes run DTO OK");
		console.log("cleanup OK");
		return 0;
	} catch (error) {
		const message = error instanceof Error ? error.message : String(error);
		process.stderr.write(`UI DTO smoke failed: ${message}\n`);
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
