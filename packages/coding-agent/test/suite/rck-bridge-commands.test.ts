import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import registerRckBridge from "../../../../.pi/extensions/rck-bridge/index.js";
import { createHarnessWithExtensions, type Harness } from "../test-harness.js";

function getCustomEntries(harness: Harness) {
	return harness.session.sessionManager.getEntries().filter((entry) => entry.type === "custom");
}

function getCustomMessages(harness: Harness) {
	return harness.session.messages.filter((message) => message.role === "custom");
}

describe("RCK bridge commands", () => {
	let harness: Harness | undefined;

	afterEach(() => {
		harness?.cleanup();
		harness = undefined;
	});

	it("executes /state and records a mock state pack", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();
		expect(runner!.getCommand("state")).toBeDefined();

		await runner!.getCommand("state")!.handler("", runner!.createCommandContext());

		const rckRoot = join(harness.tempDir, ".pi", "rck");
		expect(existsSync(rckRoot)).toBe(true);

		const stateFiles = readdirSync(join(rckRoot, "states"));
		const eventFiles = readdirSync(join(rckRoot, "events"));
		expect(stateFiles.length).toBe(1);
		expect(eventFiles.length).toBe(1);
		expect(readdirSync(join(rckRoot, "context-packs"))).toHaveLength(0);

		const latestState = JSON.parse(readFileSync(join(rckRoot, "indexes", "latest-state.json"), "utf-8")) as {
			currentStateId: string;
			currentStatePath: string;
			currentEventId: string;
			traceId: string;
		};
		expect(latestState.currentStateId).toContain("state_");
		expect(latestState.currentStatePath).toMatch(/^\.pi\/rck\/states\//);
		expect(latestState.currentEventId).toContain("evt_");
		expect(latestState.traceId).toContain("trace_");

		const currentTrace = JSON.parse(readFileSync(join(rckRoot, "indexes", "current-trace.json"), "utf-8")) as {
			traceId: string;
			headAnchorId: string | null;
			anchorCount: number;
		};
		expect(currentTrace.traceId).toBe(latestState.traceId);
		expect(currentTrace.anchorCount).toBe(0);

		const stateJson = JSON.parse(readFileSync(join(rckRoot, "states", stateFiles[0]), "utf-8")) as {
			artifactType: string;
			stateId: string;
			traceId: string;
			summary: { objective: string };
		};
		const eventJson = JSON.parse(readFileSync(join(rckRoot, "events", eventFiles[0]), "utf-8")) as {
			artifactType: string;
			eventType: string;
			payload: { stateId: string; statePath: string };
			traceId: string;
		};
		expect(stateJson.artifactType).toBe("rck.state");
		expect(eventJson.artifactType).toBe("rck.event");
		expect(eventJson.eventType).toBe("StatePackCreated");
		expect(eventJson.payload.stateId).toBe(stateJson.stateId);
		expect(eventJson.payload.statePath).toBe(latestState.currentStatePath);
		expect(eventJson.traceId).toBe(stateJson.traceId);
		expect(stateJson.traceId).toBe(currentTrace.traceId);
		expect(eventJson.traceId).toBe(currentTrace.traceId);

		const customEntries = getCustomEntries(harness);
		expect(
			customEntries.some(
				(entry) => entry.customType === "rck-bridge.state.created" && (entry.data as { eventType?: string } | undefined)?.eventType === "StatePackCreated",
			),
		).toBe(true);
		expect(customEntries.some((entry) => entry.customType === "rck-bridge.hermes.recorded")).toBe(false);

		const customMessages = getCustomMessages(harness);
		expect(
			customMessages.some(
				(message) =>
					message.customType === "rck-bridge-status" &&
					typeof message.content === "string" &&
					message.content.includes("RCK /state wrote local state, event, and latest-state index"),
			),
		).toBe(true);
	});

	it("executes /rck inject without state and fails safely", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();
		await runner!.getCommand("rck")!.handler("inject", runner!.createCommandContext());

		const rckRoot = join(harness.tempDir, ".pi", "rck");
		expect(readdirSync(join(rckRoot, "context-packs"))).toHaveLength(0);
		expect(readdirSync(join(rckRoot, "events"))).toHaveLength(0);
		expect(existsSync(join(rckRoot, "indexes", "latest-context-pack.json"))).toBe(false);

		const customMessages = getCustomMessages(harness);
		const missingStateMessage = customMessages.find((message) => message.customType === "rck-bridge.context.missing");
		expect(missingStateMessage).toBeDefined();
		expect(String(missingStateMessage?.content)).toContain("No RCK state available for /rck inject. Run /state first.");
	});

	it("executes /state then /rck inject and records a safe context pack", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();
		await runner!.getCommand("state")!.handler("design bridge state", runner!.createCommandContext());
		await runner!.getCommand("rck")!.handler("inject", runner!.createCommandContext());


		const rckRoot = join(harness.tempDir, ".pi", "rck");
		const contextPackFiles = readdirSync(join(rckRoot, "context-packs"));
		const eventFiles = readdirSync(join(rckRoot, "events"));
		expect(contextPackFiles.length).toBe(1);
		expect(eventFiles.length).toBe(2);
		expect(existsSync(join(rckRoot, "indexes", "latest-context-pack.json"))).toBe(true);

		const latestContext = JSON.parse(readFileSync(join(rckRoot, "indexes", "latest-context-pack.json"), "utf-8")) as {
			currentContextPackId: string;
			currentContextPackPath: string;
			currentEventId: string;
			stateId: string;
			statePath: string;
			traceId: string;
		};
		expect(latestContext.currentContextPackId).toContain("pack_");
		expect(latestContext.currentContextPackPath).toMatch(/^\.pi\/rck\/context-packs\//);
		expect(latestContext.currentEventId).toContain("evt_");
		expect(latestContext.statePath).toMatch(/^\.pi\/rck\/states\//);

		const currentTrace = JSON.parse(readFileSync(join(rckRoot, "indexes", "current-trace.json"), "utf-8")) as {
			traceId: string;
			headAnchorId: string | null;
			anchorCount: number;
		};
		expect(currentTrace.traceId).toBe(latestContext.traceId);

		const contextPackJson = JSON.parse(readFileSync(join(rckRoot, "context-packs", contextPackFiles[0]), "utf-8")) as {
			artifactType: string;
			allowedToInject: boolean;
			contextPackId: string;
			stateId: string;
			statePath: string;
			traceId: string;
			contextSummary: { objective: string };
		};
		expect(contextPackJson.artifactType).toBe("rck.context-pack");
		expect(contextPackJson.allowedToInject).toBe(true);
		expect(contextPackJson.stateId).toBe(latestContext.stateId);
		expect(contextPackJson.statePath).toBe(latestContext.statePath);
		expect(contextPackJson.traceId).toBe(currentTrace.traceId);

		const injectEventFile = eventFiles.find((file) => {
			const parsed = JSON.parse(readFileSync(join(rckRoot, "events", file), "utf-8")) as { eventType?: string };
			return parsed.eventType === "ContextPackInjected";
		});
		expect(injectEventFile).toBeDefined();
		const injectEventJson = JSON.parse(readFileSync(join(rckRoot, "events", injectEventFile as string), "utf-8")) as {
			eventType: string;
			traceId: string;
			payload: { contextPackId: string; contextPackPath: string; stateId: string };
		};
		expect(injectEventJson.eventType).toBe("ContextPackInjected");
		expect(injectEventJson.traceId).toBe(currentTrace.traceId);
		expect(injectEventJson.payload.contextPackId).toBe(contextPackJson.contextPackId);
		expect(injectEventJson.payload.stateId).toBe(latestContext.stateId);

		const customEntries = getCustomEntries(harness);
		expect(customEntries.some((entry) => entry.customType === "rck-bridge.context.injected.record")).toBe(true);
		const customMessages = getCustomMessages(harness);
		const injectedMessage = customMessages.find((message) => message.customType === "rck-bridge.context.injected");
		expect(injectedMessage).toBeDefined();
		expect(String(injectedMessage?.content)).toContain("RCK context pack ready:");
		expect(String(injectedMessage?.content)).not.toMatch(/stdout|stderr|diff|log/i);
		expect((injectedMessage?.details as { allowedToInject?: boolean } | undefined)?.allowedToInject).toBe(true);
	});

	it("executes /rck anchor without state and records a formal anchor", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();
		await runner!.getCommand("rck")!.handler("anchor phase-3b-started", runner!.createCommandContext());

		const rckRoot = join(harness.tempDir, ".pi", "rck");
		expect(existsSync(join(rckRoot, "anchors"))).toBe(true);
		expect(existsSync(join(rckRoot, "indexes", "latest-anchor.json"))).toBe(true);
		expect(existsSync(join(rckRoot, "indexes", "latest-state.json"))).toBe(false);
		expect(existsSync(join(rckRoot, "indexes", "latest-context-pack.json"))).toBe(false);

		const anchorFiles = readdirSync(join(rckRoot, "anchors"));
		const eventFiles = readdirSync(join(rckRoot, "events"));
		expect(anchorFiles).toHaveLength(1);
		expect(eventFiles).toHaveLength(1);

		const latestAnchor = JSON.parse(readFileSync(join(rckRoot, "indexes", "latest-anchor.json"), "utf-8")) as {
			currentAnchorId: string;
			currentAnchorPath: string;
			currentEventId: string;
			traceId: string;
		};
		expect(latestAnchor.currentAnchorId).toContain("anchor_");
		expect(latestAnchor.currentAnchorPath).toMatch(/^\.pi\/rck\/anchors\//);
		expect(latestAnchor.currentEventId).toContain("evt_");

		const currentTrace = JSON.parse(readFileSync(join(rckRoot, "indexes", "current-trace.json"), "utf-8")) as {
			traceId: string;
			headAnchorId: string | null;
			anchorCount: number;
		};
		expect(currentTrace.traceId).toBe(latestAnchor.traceId);
		expect(currentTrace.headAnchorId).toBe(latestAnchor.currentAnchorId);
		expect(currentTrace.anchorCount).toBe(1);

		const anchorJson = JSON.parse(readFileSync(join(rckRoot, "anchors", anchorFiles[0]), "utf-8")) as {
			artifactType: string;
			anchorId: string;
			anchorName: string;
			stateId?: string;
			statePath?: string;
		};
		expect(anchorJson.artifactType).toBe("rck.anchor");
		expect(anchorJson.anchorName).toBe("phase-3b-started");
		expect(anchorJson.stateId).toBeUndefined();
		expect(anchorJson.statePath).toBeUndefined();

		const anchorEventJson = JSON.parse(readFileSync(join(rckRoot, "events", eventFiles[0]), "utf-8")) as {
			eventType: string;
			payload: { anchorId: string; stateId?: string };
		};
		expect(anchorEventJson.eventType).toBe("AnchorRegistered");
		expect(anchorEventJson.payload.anchorId).toBe(anchorJson.anchorId);
		expect(anchorEventJson.payload.stateId).toBeUndefined();

		const customEntries = getCustomEntries(harness);
		expect(customEntries.some((entry) => entry.customType === "rck-bridge.anchor.registered")).toBe(true);
		const customMessages = getCustomMessages(harness);
		expect(customMessages.some((message) => message.customType === "rck-bridge.anchor.registered" && String(message.content).includes("RCK anchor ready: phase-3b-started"))).toBe(true);
	});

	it("executes /state then /rck anchor and links to the latest state", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();
		await runner!.getCommand("state")!.handler("phase 3b anchor linkage", runner!.createCommandContext());

		await runner!.getCommand("rck")!.handler("anchor phase-3b-linked", runner!.createCommandContext());

		const rckRoot = join(harness.tempDir, ".pi", "rck");
		expect(existsSync(join(rckRoot, "indexes", "latest-state.json"))).toBe(true);
		expect(existsSync(join(rckRoot, "indexes", "latest-anchor.json"))).toBe(true);

		const latestState = JSON.parse(readFileSync(join(rckRoot, "indexes", "latest-state.json"), "utf-8")) as {
			currentStateId: string;
			currentStatePath: string;
			currentEventId: string;
			traceId: string;
		};
		const latestAnchor = JSON.parse(readFileSync(join(rckRoot, "indexes", "latest-anchor.json"), "utf-8")) as {
			currentAnchorId: string;
			currentAnchorPath: string;
			currentEventId: string;
			traceId: string;
		};
		expect(latestAnchor.traceId).toBeDefined();
		expect(latestAnchor.currentAnchorPath).toMatch(/^\.pi\/rck\/anchors\//);
		expect(latestAnchor.currentEventId).toContain("evt_");

		const currentTrace = JSON.parse(readFileSync(join(rckRoot, "indexes", "current-trace.json"), "utf-8")) as {
			traceId: string;
			headAnchorId: string | null;
			anchorCount: number;
		};
		expect(currentTrace.traceId).toBe(latestState.traceId);
		expect(currentTrace.traceId).toBe(latestAnchor.traceId);

		const anchorFiles = readdirSync(join(rckRoot, "anchors"));
		expect(anchorFiles).toHaveLength(1);
		const anchorJson = JSON.parse(readFileSync(join(rckRoot, "anchors", anchorFiles[0]), "utf-8")) as {
			artifactType: string;
			anchorName: string;
			stateId?: string;
			statePath?: string;
		};
		expect(anchorJson.artifactType).toBe("rck.anchor");
		expect(anchorJson.anchorName).toBe("phase-3b-linked");
		expect(anchorJson.stateId).toBe(latestState.currentStateId);
		expect(anchorJson.statePath).toBe(latestState.currentStatePath);

		const eventFiles = readdirSync(join(rckRoot, "events"));
		const anchorEventFile = eventFiles.find((file) => {
			const parsed = JSON.parse(readFileSync(join(rckRoot, "events", file), "utf-8")) as { eventType?: string };
			return parsed.eventType === "AnchorRegistered";
		});
		expect(anchorEventFile).toBeDefined();
		const anchorEventJson = JSON.parse(readFileSync(join(rckRoot, "events", anchorEventFile as string), "utf-8")) as {
			eventType: string;
			payload: { anchorId: string; stateId?: string; statePath?: string };
		};
		expect(anchorEventJson.eventType).toBe("AnchorRegistered");
		expect(anchorEventJson.payload.stateId).toBe(latestState.currentStateId);
		expect(anchorEventJson.payload.statePath).toBe(latestState.currentStatePath);

		const customMessages = getCustomMessages(harness);
		expect(customMessages.some((message) => message.customType === "rck-bridge.anchor.registered" && String(message.content).includes("linked to latest state"))).toBe(true);
	});


	it("executes /rck status without storage and stays read-only", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();
		await runner!.getCommand("rck")!.handler("status", runner!.createCommandContext());

		const rckRoot = join(harness.tempDir, ".pi", "rck");
		expect(existsSync(rckRoot)).toBe(false);

		const customMessages = getCustomMessages(harness);
		const statusMessage = customMessages.find(
			(message) => message.customType === "rck-bridge-status" && String(message.content).includes("No RCK storage found. Run /state first."),
		);
		expect(statusMessage).toBeDefined();
		expect(String(statusMessage?.content)).toContain("No RCK storage found. Run /state first.");
	});

	it("executes /state then /rck status and reports missing context pack and anchor", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();
		await runner!.getCommand("state")!.handler("status baseline", runner!.createCommandContext());
		await runner!.getCommand("rck")!.handler("status", runner!.createCommandContext());

		const rckRoot = join(harness.tempDir, ".pi", "rck");
		expect(existsSync(join(rckRoot, "indexes", "latest-state.json"))).toBe(true);
		expect(existsSync(join(rckRoot, "indexes", "latest-context-pack.json"))).toBe(false);
		expect(existsSync(join(rckRoot, "indexes", "latest-anchor.json"))).toBe(false);

		const customMessages = getCustomMessages(harness);
		const statusMessage = customMessages.find(
			(message) => message.customType === "rck-bridge-status" && String(message.content).startsWith("RCK status"),
		);
		expect(statusMessage).toBeDefined();
		const content = String(statusMessage?.content);
		expect(content).toContain("- state: present");
		expect(content).toContain("- context pack: missing");
		expect(content).toContain("- anchor: missing");
		expect(content).toContain("- latest Hermes: missing");
		const currentTrace = JSON.parse(readFileSync(join(rckRoot, "indexes", "current-trace.json"), "utf-8")) as {
			traceId: string;
			headAnchorId: string | null;
			anchorCount: number;
		};
		expect(content).toContain(`- current trace: traceId=${currentTrace.traceId}`);
		expect(content).not.toMatch(/mock-hermes-stdout|mock-hermes-stderr/i);
	});

	it("executes /state, /rck inject, /rck anchor, /hermes fake, then /rck status with safe Hermes metadata", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();
		await runner!.getCommand("state")!.handler("status full flow", runner!.createCommandContext());
		await runner!.getCommand("rck")!.handler("inject", runner!.createCommandContext());
		await runner!.getCommand("rck")!.handler("anchor status-flow", runner!.createCommandContext());
		await runner!.getCommand("hermes")!.handler("inspect status flow", runner!.createCommandContext());
		await runner!.getCommand("rck")!.handler("status", runner!.createCommandContext());

		const rckRoot = join(harness.tempDir, ".pi", "rck");
		expect(existsSync(join(rckRoot, "indexes", "latest-state.json"))).toBe(true);
		expect(existsSync(join(rckRoot, "indexes", "latest-context-pack.json"))).toBe(true);
		expect(existsSync(join(rckRoot, "indexes", "latest-anchor.json"))).toBe(true);

		const customMessages = getCustomMessages(harness);
		const statusMessage = customMessages.find(
			(message) => message.customType === "rck-bridge-status" && String(message.content).startsWith("RCK status"),
		);
		expect(statusMessage).toBeDefined();
		const content = String(statusMessage?.content);
		expect(content).toContain("- state: present");
		expect(content).toContain("- context pack: present");
		expect(content).toContain("- anchor: present");
		expect(content).toContain("- latest Hermes: mode=fake, status=succeeded");
		const currentTrace = JSON.parse(readFileSync(join(rckRoot, "indexes", "current-trace.json"), "utf-8")) as {
			traceId: string;
			headAnchorId: string | null;
			anchorCount: number;
		};
		expect(content).toContain(`- current trace: traceId=${currentTrace.traceId}`);
		expect(content).toMatch(/evidence=(stdout|stderr|stdout\/stderr)/);
		expect(content).not.toMatch(/mock-hermes-stdout|mock-hermes-stderr/i);
	});

	it("executes /hermes inspect mock bridge and records safe Hermes results", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();
		expect(runner!.getCommand("hermes")).toBeDefined();

		await runner!.getCommand("hermes")!.handler("inspect mock bridge", runner!.createCommandContext());

		const rckRoot = join(harness.tempDir, ".pi", "rck");
		const stdoutDir = join(rckRoot, "evidence", "hermes", "stdout");
		const stderrDir = join(rckRoot, "evidence", "hermes", "stderr");
		const eventDir = join(rckRoot, "events");
		const stdoutFiles = existsSync(stdoutDir) ? readdirSync(stdoutDir) : [];
		const stderrFiles = existsSync(stderrDir) ? readdirSync(stderrDir) : [];
		const eventFiles = readdirSync(eventDir);
		expect(stdoutFiles).toHaveLength(1);
		expect(stderrFiles).toHaveLength(0);
		expect(eventFiles).toHaveLength(2);

		const eventRecords = eventFiles.map((file) =>
			JSON.parse(readFileSync(join(eventDir, file), "utf-8")) as {
				eventType: string;
				traceId: string;
				requestEventId?: string;
				mode?: string;
				status?: string;
				blockedReason?: string;
				stdoutRef?: { kind?: string; path?: string };
				stderrRef?: { kind?: string; path?: string };
				payload?: {
					mode?: string;
					status?: string;
					blockedReason?: string;
					promptSummary?: string;
					timeoutMs?: number;
					stdoutRef?: { kind?: string; path?: string };
					stderrRef?: { kind?: string; path?: string };
					safeSummary?: string;
					requestEventId?: string;
				};
			},
		);
		const requestedEvent = eventRecords.find((event) => event.eventType === "HermesRunRequested");
		const recordedEvent = eventRecords.find((event) => event.eventType === "HermesRunRecorded");
		expect(requestedEvent).toBeDefined();
		expect(recordedEvent).toBeDefined();
		const currentTrace = JSON.parse(readFileSync(join(rckRoot, "indexes", "current-trace.json"), "utf-8")) as {
			traceId: string;
			headAnchorId: string | null;
			anchorCount: number;
		};
		expect(requestedEvent?.traceId).toBe(currentTrace.traceId);
		expect(recordedEvent?.traceId).toBe(currentTrace.traceId);
		expect(requestedEvent?.payload?.mode).toBe("fake");
		expect(requestedEvent?.payload?.promptSummary).toBe("inspect mock bridge");
		expect(requestedEvent?.payload?.timeoutMs).toBeUndefined();
		expect(recordedEvent?.payload?.mode).toBe("fake");
		expect(recordedEvent?.payload?.status).toBe("succeeded");
		expect(recordedEvent?.payload?.blockedReason).toBeUndefined();
		expect(recordedEvent?.payload?.stdoutRef?.kind).toBe("stdout");
		expect(recordedEvent?.payload?.stderrRef).toBeUndefined();
		expect(recordedEvent?.requestEventId).toBeDefined();
		expect(recordedEvent?.payload?.requestEventId).toBe(recordedEvent?.requestEventId);

		const customEntries = getCustomEntries(harness);
		const requestedEntry = customEntries.find((entry) => entry.customType === "rck-bridge.hermes.requested");
		const recordedEntry = customEntries.find((entry) => entry.customType === "rck-bridge.hermes.recorded");

		expect(requestedEntry).toBeDefined();
		expect((requestedEntry?.data as { eventType?: string; command?: { args?: string } } | undefined)?.eventType).toBe(
			"HermesRunRequested",
		);

		expect((requestedEntry?.data as { command?: { args?: string } } | undefined)?.command?.args).toBe(
			"inspect mock bridge",
		);
		expect(recordedEntry).toBeDefined();
		expect((recordedEntry?.data as { eventType?: string; exitCode?: number } | undefined)?.eventType).toBe(
			"HermesRunRecorded",
		);
		expect((recordedEntry?.data as { mode?: string; status?: string } | undefined)?.mode).toBe("fake");
		expect((recordedEntry?.data as { mode?: string; status?: string } | undefined)?.status).toBe("succeeded");
		expect((recordedEntry?.data as { blockedReason?: string } | undefined)?.blockedReason).toBeUndefined();
		expect((recordedEntry?.data as { stdoutRef?: { kind?: string; path?: string } } | undefined)?.stdoutRef?.kind).toBe(
			"stdout",
		);
		expect((recordedEntry?.data as { stderrRef?: unknown } | undefined)?.stderrRef).toBeUndefined();

		const stdoutFile = stdoutFiles[0];
		const stdoutContent = readFileSync(join(stdoutDir, stdoutFile), "utf-8");
		expect(stdoutContent).toContain("mock-hermes-stdout: inspect mock bridge");
		expect((recordedEntry?.data as { stdoutRef?: { path?: string; byteLength?: number } } | undefined)?.stdoutRef?.path).toBe(
			`.pi/rck/evidence/hermes/stdout/${stdoutFile}`,
		);
		expect((recordedEntry?.data as { safeSummary?: string } | undefined)?.safeSummary).toContain("mode=fake");
		expect((recordedEntry?.data as { safeSummary?: string } | undefined)?.safeSummary).toContain("status=succeeded");

		const customMessages = getCustomMessages(harness);
		const statusMessage = customMessages.find(
			(message) =>
				message.customType === "rck-bridge-status" &&
				typeof message.content === "string" &&
				message.content.includes("Hermes fake run recorded"),
		);
		expect(statusMessage).toBeDefined();
		expect(String(statusMessage?.content)).toContain("status=succeeded");
		expect(String(statusMessage?.content)).not.toMatch(/mock-hermes-stdout|mock-hermes-stderr|stdout|stderr/i);
		expect((statusMessage?.details as { mock?: boolean; stdoutRef?: unknown } | undefined)?.mock).toBe(true);
		expect((statusMessage?.details as { eventType?: string } | undefined)?.eventType).toBe("HermesRunRecorded");
		expect((statusMessage?.details as { stdoutRef?: { kind?: string; path?: string } } | undefined)?.stdoutRef?.kind).toBe(
			"stdout",
		);
	});

	it("executes /hermes inspect mock bridge fail and records stderr evidence", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();

		await runner!.getCommand("hermes")!.handler("inspect mock bridge fail", runner!.createCommandContext());

		const rckRoot = join(harness.tempDir, ".pi", "rck");
		const stdoutDir = join(rckRoot, "evidence", "hermes", "stdout");
		const stderrDir = join(rckRoot, "evidence", "hermes", "stderr");
		const eventDir = join(rckRoot, "events");
		const stdoutFiles = existsSync(stdoutDir) ? readdirSync(stdoutDir) : [];
		const stderrFiles = existsSync(stderrDir) ? readdirSync(stderrDir) : [];
		const eventFiles = readdirSync(eventDir);
		expect(stdoutFiles).toHaveLength(0);
		expect(stderrFiles).toHaveLength(1);
		expect(eventFiles).toHaveLength(2);

		const eventRecords = eventFiles.map((file) =>
			JSON.parse(readFileSync(join(eventDir, file), "utf-8")) as {
				eventType: string;
				requestEventId?: string;
				payload?: { mode?: string; status?: string; blockedReason?: string; stderrRef?: { kind?: string } };
			},
		);
		const requestedEvent = eventRecords.find((event) => event.eventType === "HermesRunRequested");
		const recordedEvent = eventRecords.find((event) => event.eventType === "HermesRunRecorded");
		expect(requestedEvent?.payload?.mode).toBe("fake");
		expect(recordedEvent?.payload?.mode).toBe("fake");
		expect(recordedEvent?.payload?.status).toBe("failed");
		expect(recordedEvent?.payload?.blockedReason).toBeUndefined();
		expect(recordedEvent?.payload?.stderrRef?.kind).toBe("stderr");

		const customEntries = getCustomEntries(harness);
		const recordedEntry = customEntries.find((entry) => entry.customType === "rck-bridge.hermes.recorded");
		expect(recordedEntry).toBeDefined();
		expect((recordedEntry?.data as { mode?: string; status?: string } | undefined)?.mode).toBe("fake");
		expect((recordedEntry?.data as { status?: string } | undefined)?.status).toBe("failed");
		expect((recordedEntry?.data as { stdoutRef?: unknown } | undefined)?.stdoutRef).toBeUndefined();
		expect((recordedEntry?.data as { stderrRef?: { kind?: string; path?: string } } | undefined)?.stderrRef?.kind).toBe(
			"stderr",
		);

		const stderrFile = stderrFiles[0];
		const stderrContent = readFileSync(join(stderrDir, stderrFile), "utf-8");
		expect(stderrContent).toContain("mock-hermes-stderr: inspect mock bridge fail");
		expect((recordedEntry?.data as { stderrRef?: { path?: string } } | undefined)?.stderrRef?.path).toBe(
			`.pi/rck/evidence/hermes/stderr/${stderrFile}`,
		);

		const customMessages = getCustomMessages(harness);
		const statusMessage = customMessages.find(
			(message) => message.customType === "rck-bridge-status" && String(message.content).includes("Hermes fake run recorded"),
		);
		expect(statusMessage).toBeDefined();
		expect(String(statusMessage?.content)).not.toContain(stderrContent);
		expect(String(statusMessage?.content)).not.toMatch(/mock-hermes-stdout|mock-hermes-stderr|stdout|stderr/i);
	});

	it("executes /hermes --mode real and blocks real execution by default", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();

		await runner!.getCommand("hermes")!.handler("--mode real inspect mock bridge", runner!.createCommandContext());

		const rckRoot = join(harness.tempDir, ".pi", "rck");
		const stdoutDir = join(rckRoot, "evidence", "hermes", "stdout");
		const stderrDir = join(rckRoot, "evidence", "hermes", "stderr");
		const eventDir = join(rckRoot, "events");
		expect(existsSync(stdoutDir) ? readdirSync(stdoutDir) : []).toHaveLength(0);
		expect(existsSync(stderrDir) ? readdirSync(stderrDir) : []).toHaveLength(0);
		expect(readdirSync(eventDir)).toHaveLength(2);


		const eventRecords = readdirSync(eventDir).map((file) =>
			JSON.parse(readFileSync(join(eventDir, file), "utf-8")) as {
				eventType: string;
				traceId: string;
				requestEventId?: string;
				payload?: { mode?: string; status?: string; blockedReason?: string; requestEventId?: string };
			},
		);
		const requestedEvent = eventRecords.find((event) => event.eventType === "HermesRunRequested");
		const recordedEvent = eventRecords.find((event) => event.eventType === "HermesRunRecorded");
		const currentTrace = JSON.parse(readFileSync(join(rckRoot, "indexes", "current-trace.json"), "utf-8")) as {
			traceId: string;
			headAnchorId: string | null;
			anchorCount: number;
		};
		expect(requestedEvent?.traceId).toBe(currentTrace.traceId);
		expect(recordedEvent?.traceId).toBe(currentTrace.traceId);
		expect(requestedEvent?.payload?.mode).toBe("real");
		expect(recordedEvent?.payload?.mode).toBe("real");
		expect(recordedEvent?.payload?.status).toBe("aborted");
		expect(recordedEvent?.payload?.blockedReason).toBe("real-mode-disabled");
		expect(recordedEvent?.requestEventId).toBeDefined();
		expect(recordedEvent?.payload?.requestEventId).toBe(recordedEvent?.requestEventId);

		const customEntries = getCustomEntries(harness);
		const recordedEntry = customEntries.find((entry) => entry.customType === "rck-bridge.hermes.recorded");
		expect(recordedEntry).toBeDefined();
		expect((recordedEntry?.data as { mode?: string; status?: string } | undefined)?.mode).toBe("real");
		expect((recordedEntry?.data as { mode?: string; status?: string } | undefined)?.status).toBe("aborted");
		expect((recordedEntry?.data as { blockedReason?: string } | undefined)?.blockedReason).toBe("real-mode-disabled");
		expect((recordedEntry?.data as { stdoutRef?: unknown; stderrRef?: unknown } | undefined)?.stdoutRef).toBeUndefined();
		expect((recordedEntry?.data as { stdoutRef?: unknown; stderrRef?: unknown } | undefined)?.stderrRef).toBeUndefined();
		expect((recordedEntry?.data as { safeSummary?: string } | undefined)?.safeSummary).toContain(
			"blockedReason=real-mode-disabled",
		);

		const customMessages = getCustomMessages(harness);
		const statusMessage = customMessages.find(
			(message) =>
				message.customType === "rck-bridge-status" &&
				typeof message.content === "string" &&
				message.content.includes("Hermes real run recorded"),
		);
		expect(statusMessage).toBeDefined();
		expect(String(statusMessage?.content)).not.toMatch(/mock-hermes-stdout|mock-hermes-stderr|stdout|stderr/i);
		expect((statusMessage?.details as { mode?: string; status?: string; blockedReason?: string } | undefined)?.mode).toBe(
			"real",
		);
		expect((statusMessage?.details as { mode?: string; status?: string; blockedReason?: string } | undefined)?.status).toBe(
			"aborted",
		);
		expect((statusMessage?.details as { mode?: string; status?: string; blockedReason?: string } | undefined)?.blockedReason).toBe(
			"real-mode-disabled",
		);
	});
});
