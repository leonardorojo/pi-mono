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
		};
		expect(latestContext.currentContextPackId).toContain("pack_");
		expect(latestContext.currentContextPackPath).toMatch(/^\.pi\/rck\/context-packs\//);
		expect(latestContext.currentEventId).toContain("evt_");
		expect(latestContext.statePath).toMatch(/^\.pi\/rck\/states\//);

		const contextPackJson = JSON.parse(readFileSync(join(rckRoot, "context-packs", contextPackFiles[0]), "utf-8")) as {
			artifactType: string;
			allowedToInject: boolean;
			contextPackId: string;
			stateId: string;
			statePath: string;
			contextSummary: { objective: string };
		};
		expect(contextPackJson.artifactType).toBe("rck.context-pack");
		expect(contextPackJson.allowedToInject).toBe(true);
		expect(contextPackJson.stateId).toBe(latestContext.stateId);
		expect(contextPackJson.statePath).toBe(latestContext.statePath);

		const injectEventFile = eventFiles.find((file) => {
			const parsed = JSON.parse(readFileSync(join(rckRoot, "events", file), "utf-8")) as { eventType?: string };
			return parsed.eventType === "ContextPackInjected";
		});
		expect(injectEventFile).toBeDefined();
		const injectEventJson = JSON.parse(readFileSync(join(rckRoot, "events", injectEventFile as string), "utf-8")) as {
			eventType: string;
			payload: { contextPackId: string; contextPackPath: string; stateId: string };
		};
		expect(injectEventJson.eventType).toBe("ContextPackInjected");
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

	it("executes /hermes inspect mock bridge and records both mock Hermes events", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();
		expect(runner!.getCommand("hermes")).toBeDefined();

		await runner!.getCommand("hermes")!.handler("inspect mock bridge", runner!.createCommandContext());

		const customEntries = getCustomEntries(harness);
		const requestedEntry = customEntries.find((entry) => entry.customType === "rck-bridge.hermes.requested");
		const recordedEntry = customEntries.find((entry) => entry.customType === "rck-bridge.hermes.recorded");

		expect(requestedEntry).toBeDefined();
		expect((requestedEntry?.data as { eventType?: string } | undefined)?.eventType).toBe("HermesRunRequested");
		expect(recordedEntry).toBeDefined();
		expect((recordedEntry?.data as { eventType?: string; exitCode?: number } | undefined)?.eventType).toBe("HermesRunRecorded");
		expect((recordedEntry?.data as { eventType?: string; exitCode?: number } | undefined)?.exitCode).toBe(0);

		const customMessages = getCustomMessages(harness);
		const statusMessage = customMessages.find((message) => message.customType === "rck-bridge-status");
		expect(statusMessage).toBeDefined();
		expect(String(statusMessage?.content)).toContain("Mock Hermes completed: emitted HermesRunRequested and HermesRunRecorded");
		expect((statusMessage?.details as { mock?: boolean } | undefined)?.mock).toBe(true);
		expect((statusMessage?.details as { eventType?: string } | undefined)?.eventType).toBe("HermesRunRecorded");
	});
});
