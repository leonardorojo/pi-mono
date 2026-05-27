# RFS TraceSlice

## 1. Purpose

TraceSlice is the operational cut of the RCK DAG for a concrete task.
It defines *what* slice of the DAG is requested, validated, and materialized for downstream use.

TraceSlice is not the DAG itself.
TraceSlice is not ContextPack.
TraceSlice is not an agent.

See also: [`RCK_DAG_PRINCIPLES.md`](RCK_DAG_PRINCIPLES.md).

## 2. Relationship with RCK DAG

RCK DAG is the source of truth for repository memory.
TraceSlice is a bounded selection over that DAG.
ContextPack is the materialization of a validated TraceSlice.

Conceptually:

```text
Prompt
  -> Intent
  -> TraceSlice proposal
  -> RFS validation
  -> validated TraceSlice
  -> ContextPack
  -> main LLM
```

The key boundary is simple:

- the agent proposes;
- RFS validates;
- ContextPack materializes;
- RCK records only after the final answer.

## 3. Complete mode pipeline

Complete mode uses a five-stage pipeline:

```text
[1/5] Intent
  PiIntentInferenceAgent
  claude-haiku-4.5

[2/5] TraceSlice Proposal
  PiTraceSliceProposalAgent
  claude-sonnet-4.5

[3/5] Validation
  RckTraceSliceProposalValidator

[4/5] ContextPack
  RckTraceSliceContextPackBuilder

[5/5] Principal Answer
  PiPrincipalAnswerAgent
  session model
```

The contract is:

- intent conditions the request;
- proposal suggests a slice;
- validation authorizes the slice;
- ContextPack materializes the validated slice;
- the principal answer is produced from the resulting context.

## 4. TraceSliceProposal v0

The proposal object is LLM-backed, non-authoritative, and JSON-only.
It is a request for a slice, not the final slice.

Top-level shape:

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
  "rationale": [],
  "confidence": 0.0,
  "warnings": []
}
```

Proposal semantics:

- `type = "rufus.trace-slice-proposal"`
- `schemaVersion = 1`
- output is JSON only
- proposal is not authoritative
- proposal may include rationale and warnings
- proposal may request ids, anchors, and a bounded materialization policy

## 5. PiTraceSliceProposalAgent input

The agent receives a compact request, not the full DAG.
Its v0 input shape is:

- `UserPrompt`
- `Intent`
- `DagQuickIndex`
- `Limits`
- `PolicyHints`

`DagQuickIndex` v0 contains:

- `HeadStateId`
- `RecentStateIds`
- `RecentDeltaIds`
- anchors as metadata/candidates

It does *not* include:

- the full DAG
- full ContextPack payloads
- full state payloads
- full delta payloads
- file contents
- diffs
- stdout/stderr
- JSONL

## 6. Validation

The proposal is not the authority.
RFS validates it before anything downstream consumes it.

Validation checks include:

- ids exist in the current DAG
- `maxStates` / `maxDeltas` are respected
- anchors exist and are acceptable
- artifact refs are allowed and metadata-only
- the requested policy is safe

The validator may:

- accept
- reject
- downgrade

## 7. Validated TraceSlice

The validated TraceSlice is the authoritative slice used downstream.

It contains:

- the final `selection`
- the final `validation` block
- accepted / rejected / downgraded proposal parts
- the final materialization policy
- notes and exclusions

It is the slice RFS actually uses for ContextPack materialization.

## 8. ContextPack

ContextPack is built from the validated TraceSlice.
It is never built from a raw proposal.

ContextPack is the materialization layer for the main LLM.
It preserves the selection boundary while projecting the chosen DAG view into a downstream-ready JSON document.

## 9. Anchors current semantics v0

Current v0 anchor semantics are intentionally conservative:

- anchors are passed as metadata/candidates in `DagQuickIndex`
- anchors can be requested in `requestedSelection.anchorIds`
- anchors are validated by RFS
- anchors can be materialized metadata-only
- anchors are *not* currently the primary semantic index
- there is *no* automatic expansion `anchor -> states/deltas`
- `parentAnchorIds` are *not* currently used for semantic navigation

Anchors are milestones and relevance hints, not a recursive expansion mechanism in v0.

## 10. Future direction: anchor-aware TraceSlice selection

A future anchor-aware planner may evolve toward:

- ranking anchors first from intent
- expanding from selected anchors into linked `stateId`
- including nearby deltas around those states
- using `parentAnchorIds` as anchor lineage when available
- enriching `DagQuickIndex` with summaries and neighborhood signals
- distinguishing requested selection from hard limits in the UI

That direction is future work, not current v0 behavior.

## 11. Guardrails

The contract boundaries are:

- the agent proposes
- RFS validates
- ContextPack materializes
- RCK records only after the final answer

Prohibited in proposal input and proposal output:

- file contents
- diffs
- stdout/stderr dumps
- JSONL
- bypassing validation
- silent fallback that widens scope

## 12. Technical commands

Relevant commands in this phase:

- `rfs trace-slice-proposal`
- `rfs trace-slice-proposal-llm`
- `rfs trace-slice-validate`
- `rfs context-pack --trace-slice-validated`
- TUI Complete [2/5]

## 13. Known limitations

- `DagQuickIndex` v0 is compact and flat.
- anchor-aware selection is not implemented yet.
- `TraceSliceProposal` is not a full DAG traversal.
- the UI may need to distinguish limits vs requested selection more clearly.

## Related docs

- [`RCK_DAG_PRINCIPLES.md`](RCK_DAG_PRINCIPLES.md)
- [`RFS_TUI_UX_CONTRACT.md`](RFS_TUI_UX_CONTRACT.md)
- historical pointers: `RFS_TRACE_SLICE_LLM_PROPOSAL.md`, `RFS_TRACE_SLICE_PROPOSAL_CONTRACT.md`, `RFS_TRACE_SLICE_V0.md`, `RFS_TRACE_SLICE_V0_SHAPE_REVIEW.md`, `RFS_CONTEXT_PACK_FROM_TRACE_SLICE.md`
