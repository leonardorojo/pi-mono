# Rufus.Agenting -> RCK Recording Contract v0

## Status

Contract v0.
Design. Implemented.
The `rfs intent --record` command implements this contract via
`RckAgentTaskRecorder.RecordIntent()` in `Program.cs`.

This document defines the concrete contract. The implementation
does not modify `Rufus.RCK.Core`.
It does not modify `Rufus.Agenting` execution semantics.

Current architectural split remains unchanged:

- RFS orchestrates.
- `Rufus.Agenting` executes `AgentTask` and returns `AgentTaskResult`.
- `Rufus.RCK.Core` models/remembers/persists canonical RCK objects.
- `Rufus.RCK.Workspace` adapts filesystem/git/.rfs and local persistence.
- `Rufus.Agenting` does not write RCK directly.
- `Rufus.RCK.Core` does not depend on `Rufus.Agenting`.
- There is no `ModelRouter`.
- There is no runtime model selection.
- Provider/model belongs to the agent or its `ExecutionModel`.

Primary current target:

- existing `IntentInferenceAgent`
- existing `IAgent`
- existing `AgentTask`
- existing `AgentTaskResult`
- existing `AgentEvidence`
- existing `rfs intent "<prompt>"`
- existing `tools/rfs/docs/RUFUS_AGENTING_RCK_RECORDING.md`

This Contract v0 is the authoritative design note for the first recording microphase.

## 1. What Contract v0 is

Contract v0 defines a controlled projection from `AgentTaskResult` into existing RCK State/Delta recording patterns.

Core position:

1. `AgentTaskResult` can be projected to RCK.
2. The projection is controlled by RFS / Workspace, not by the agent itself.
3. The raw `AgentTaskResult` is not persisted as-is.
4. Raw logs/events are not persisted as-is.
5. The first supported case is `IntentInferenceAgent`.

Implications:

- `AgentTaskResult` is an execution contract.
- RCK State/Delta is a cognitive memory contract.
- Projection is selective, bounded, and normalized.
- Projection is allowed to keep only a small summary of input/output/evidence.
- Projection must remain engine-agnostic even if future agents produce richer runtime traces.

Out of scope for v0:

- persisting full raw `AgentTaskResult`
- persisting raw event streams
- persisting raw JSONL
- persisting stdout/stderr
- introducing agent-specific schema into `Rufus.RCK.Core`

## 2. What kind of record `rfs intent --record` creates

Decision for v0:

`rfs intent --record` must create a new State plus a new Delta, following the same high-level RCK chain behavior as current `ask --record` / `agent --record`.

Rationale:

- it keeps the active chain moving forward;
- it makes the run inspectable in history;
- it preserves the existing mental model of RCK progression;
- it avoids a separate side-channel store for agent-task results.

Contract meaning:

- the new State represents the cognitive snapshot after the agent execution has completed and after RFS has accepted a controlled projection of that result;
- the new Delta represents the transition caused by that execution;
- the Delta explains why the state changed;
- the State captures the accepted post-execution snapshot.

Important boundaries:

- this does not require `Rufus.RCK.Core` schema changes;
- this should reuse existing State/Delta primitives already available in Core;
- this should be implemented at the Workspace/orchestrator layer;
- the agent run does not become a raw blob stored in the DAG.

Implementation position for v0:

- reuse the existing RCK pattern of `State + Delta + HEAD update`;
- keep the projection payload inside existing JSON payloads carried by `RckState` and `RckDelta`;
- do not change Core model types to add an `AgentTaskResult` concept.

## 3. State payload v0

The new State payload must be concrete, controlled, and small.

Recommended conceptual payload:

