# RFS TUI Recording Contract

## Status

PT4.
Design-only.
No runtime changes in this phase.

This document defines how a finalized TUI interaction is recorded once the main LLM responds.
It does not implement recording.
It does not touch `Rufus.RCK.Core`.
It does not write RCK.
It does not introduce a new Core payload.

## 1. Purpose

RFS TUI is a cognitive repo session.
The recording contract exists to capture one completed interaction as a single State + Delta pair after the main LLM response is available.

Scope:

- prompt lifecycle
- mode selection lifecycle
- internal pipeline lifecycle
- final main LLM response lifecycle
- controlled State + Delta projection

Non-scope:

- prompt-entry recording
- mode-selection recording
- per-step internal recording
- raw runtime trace persistence
- runtime behavior changes

## 2. Current RCK recording mechanisms audited

Audited components:

- `RckInteractionRecorder`
- `RckInteractionRecord`
- `RckAgentTaskRecorder`
- `RckAgentTaskRecordInput`
- `RckIntentProjection`
- `RckWorkspaceLogReader`
- `RckWorkspaceContextPackReader`

Current findings:

- `RckInteractionRecorder` already persists `rufus.interaction-state` and `rufus.interaction-delta` inside the standard RCK `State` / `Delta` wrappers.
- `RckInteractionRecord` currently holds a small interaction surface: mode, prompt, answer, answer summary, and tools.
- `RckAgentTaskRecorder` is a separate projection path for agent-task execution and uses `rufus.agent-task-state` / `rufus.agent-task-delta`.
- `RckAgentTaskRecordInput` is execution-centric, not a TUI interaction contract.
- `RckIntentProjection` already shows the preferred pattern for bounded nested structured output.
- `RckWorkspaceLogReader` and `RckWorkspaceContextPackReader` already decode interaction fields generically from `interaction`, `git`, and `artifacts` blocks.

Conclusion from the audit:

- the existing interaction payload family is the right boundary for TUI recording;
- no new Core payload family is required;
- agent-task payloads are the wrong semantic fit for TUI final-response recording.

## 3. Decision: reuse existing interaction payloads or justify otherwise

Decision:

- reuse `rufus.interaction-state` and `rufus.interaction-delta`;
- do not create a new Core payload type;
- do not reuse `rufus.agent-task-state` / `rufus.agent-task-delta` for TUI final-response recording.

Why:

- the TUI interaction is still an LLM interaction, not an agent-task result;
- the existing interaction payloads already carry prompt, answer summary, Git context, artifact metadata, and delta evidence in a compatible way;
- the existing readers already understand that shape;
- a new payload would add schema surface without solving a current boundary problem.

Recommended Workspace shape for PT5:

- keep the payload family the same;
- add TUI-specific metadata as optional nested fields inside the existing interaction payload;
- keep the control surface in Workspace, not in Core.

## 4. When TUI recording happens

Record only when the main LLM has responded.

Do not record:

- when the prompt is typed
- when the mode is chosen
- when the internal pipeline advances through selection / validation / context materialization steps
- when the user runs informational commands such as `/status`, `/help`, `/log`, or `/exit`

One finalized TUI interaction produces:

- one new State
- one new Delta
- one HEAD move to the new State

## 5. State / Delta semantics

State semantics:

- the State is the post-response cognitive snapshot;
- it represents the whole interaction after the pipeline has finished;
- it is not a log of intermediate steps.

Delta semantics:

- the Delta describes the transition that produced the new State;
- it summarizes the complete prompt -> mode -> pipeline -> response sequence;
- it does not create one state per internal step;
- it does not encode the internal pipeline as separate persisted states.

Controlled data rule:

- record summaries and refs, not raw dumps;
- keep the State and Delta small enough to inspect safely in logs.
- preserve the existing `createdAtUtc` timestamps in the State / Delta envelope meta blocks;
- surface main-LLM `provider` / `model` metadata only when it is available and bounded.

## 6. Anchor semantics

Anchor is not created for each TUI interaction.

Anchors are created only on:

- init / genesis
- git commit change
- explicit `/anchor`

