# RFS TraceSliceProposal Contract

## Purpose

P16 defines the contractual boundary between a future planning agent and RFS for TraceSlice selection.

This phase is documentation-only.
It does not implement `TraceSlicePlannerAgent`.
It does not add an LLM path.
It does not change runtime behavior.
It does not modify `Rufus.RCK.Core`.

The architectural rule is:

- the agent proposes;
- RFS validates;
- RFS materializes.

Conceptually:

```text
PromptN
  -> Intent
  -> TraceSliceProposal
  -> validated TraceSlice
  -> ContextPack
  -> main LLM
```

This document introduces the proposal/validation boundary without changing the current deterministic TraceSlice v0 flow.

## 1. Problem

A future `TraceSlicePlannerAgent` must not produce the final authoritative `TraceSlice` directly.

Why:

- an LLM/agent may suggest relevance, but relevance is not authority;
- a planning agent must not be able to bypass RFS boundaries;
- a planning agent must not be able to include forbidden files, diffs, or raw content directly;
- a planning agent must not be able to invent `stateId` or `deltaId` values that do not exist in the current DAG;
- a planning agent must not write RCK;
- a planning agent must not materialize `ContextPack`.

If an agent could emit the final `TraceSlice` without validation, it could silently widen scope, smuggle forbidden evidence, or claim graph structure that does not exist.
That would break the intent-first, bounded, auditable contract already established for TraceSlice v0.

So the agent output must be treated as a non-authoritative proposal.
RFS remains the authority that validates selection, enforces policy, and produces the actual slice used downstream.

## 2. Definitions

### TraceSliceProposal

`TraceSliceProposal` is the output of an agent.

It:

- is a proposal, request, or suggestion;
- may include rationale and confidence;
- may contain proposed ids;
- may contain a requested `materializationPolicy`;
- is not the source of truth;
- does not have authority to select final context on its own.

A proposal can say "these states and deltas appear relevant".
It cannot say "these are now the final accepted TraceSlice contents".

### TraceSlice

`TraceSlice` is the validated selection produced by RFS.

It:

- is derived from prompt, intent, current DAG state, and validated proposal input when present;
- contains only existing ids and allowed policies;
- may record accepted, rejected, or downgraded proposal parts;
- is the real plan used for `ContextPack` materialization.

`TraceSlice` is therefore not just "what the agent asked for".
It is the result after RFS applies structural checks, scope rules, and materialization constraints.

### ContextPack

`ContextPack` materializes a validated `TraceSlice`.

It:

- consumes the validated `TraceSlice`;
- does not decide selection;
- does not validate proposal authority;
- does not write RCK.

`ContextPack` is a deterministic materialization layer, not a planning layer.

## 3. Conceptual shape of TraceSliceProposal v0

The following shape is conceptual only.
It is not an implementation in this phase.
It is not a frozen runtime schema.

```json
{
  "type": "rufus.trace-slice-proposal",
  "schemaVersion": 1,
  "prompt": {
    "text": "...",
    "isExcerpt": false
  },
  "intent": {
    "kind": "...",
    "summary": "...",
    "source": "..."
  },
  "requestedSelection": {
    "stateIds": [],
    "deltaIds": [],
    "anchorIds": [],
    "artifactRefs": []
  },
  "requestedMaterializationPolicy": {
    "includeStatePayloads": true,
    "includeDeltaDecodedOps": true,
    "includeArtifactContents": false,
    "includeGitDiffs": false,
    "includeStdoutStderr": false,
    "includeJsonl": false
  },
  "rationale": [
    {
      "target": "state:<id>",
      "reason": "..."
    }
  ],
  "confidence": 0.0,
  "warnings": []
}
```

Interpretation notes:

- `type` identifies the object as a proposal, not a final TraceSlice.
- `schemaVersion = 1` is conceptual versioning for the proposal contract.
- `prompt` captures the request that the planner evaluated.
- `intent` carries the conditioning intent summary that the planner received.
- `requestedSelection` contains candidate ids and refs only.
- `requestedMaterializationPolicy` expresses what the planner would like RFS to materialize.
- `rationale` explains why specific targets were requested.
- `confidence` is advisory only.
- `warnings` are planner-side caveats and do not replace RFS validation output.

The important distinction is that every selection or policy field here is requested, not self-authorized.

## 4. Validation contract

Before any proposal can influence the actual slice, RFS must validate it.

Minimum validation rules:

- `stateIds` must exist;
- `deltaIds` must exist;
- `anchorIds` must exist or be rejected explicitly;
- requested deltas must connect states reasonably within the active graph context;
- the result must not exceed `maxStates`;
- the result must not exceed `maxDeltas`;
- the proposal must not include `.rfs` contents;
- the proposal must not include `bin/` or `obj/` contents;
- the proposal must not request file contents by default;
- the proposal must not request diffs by default;
- the proposal must not request stdout/stderr dumps;
- the proposal must not request raw JSONL;
- the proposal must not include refs outside the allowed workspace boundary;
- `materializationPolicy` may be reduced by RFS but must not be expanded without explicit authorization;
- the proposal must not modify `HEAD`;
- the proposal must not write RCK.

Additional contract clarifications:

- proposed ids are only candidates until verified against the current DAG;
- proposed artifact refs are advisory and remain subject to workspace-relative policy checks;
- unknown ids are rejected, not auto-created;
- structurally disconnected or weakly justified deltas may be rejected even if they exist;
- anchor acceptance remains controlled by RFS, not by the planner;
- materialization policy is not a capability grant from the agent to the runtime.

The planner may ask for more than RFS is willing to allow.
RFS is expected to narrow, reject, or downgrade such requests.

