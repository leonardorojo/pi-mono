import { afterEach, describe, expect, it } from "vitest";
import registerRckBridge from "../../../../.pi/extensions/rck-bridge/index.ts";
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
					message.content.includes("Mock state created: Mock state snapshot for current branch"),
			),
		).toBe(true);
	});

	it("executes /rck inject and records a safe mock context pack", async () => {
		harness = await createHarnessWithExtensions({
			extensionFactories: [{ path: "<rck-bridge>", factory: registerRckBridge }],
		});

		const runner = harness.session.extensionRunner;
		expect(runner).toBeDefined();
		expect(runner!.getCommand("rck")).toBeDefined();

		await runner!.getCommand("rck")!.handler("inject", runner!.createCommandContext());

		const customEntries = getCustomEntries(harness);
		const contextEntry = customEntries.find((entry) => entry.customType === "rck-bridge.context.injected.record");
		expect(contextEntry).toBeDefined();
		expect((contextEntry?.data as { eventType?: string } | undefined)?.eventType).toBe("ContextPackInjected");

		const customMessages = getCustomMessages(harness);
		const injectedMessage = customMessages.find((message) => message.customType === "rck-bridge.context.injected");
		expect(injectedMessage).toBeDefined();
		expect(typeof injectedMessage?.content === "string").toBe(true);
		expect(String(injectedMessage?.content)).toContain("MOCK CONTEXT PACK:");
		expect(String(injectedMessage?.content)).not.toMatch(/stdout|stderr|diff|log/i);
		expect((injectedMessage?.details as { mock?: boolean } | undefined)?.mock).toBe(true);
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