```json
{
  "type": "rufus.agent-task-state",
  "schemaVersion": 1,
  "agentTask": {
    "taskId": "...",
    "taskKind": "infer-intent",
    "agentId": "intent-inference",
    "status": "Succeeded",
    "summary": "Deterministic mock intent inference classified the prompt as 'build-context-pack'."
  },
  "execution": {
    "provider": "mock",
    "model": "deterministic-v1"
  },
  "output": {
    "kind": "intent",
    "summary": "Prompt classified as build-context-pack.",
    "data": {
      "intent": "build-context-pack",
      "entities": ["ContextPack"],
      "constraints": ["Do not write raw JSONL into RCK."]
    }
  },
  "git": {
    "branch": "feature/rufus-cli-design",
    "commit": "abc123...",
    "dirty": true
  },
  "artifacts": []
}
```

Required rules:

1. `type` must clearly identify this as an agent-task-derived state payload.
2. `schemaVersion` starts at `1`.
3. `agentTask.summary` is a compact accepted summary, not a raw trace.
4. `execution.provider` and `execution.model` come from the agent descriptor / execution model.
5. `output.kind` declares the projection shape.
6. `output.summary` is always present.
7. `output.data` must be controlled and small.
8. `git` preserves current branch/commit/dirty context.
9. `artifacts` lists detected changed artifacts if the Workspace recorder chooses to reuse existing artifact detection logic.

Rules for `output.data` in v0:

- it must be bounded;
- it must be semantically useful;
- it must not contain raw logs;
- it must not contain full prompt transcripts if they are long;
- it must not contain stdout/stderr;
- it must not contain file content;
- it must not contain diffs;
- it must not contain provider event dumps.

For `IntentInferenceAgent`, `output.data` may contain a controlled subset of `PromptIntent`, for example:

- `intent`
- short `entities`
- short `constraints`

For future agents, `output.data` may be one of these bounded forms:

- summary-only
- summary + tiny structured data
- summary + external refs only

Contract rule:

Future agents are not automatically entitled to store their full structured output. If the output is large or unstable, State payload must degrade to summary-only or summary-plus-ref.

## 4. Delta payload v0

The Delta payload must describe the transition and its cause.

Recommended conceptual payload:

```json
{
  "type": "rufus.agent-task-delta",
  "schemaVersion": 1,
  "change": {
    "summary": "Recorded a new agent task result for intent inference.",
    "fromStateId": "...",
    "toStateId": "...",
    "changes": [
      {
        "path": "/agentTask",
        "kind": "added",
        "summary": "Stored controlled result of IntentInferenceAgent execution."
      },
      {
        "path": "/git",
        "kind": "refreshed",
        "summary": "Captured current Git context."
      }
    ]
  },
  "cause": {
    "type": "agent-task",
    "taskId": "...",
    "taskKind": "infer-intent",
    "goal": "Infer the operational intent of the prompt.",
    "inputSummary": "User asked to implement rfs show command.",
    "agentId": "intent-inference",
    "executionModel": {
      "provider": "mock",
      "model": "deterministic-v1"
    }
  },
  "evidence": {
    "agent": {
      "status": "Succeeded",
      "summary": "Prompt classified as build-context-pack.",
      "warnings": [],
      "errors": []
    },
    "items": []
  }
}
```

Required rules:

1. `Delta.change` explains the recorded transition.
2. `Delta.cause` explains what caused the transition.
3. `Delta.evidence` stores controlled evidence, not raw runtime streams.
4. Large output must not be duplicated inside Delta if it is already represented in State.
5. Long prompt/input text must be summarized or truncated by policy.

Specific v0 interpretation:

- `change` says what changed in cognitive memory;
- `cause` says which agent task caused it;
- `evidence` says why the projection is trustworthy enough to persist.

Rules for `cause.goal` and `cause.inputSummary`:

- `goal` may be the task goal if short;
- `inputSummary` should be a short accepted summary of the input prompt;
- if the original prompt is long, do not store it raw in Delta;
- v0 prefers summary/truncation over copying the original full input.

Rules for `evidence`:

- `agent.status` mirrors the accepted task result status;
- `agent.summary` is compact;
- `warnings` and `errors` are short strings only;
- `items` may hold small controlled evidence descriptors;
- no raw logs;
- no raw event envelopes;
- no stdout/stderr;
- no tool call dumps.