The commit-boundary anchor remains structural and automatic.
It is not a per-step interaction marker.

## 7. TUI modes and recording shape

The persisted interaction `mode` should be one of:

- `tui-direct`
- `tui-simple`
- `tui-complete`
- `tui-plan`

### `tui-direct`

- `pipelineKind = direct`
- no TraceSlice
- no ContextPack
- no validation
- no selection summary

### `tui-simple`

- `pipelineKind = simple`
- `lastInteractionCount`
- `selectedRecentStateIds` when applicable
- `selectedRecentDeltaIds` when applicable
- artifact refs are metadata-only
- `contextSummary`

### `tui-complete`

- `pipelineKind = complete`
- `intentSummary`
- `proposalSummary`
- `validationStatus`
- `traceSliceSelectionSummary`
- `contextPackScope`
- `selectedStateIds`
- `selectedDeltaIds`
- `selectedAnchorIds`
- `materializationPolicy`
- warnings and errors summarized if needed

### `tui-plan`

- `pipelineKind = plan`
- `contextMode = simple | complete`
- `planSummary`
- no code changes
- no patch applied

## 8. Pipeline summary shape

Use an optional controlled block such as `interaction.pipelineSummary`.

This block should stay small and structured.

Minimum conceptual fields:

- `pipelineKind`
- `contextMode` when relevant
- `summary`
- `model` when available:
  - `provider`
  - `name`
- `warnings`
- `errors`
- `refs` or short refs-by-id only

Mode-specific additions:

- direct: only a short summary of the single-shot response path
- simple: recent-state / recent-delta selection metadata plus a short context summary
- complete: intent, proposal, validation, trace-slice selection, context-pack scope, and materialization policy summaries
- plan: planning summary plus the chosen context mode

Do not store:

- full TraceSlice contents when refs are enough
- full ContextPack contents when refs are enough
- raw tool output
- raw prompt internals
- unbounded arrays

## 9. Forbidden persisted data

Do not persist:

- raw JSONL
- stdout / stderr dumps
- file contents
- diffs
- full ContextPack blobs when a summary and refs are enough
- full TraceSlice blobs when refs are enough
- provider event streams
- secrets
- large internal prompts
- per-step internal pipeline transcripts
- arbitrary blobs disguised as summaries

## 10. Compatibility with log / context-pack

Compatibility target:

- `rfs log` and `rfs context-pack` must continue to work with the existing interaction payload family.
- current readers already inspect `interaction.mode`, `interaction.prompt`, `interaction.answerSummary`, `git`, and `artifacts`.
- current delta readers already inspect `cause.mode`, `cause.prompt`, `cause.answer`, and evidence counts.

Compatibility rule:

- keep the existing interaction payload type names;
- keep unknown fields optional;
- add new nested metadata only if older readers can safely ignore it.

## 11. Implementation guidance for PT5

Recommended implementation shape:

- extend `RckInteractionRecorder` with a TUI-specific entrypoint rather than inventing a new Core payload;
- add a TUI-specific input object in Workspace, such as `TuiInteractionRecordInput` or `RckTuiInteractionRecordInput`;
- keep the shared persistence helpers in one place;
- reuse the existing repo-root / HEAD / Git / artifact-ref logic;
- record exactly once, when the main LLM responds;
- keep pipeline summary fields optional and bounded;
- keep `RckWorkspaceLogReader` and `RckWorkspaceContextPackReader` tolerant of unknown nested fields.

Recorder choice:

- preferred: extend `RckInteractionRecorder` with a dedicated TUI method;
- not required: a separate `RckTuiInteractionRecorder` class;
- create a separate recorder only if the API later diverges enough that the generic interaction recorder becomes misleading.

## 12. Non-goals

This contract does not:

- implement runtime recording
- change TUI behavior
- add a new Core payload family
- touch `Rufus.RCK.Core`
- write RCK now
- record prompt entry
- record mode selection as its own persisted state
- record per-step internal pipeline states
- persist raw context packs, traces, or tool outputs
- change log/context-pack compatibility surface
- introduce RCK schema migrations
