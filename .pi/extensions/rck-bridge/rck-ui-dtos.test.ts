import { describe, expect, it } from "vitest";
import {
	createAnchorResultDto,
	createInjectContextResultDto,
	createRckAdapterErrorDto,
	createStateResultDto,
	normalizeHermesRunResultDto,
	normalizeRckInventoryDto,
	normalizeRckStatusDto,
	normalizeRckSupervisionDto,
	type RckAdapterErrorDto,
} from "./rck-ui-dtos.js";
import type { RckEventPayload } from "./rck-storage.js";
import type { RckSupervisionEvaluation } from "./rck-supervision.js";

describe("rck-ui-dtos", () => {
	const sampleHermesRecordedEvent: RckEventPayload = {
		schemaVersion: "0.1",
		artifactType: "rck.event",
		id: "evt_recorded_1",
		traceId: "trace_1",
		createdAt: "2026-05-10T09:00:00.000Z",
		repoPath: "/home/rufus/DEV/leonardorojo/pi-mono",
		cwd: "/home/rufus/DEV/leonardorojo/pi-mono",
		piSessionId: "session_1",
		piEntryId: null,
		parentPiEntryId: null,
		branchId: null,
		summary: "Hermes run recorded",
		actor: "extension",
		tags: ["rck-bridge", "fake", "hermes", "recorded"],
		correlation: { traceId: "trace_1", requestEventId: "evt_requested_1" },
		piWriteTarget: "entry",
		rckWriteTarget: "rck",
		llmInjectionPolicy: "none",
		eventId: "evt_recorded_1",
		eventType: "HermesRunRecorded",
		requestEventId: "evt_requested_1",
		resultSummary: "Hermes fake run succeeded",
		exitCode: 0,
		mode: "fake",
		status: "succeeded",
		timedOut: false,
		durationMs: 21,
		safeSummary: "mode=fake status=succeeded durationMs=21",
		stdoutRef: {
			artifactId: "artifact_stdout_1",
			kind: "stdout",
			path: ".pi/rck/evidence/hermes/stdout/artifact_stdout_1.log",
			byteLength: 18,
		},
		stderrRef: {
			artifactId: "artifact_stderr_1",
			kind: "stderr",
			path: ".pi/rck/evidence/hermes/stderr/artifact_stderr_1.log",
			byteLength: 0,
		},
		stdoutByteLength: 18,
		stderrByteLength: 0,
		stdoutTruncated: false,
		stderrTruncated: false,
		payload: {
			runId: "run_1",
			requestEventId: "evt_requested_1",
			mode: "fake",
			status: "succeeded",
			exitCode: 0,
			timedOut: false,
			durationMs: 21,
			stdoutRef: {
				artifactId: "artifact_stdout_1",
				kind: "stdout",
				path: ".pi/rck/evidence/hermes/stdout/artifact_stdout_1.log",
				byteLength: 18,
			},
			stderrRef: {
				artifactId: "artifact_stderr_1",
				kind: "stderr",
				path: ".pi/rck/evidence/hermes/stderr/artifact_stderr_1.log",
				byteLength: 0,
			},
			stdoutTruncated: false,
			stderrTruncated: false,
			stdoutByteLength: 18,
			stderrByteLength: 0,
			safeSummary: "mode=fake status=succeeded durationMs=21",
		},
	};

	it("normalizes status, inventory, and supervision DTOs from bridge outputs", () => {
		const status = normalizeRckStatusDto({
			currentTrace: {
				schemaVersion: "rck.current-trace/v0.1",
				artifactType: "rck.index.current-trace",
				traceId: "trace_1",
				createdAtUtc: "2026-05-10T08:59:00.000Z",
				updatedAtUtc: "2026-05-10T09:00:00.000Z",
				headAnchorId: "anchor_1",
				anchorCount: 1,
			},
			latestStateIndex: {
				schemaVersion: "0.1",
				artifactType: "rck.index.latest-state",
				currentStateId: "state_1",
				currentStatePath: ".pi/rck/states/state_1.json",
				currentEventId: "evt_state_1",
				currentEventPath: ".pi/rck/events/evt_state_1.json",
				traceId: "trace_1",
				updatedAt: "2026-05-10T08:59:30.000Z",
				updatedByEventId: "evt_state_1",
			},
			latestState: {
				schemaVersion: "0.1",
				artifactType: "rck.state",
				id: "state_file_1",
				traceId: "trace_1",
				createdAt: "2026-05-10T08:59:30.000Z",
				repoPath: "/home/rufus/DEV/leonardorojo/pi-mono",
				cwd: "/home/rufus/DEV/leonardorojo/pi-mono",
				piSessionId: "session_1",
				piEntryId: null,
				parentPiEntryId: null,
				branchId: "branch_1",
				summary: "State created",
				actor: "extension",
				correlation: { traceId: "trace_1", requestEventId: "evt_state_1" },
				piWriteTarget: "entry",
				rckWriteTarget: "rck",
				llmInjectionPolicy: "safe-summary",
				stateId: "state_1",
				stateType: "operational",
				stateSummary: {
					title: "Plan",
					objective: "Normalize bridge outputs",
					scope: "docs only",
					nextAction: "Run smoke",
				},
				source: { eventId: "evt_state_1", command: "/state" },
			},
			latestContextPackIndex: {
				schemaVersion: "0.1",
				artifactType: "rck.index.latest-context-pack",
				currentContextPackId: "pack_1",
				currentContextPackPath: ".pi/rck/context-packs/pack_1.json",
				currentEventId: "evt_context_1",
				currentEventPath: ".pi/rck/events/evt_context_1.json",
				stateId: "state_1",
				statePath: ".pi/rck/states/state_1.json",
				traceId: "trace_1",
				updatedAt: "2026-05-10T09:00:00.000Z",
				updatedByEventId: "evt_context_1",
			},
			latestContextPack: {
				schemaVersion: "0.1",
				artifactType: "rck.context-pack",
				id: "context_file_1",
				traceId: "trace_1",
				createdAt: "2026-05-10T09:00:00.000Z",
				repoPath: "/home/rufus/DEV/leonardorojo/pi-mono",
				cwd: "/home/rufus/DEV/leonardorojo/pi-mono",
				piSessionId: "session_1",
				piEntryId: null,
				parentPiEntryId: null,
				branchId: "branch_1",
				summary: "Context pack created",
				actor: "extension",
				correlation: { traceId: "trace_1", requestEventId: "evt_state_1" },
				piWriteTarget: "custom_message",
				rckWriteTarget: "llm",
				llmInjectionPolicy: "safe-context",
				contextPackId: "pack_1",
				contextPackType: "safe-summary",
				stateId: "state_1",
				statePath: ".pi/rck/states/state_1.json",
				allowedToInject: true,
				stateSummary: {
					title: "Plan",
					objective: "Normalize bridge outputs",
					scope: "docs only",
					nextAction: "Run smoke",
				},
				contextSummary: {
					title: "Plan",
					objective: "Normalize bridge outputs",
					scope: "docs only",
					nextAction: "Run smoke",
				},
			},
			latestAnchorIndex: {
				schemaVersion: "0.1",
				artifactType: "rck.index.latest-anchor",
				currentAnchorId: "anchor_1",
				currentAnchorPath: ".pi/rck/anchors/anchor_1.json",
				currentEventId: "evt_anchor_1",
				currentEventPath: ".pi/rck/events/evt_anchor_1.json",
				traceId: "trace_1",
				updatedAt: "2026-05-10T09:00:10.000Z",
				updatedByEventId: "evt_anchor_1",
			},
			latestAnchor: {
				schemaVersion: "0.1",
				artifactType: "rck.anchor",
				id: "anchor_file_1",
				traceId: "trace_1",
				createdAt: "2026-05-10T09:00:10.000Z",
				repoPath: "/home/rufus/DEV/leonardorojo/pi-mono",
				cwd: "/home/rufus/DEV/leonardorojo/pi-mono",
				piSessionId: "session_1",
				piEntryId: null,
				parentPiEntryId: null,
				branchId: "branch_1",
				summary: "Anchor registered",
				actor: "extension",
				correlation: { traceId: "trace_1", requestEventId: "evt_anchor_1" },
				piWriteTarget: "entry",
				rckWriteTarget: "rck",
				llmInjectionPolicy: "safe-summary",
				anchorId: "anchor_1",
				anchorName: "phase-7c-start",
				stateId: "state_1",
				statePath: ".pi/rck/states/state_1.json",
			},
			latestHermesRun: sampleHermesRecordedEvent,
			generatedAt: "2026-05-10T09:01:00.000Z",
		});

		expect(status.traceId).toBe("trace_1");
		expect(status.currentTrace).toMatchObject({ traceId: "trace_1", headAnchorId: "anchor_1", anchorCount: 1 });
		expect(status.latestState).toMatchObject({
			stateId: "state_1",
			statePath: ".pi/rck/states/state_1.json",
			eventId: "evt_state_1",
			safeSummary: "Plan: Normalize bridge outputs | scope=docs only | next=Run smoke",
		});
		expect(status.latestContextPack).toMatchObject({
			contextPackId: "pack_1",
			stateId: "state_1",
			eventId: "evt_context_1",
			safeSummary: expect.stringContaining("Context pack created"),
		});
		expect(status.latestAnchor).toMatchObject({
			anchorId: "anchor_1",
			label: "phase-7c-start",
		});
		expect(status.latestHermesRun).toMatchObject({
			recordedEventId: "evt_recorded_1",
			requestedEventId: "evt_requested_1",
			runId: "run_1",
			status: "succeeded",
		});
		expect(status.latestHermesRun?.evidenceRefs).toHaveLength(2);
		expect(status.generatedAt).toBe("2026-05-10T09:01:00.000Z");

		const inventory = normalizeRckInventoryDto({
			traceId: "trace_1",
			counts: {
				states: 1,
				contextPacks: 1,
				anchors: 1,
				events: 4,
			},
			latestEvents: [sampleHermesRecordedEvent],
			latestHermesRun: sampleHermesRecordedEvent,
			generatedAt: "2026-05-10T09:01:00.000Z",
		});

		expect(inventory.counts).toEqual({ states: 1, contextPacks: 1, anchors: 1, events: 4, hermesRuns: 1 });
		expect(inventory.latestEvents[0]).toMatchObject({
			eventId: "evt_recorded_1",
			eventType: "HermesRunRecorded",
			summary: "mode=fake status=succeeded durationMs=21",
		});
		expect(inventory.latestHermesRun?.safeSummary).toContain("mode=fake");

		const supervision: RckSupervisionEvaluation = {
			level: "ok",
			reason: "Latest Hermes run succeeded without supervision flags",
			recommendedAction: "No action needed",
			needsAttention: false,
			traceId: "trace_1",
			latestRunId: "run_1",
			latestEventId: "evt_recorded_1",
			signals: {
				status: "succeeded",
				durationMs: 21,
			},
		};

		expect(normalizeRckSupervisionDto({ evaluation: supervision, generatedAt: "2026-05-10T09:01:00.000Z" })).toEqual({
			traceId: "trace_1",
			level: "ok",
			reason: "Latest Hermes run succeeded without supervision flags",
			recommendedAction: "No action needed",
			needsAttention: false,
			latestRunId: "run_1",
			latestEventId: "evt_recorded_1",
			signals: {
				status: "succeeded",
				durationMs: 21,
			},
			generatedAt: "2026-05-10T09:01:00.000Z",
		});
	});

	it("normalizes Hermes runs, evidence refs, and adapter errors without raw output", () => {
		const hermesRun = normalizeHermesRunResultDto(sampleHermesRecordedEvent);

		expect(hermesRun).toMatchObject({
			traceId: "trace_1",
			runId: "run_1",
			requestedEventId: "evt_requested_1",
			recordedEventId: "evt_recorded_1",
			status: "succeeded",
			exitCode: 0,
			durationMs: 21,
			safeSummary: "mode=fake status=succeeded durationMs=21",
		});
		expect(hermesRun.evidenceRefs).toEqual([
			{
				kind: "stdout",
				refId: "artifact_stdout_1",
				path: ".pi/rck/evidence/hermes/stdout/artifact_stdout_1.log",
				isRaw: false,
				displayPolicy: "reference-only",
			},
			{
				kind: "stderr",
				refId: "artifact_stderr_1",
				path: ".pi/rck/evidence/hermes/stderr/artifact_stderr_1.log",
				isRaw: false,
				displayPolicy: "reference-only",
			},
		]);
		expect((hermesRun as { stdout?: unknown; stderr?: unknown }).stdout).toBeUndefined();
		expect((hermesRun as { stdout?: unknown; stderr?: unknown }).stderr).toBeUndefined();

		expect(
			createStateResultDto({
				traceId: "trace_1",
				stateId: "state_1",
				eventId: "evt_state_1",
				safeSummary: "state created",
				generatedAt: "2026-05-10T09:01:00.000Z",
			}),
		).toEqual({
			traceId: "trace_1",
			stateId: "state_1",
			eventId: "evt_state_1",
			safeSummary: "state created",
			generatedAt: "2026-05-10T09:01:00.000Z",
		});

		expect(
			createInjectContextResultDto({
				traceId: "trace_1",
				contextPackId: "pack_1",
				eventId: "evt_context_1",
				safeSummary: "context injected",
				generatedAt: "2026-05-10T09:01:00.000Z",
			}),
		).toEqual({
			traceId: "trace_1",
			contextPackId: "pack_1",
			eventId: "evt_context_1",
			safeSummary: "context injected",
			generatedAt: "2026-05-10T09:01:00.000Z",
		});

		expect(
			createAnchorResultDto({
				traceId: "trace_1",
				anchorId: "anchor_1",
				eventId: "evt_anchor_1",
				label: "phase-7c-start",
				safeSummary: "anchor created",
				generatedAt: "2026-05-10T09:01:00.000Z",
			}),
		).toEqual({
			traceId: "trace_1",
			anchorId: "anchor_1",
			eventId: "evt_anchor_1",
			label: "phase-7c-start",
			safeSummary: "anchor created",
			generatedAt: "2026-05-10T09:01:00.000Z",
		});

		const errorDto: RckAdapterErrorDto = createRckAdapterErrorDto({
			code: "COMMAND_FAILED",
			message: "Command failed",
			source: "rck-bridge",
			command: "/rck status",
			recoverable: true,
			recommendedAction: "Retry after bridge recovers",
			generatedAt: "2026-05-10T09:01:00.000Z",
		});

		expect(errorDto).toEqual({
			code: "COMMAND_FAILED",
			message: "Command failed",
			source: "rck-bridge",
			command: "/rck status",
			recoverable: true,
			recommendedAction: "Retry after bridge recovers",
			generatedAt: "2026-05-10T09:01:00.000Z",
		});
	});
});