## 5. Refs and evidenceRefs v0

Decision for v0:

For `IntentInferenceAgent`, `refs` and `evidenceRefs` will normally be empty or artifact-only.

Reason:

- the deterministic intent agent usually produces a small local result;
- it does not naturally require external artifact storage;
- the first implementation should stay minimal.

Allowed future use:

If a future agent references external artifacts, those may be represented through `refs` / `evidenceRefs`.

Examples of acceptable future refs:

- file artifact created or inspected by the workspace recorder
- external audit file retained outside the DAG
- normalized artifact path detected from git workspace context

Forbidden content for v0 refs/evidenceRefs:

- full file contents inline
- diffs inline
- raw JSONL inline
- raw stdout/stderr inline
- raw provider event streams inline

Contract rule:

`refs` / `evidenceRefs` are for reference semantics, not for smuggling large payloads into the DAG.

Artifact policy for v0:

- if artifact detection already exists in the current interaction recorder and can be reused safely, P13 may reuse it;
- artifact refs must still point to files/locations, not embed their content;
- for `IntentInferenceAgent`, no extra artifact machinery is required beyond what already exists.

## 6. Relationship with existing RCK interaction recording

Current baseline in Workspace:

- `ask --record`
- `agent --record`
- `RckInteractionRecorder`

Decision for v0:

Do not collapse `InteractionRecord` and `AgentTaskRecord` into the same semantic type if that makes the design ambiguous.

Recommended implementation shape:

Preferred:

- add a separate recorder in Workspace, for example `RckAgentTaskRecorder`

Acceptable alternative:

- add a clearly separate method such as `RckInteractionRecorder.RecordAgentTask(...)`

Not recommended:

- forcing agent-task recording to masquerade as an interaction record if that blurs the payload semantics or naming.

Reasoning:

1. current interaction recording models an LLM interaction surface;
2. agent-task recording models a controlled projection of `AgentTaskResult`;
3. both reuse the same underlying RCK primitives;
4. but their payload semantics are different enough to justify separation at the Workspace API boundary.

Concrete recommendation:

P13 should prefer a separate Workspace recorder class, named along the lines of:

- `RckAgentTaskRecorder`

because that keeps these distinctions explicit:

- interaction recording
- agent-task recording
- future trace-slice recording

However, implementation reuse is encouraged internally:

- repo root discovery
- HEAD loading
- previous state loading
- git context capture
- changed artifact detection
- state/delta persistence
- HEAD update
- optional commit-anchor logic

So the contract is:

- separate semantic API surface;
- shared lower-level persistence helpers where useful.

## 7. Expected CLI behavior for P13

Future CLI target:

`rfs intent --record "<prompt>"`

Required behavior:

1. execute `IntentInferenceAgent`;
2. print the same intent result that current `rfs intent` prints;
3. record one new State;
4. record one new Delta;
5. update `HEAD` to the new State;
6. preserve git context in the recorded payload;
7. optionally reuse current artifact detection logic if it fits cleanly;
8. not write raw logs/events;
9. not change the meaning of plain `rfs intent`.

Output behavior rule:

- `--record` must be additive to persistence, not a different user-facing inference result;
- stdout should remain aligned with current `rfs intent` output contract;
- recording side effects should not require a different result shape for the user.

HEAD behavior rule:

- after successful recording, `.rfs/rck/HEAD` must point to the new state;
- this follows the existing State/Delta chain pattern.

Git context rule:

- the recorded state must capture current branch/commit/dirty state;
- this mirrors the existing recorder behavior and keeps downstream inspection useful.

Artifact detection rule:

- if `RckInteractionRecorder` already contains safe artifact detection that is reusable without semantic distortion, P13 may reuse that logic;
- if not, P13 may omit artifacts for the first pass except for the already available git context.

Anchor rule:

- P13 should not create anchors by default just because this is an agent task;
- commit-anchor creation should only happen if the existing commit-anchor pattern already applies naturally through shared recorder behavior;
- no new agent-task-specific anchor concept is introduced in v0.

Fallback rule:

- no legacy fallback is required for v0;
- `IntentInferenceAgent` is deterministic and local;
- P13 does not need a legacy non-agent recording path for this command.

Failure rule:

- if agent execution fails, P13 should not invent partial raw persistence behavior in this first phase;
- the minimal safe implementation can simply not record on failed execution, or record only if it already has an explicitly designed failure payload;
- Contract v0 does not require failed-task persistence.

## 8. Acceptance criteria for P13

P13 acceptance should validate behavior in a real external repo initialized for RCK use.

Suggested command flow:

```bash
lrfs intent --record "Implement rfs show command"
lrfs log
lrfs context-pack > /tmp/context.json
python3 -m json.tool /tmp/context.json
```

Expected validations:

1. a new State was created;
2. a new Delta was created;
3. `HEAD` was updated to the new State;
4. `lrfs log` shows the new chain entry;
5. `context-pack` remains valid JSON;
6. the recorded output contains a controlled intent payload or controlled summary;
7. no raw JSONL was written into State/Delta payloads;
8. no raw stdout/stderr was written into State/Delta payloads;
9. `Rufus.RCK.Core` was not modified to support the feature;
10. `Rufus.Agenting` did not gain a dependency on `Rufus.RCK.Core`.

Expected qualitative inspection points:

- State payload looks like a small accepted snapshot, not a dump;
- Delta payload explains cause/change/evidence cleanly;
- provider/model are present as execution metadata;
- long input text is summarized or bounded;
- refs/evidenceRefs are empty or small/controlled;
- no runtime model-selection machinery appears.

Suggested negative checks:

- search the resulting state/delta JSON for `stdout`
- search for `stderr`
- search for JSONL/event-stream-like payloads
- verify no raw file content was embedded

Suggested architecture checks:

- confirm `Rufus.RCK.Core` diff is empty for P13;
- confirm `Rufus.Agenting` project references remain free of Core coupling;
- confirm plain `rfs intent` behavior is unchanged.

## 9. Non-goals

This phase and this contract do not implement or authorize:

- `rfs intent --record`
- `RckAgentTaskRecorder`
- `TraceSlice`
- `TraceSliceAgent`
- ContextPack derived from `TraceSlice`
- `ModelRouter`
- runtime model selection
- agent legacy migration
- RCK schema changes in Core

Also out of scope:

- changing `.rfs/rck` layout
- writing raw `AgentTaskResult`
- writing raw logs/event streams
- writing raw stdout/stderr
- introducing Pi-specific DAG schema
- changing current `rfs intent` runtime semantics

## 10. Final recommendation

Recommendation:

P13 should implement `rfs intent --record` using this Contract v0.

Constraints for P13:

- keep it minimal;
- keep it deterministic;
- keep it local;
- keep `Rufus.RCK.Core` unchanged;
- keep `Rufus.Agenting` free of direct RCK persistence;
- keep output controlled and small;
- keep raw execution traces out of the DAG.

Sequencing recommendation:

1. first validate this pattern with `IntentInferenceAgent`;
2. then inspect real recorded State/Delta output in external repos;
3. only after that consider richer agent-task recorders or `TraceSlice`-level flows.

Explicit recommendation against premature expansion:

- do not advance to `TraceSlice` recording until this agent-task projection pattern is validated;
- do not introduce runtime model-selection abstractions;
- do not invent a general agent-memory system before proving the smallest deterministic case.

In short:

- P13 should implement `rfs intent --record` as a minimal State + Delta recording path;
- it should project `AgentTaskResult` into controlled RCK payloads;
- it should reuse existing Workspace persistence patterns where reasonable;
- it should not modify runtime behavior beyond adding the new recording path;
- it should not touch `Rufus.RCK.Core`.