## 5. Validation output

RFS must convert a `TraceSliceProposal` into a validated `TraceSlice` decision.

At minimum, the validation result should expose the acceptance outcome conceptually as:

```json
{
  "validation": {
    "status": "accepted|partial|rejected",
    "accepted": [],
    "rejected": [],
    "downgraded": [],
    "reasons": []
  }
}
```

Meaning:

- `status = accepted` means the proposal was accepted as requested within allowed bounds;
- `status = partial` means only part of the proposal was accepted;
- `status = rejected` means the proposal did not produce an acceptable selection;
- `accepted` lists the proposal parts that survived validation;
- `rejected` lists the proposal parts that failed validation;
- `downgraded` lists accepted items that were narrowed or reduced;
- `reasons` explains why acceptance, rejection, or downgrade occurred.

Example:

- the agent requests `includeGitDiffs = true`;
- RFS degrades that request to `false`;
- the downgrade is recorded in `downgraded` with an explanatory reason.

The validated `TraceSlice` is the authoritative downstream object.
The proposal remains part of the audit story, not the source of truth.

## 6. Relationship with Agenting

A future `TraceSlicePlannerAgent` fits into `Rufus.Agenting` as an execution concern, not as an RCK concern.

Conceptual task contract:

### AgentTask

- `Kind = "propose-trace-slice"`
- `Input = prompt + intent + DAG quick index`
- `ExpectedOutput = TraceSliceProposal JSON`

### AgentTaskResult

- `Output = proposal JSON`
- `Summary = brief planner summary`
- `Evidence = references to quick-index/input summaries`
- no direct RCK writes

This keeps the same operational layering already documented for `Rufus.Agenting`:

- `Rufus.Agenting` executes;
- RFS validates;
- RCK stores only controlled results when and if a later phase requires persistence.

Important boundary rules:

- `Rufus.Agenting` may execute a future planner;
- RFS remains the validator and orchestrator;
- RCK does not know `TraceSlicePlannerAgent`;
- `Rufus.RCK.Core` does not change for this contract.

So the planner agent is an execution component that returns a candidate proposal.
It is not a privileged writer of final TraceSlice state.

## 7. Relationship with current TraceSlice v0

Current TraceSlice v0 remains the deterministic baseline.

That means:

- TraceSlice v0 can be treated as the current authoritative baseline selection behavior;
- a future deterministic `TraceSlicePlannerAgent` may produce a `TraceSliceProposal` equivalent to the current v0 selection;
- RFS would then validate that proposal and emit the final `TraceSlice`;
- the current behavior of `rfs trace-slice` does not change in P16.

So P16 does not replace or widen TraceSlice v0.
It introduces a future-compatible proposal boundary around it.

The conceptual evolution is:

```text
Today:
Prompt
  -> Intent
  -> deterministic TraceSlice v0
  -> ContextPack

Later:
Prompt
  -> Intent
  -> TraceSliceProposal
  -> RFS validation
  -> validated TraceSlice
  -> ContextPack
```

Both flows preserve the rule that RFS owns the final TraceSlice.

## 8. Relationship with ContextPack from TraceSlice

P15 already materializes `ContextPack` from `TraceSlice`.
P16 does not change that.

Future conceptual chain:

```text
Prompt
  -> Intent
  -> Proposal
  -> Validated TraceSlice
  -> ContextPack
```

Important rule:

- `ContextPack` never consumes `TraceSliceProposal` directly;
- `ContextPack` consumes only the validated `TraceSlice`.

This preserves a clean separation of responsibilities:

- proposal is speculative;
- TraceSlice is validated selection;
- ContextPack is deterministic materialization.

## 9. Security / anti-black-box rules

The following rules must remain explicit:

- the LLM does not decide the final context;
- the LLM cannot invent ids;
- the LLM cannot widen `materializationPolicy` without validation;
- the LLM cannot include external contents directly into the final slice;
- RFS must be able to explain why each requested part was accepted, rejected, or downgraded;
- every proposal must be auditable.

Auditability requires that RFS can reconstruct:

- what the planner asked for;
- what existed in the graph;
- what was accepted;
- what was rejected;
- what was downgraded;
- why those decisions were made.

This prevents the planning step from becoming an opaque black box that silently controls downstream context.

## 10. Non-goals

P16 does not implement:

- `TraceSlicePlannerAgent`;
- a Pi-backed agent;
- a deterministic planner agent implementation;
- validation runtime;
- ContextPack changes;
- RCK writes;
- Core changes;
- model routing;
- agent migration;
- bridge deprecation.

It also does not change:

- current `rfs trace-slice` behavior;
- current `rfs context-pack --trace-slice` behavior;
- current runtime materialization paths.

## 11. Next phase

The next formal phase is:

## P17 — TraceSlicePlannerAgent deterministic

Future goal:

- implement a deterministic agent that produces `TraceSliceProposal` equivalent to current TraceSlice v0 behavior;
- let RFS validate that proposal;
- do not use an LLM;
- do not use Pi;
- do not modify `Rufus.RCK.Core`.

Restrictions for that future phase:

- do not modify `Rufus.RCK.Core`;
- do not modify runtime outside the narrow planner/validation path;
- do not touch `Program.cs` except for strictly necessary referenced documentation or minimal wiring, and prefer not to touch code at all when possible;
- do not write `.rfs/rck`;
- do not touch `packages/` or `.pi/`;
- do not create `ModelRouter`.

P16 therefore closes with a contract only:

- planner output becomes `TraceSliceProposal`;
- RFS remains the validation authority;
- validated `TraceSlice` remains the only input to `ContextPack` materialization.
