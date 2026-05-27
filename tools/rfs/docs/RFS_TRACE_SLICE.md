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

PLT2 evolves this input toward `DagQuickIndexV1`, but still keeps it compact, structural, and safe.

## 6. PLT2 — Anchor-guided structural DAG slicing

PLT2 is the next step for TraceSlice selection.
It is *not* semantic slicing.
It is *not* RAG.
It is *not* similarity search.
It is *not* a free-form selection of memory by textual resemblance.

PLT2 uses anchors as structural entry points into the RCK DAG.
The LLM chooses candidate anchors.
RFS expands structurally.
The validator validates.
ContextPack materializes.

Conceptually:

```text
Intent
  ↓
DagQuickIndexV1
  ↓
PiTraceSliceProposalAgent
  ↓
AnchorSelection
  ↓
AnchorExpansionService
  ↓
TraceSliceProposal v1
  ↓
RckTraceSliceProposalValidator
  ↓
Validated TraceSlice
  ↓
ContextPack
```

The design rule is simple:

- the LLM chooses points of entry;
- RFS cuts the DAG;
- RFS validates the result;
- ContextPack materializes the validated cut;
- RCK records only after the final answer.

## 7. DagQuickIndexV1 conceptual shape

`DagQuickIndexV1` is the compact structural index used to guide anchor selection and deterministic expansion.
It remains safe and intentionally incomplete.

### DagQuickIndexV1

- `headStateId`
- `recentStateIds[]`
- `recentDeltaIds[]`
- `anchors[]`
- `states[]`
- `deltas[]`

### AnchorCandidate

- `id`
- `stateId`
- `label`
- `reason`
- `createdAtUtc`
- `isRecentChain`
- `parentAnchorIds[]`
- `distanceToHead`
- `incomingDeltaIds[]`
- `outgoingDeltaIds[]`

### StateCandidate

- `id`
- `shortId`
- `createdAtUtc`
- `attachedAnchorIds[]`
- `incomingDeltaIds[]`
- `outgoingDeltaIds[]`
- `distanceToHead`

### DeltaCandidate

- `id`
- `fromStateId`
- `toStateId`
- `createdAtUtc`

Future-friendly fields such as `promptSummary`, `answerSummary`, `operationSummary`, and `evidenceSummary` are intentionally out of scope for the first PLT2 cut.

The following remain forbidden in `DagQuickIndexV1`:

- full state payloads
- full answers
- file contents
- diffs
- stdout/stderr
- raw JSONL
- secrets

## 8. AnchorSelection internal DTO

`AnchorSelection` is an internal-only output produced by `PiTraceSliceProposalAgent` in PLT2.
It does not replace the public proposal schema.
It is not authoritative.
It does not contain state or delta payloads.
It does not change the storage schema.

### AnchorSelection

- `selectedAnchorIds[]`
- `fallbackStrategy`
- `rationale[]`
- `warnings[]`
- `confidence`

Optional later fields may exist internally, but are not required for PLT2 initial delivery:

- `requestedRecentChainFallback`
- `maxExpansionDepth`
- `candidateAnchorScores[]`

## 9. AnchorExpansionService

`AnchorExpansionService` lives in `Rufus.RCK.Workspace`.
It is deterministic.
It performs structural expansion, not semantic planning.
It does not call an LLM.
It does not read file contents, diffs, stdout/stderr, or raw JSONL.

### Input

- `AnchorSelection`
- `DagQuickIndexV1`
- `maxStates`
- `maxDeltas`
- `expansionPolicy`

### Output

- `anchorIds[]`
- `stateIds[]`
- `deltaIds[]`
- `strategy`
- `warnings[]`
- `expansionEvidence[]`

### Minimum rules

For each valid selected anchor, the service should:

- include `anchor.stateId`
- include connected incoming and outgoing deltas while respecting limits
- include neighboring states connected by those deltas
- preserve deterministic ordering
- respect `maxStates` and `maxDeltas`

If `includePathToHead` is enabled in a later phase, the service may optionally include a structural path toward `HEAD`, but that is not required for PLT2 initial delivery.
Parent anchor lineage may be carried as metadata when available, but it is not required for the initial cut.

### Explicit fallback rules

- no anchors available → use recent-chain structural fallback with a warning
- no relevant anchors → use recent-chain structural fallback with a warning
- selected anchor missing → warn and reject or ignore according to policy
- anchor points to missing state → warn; the validator decides if the result is acceptable
- expansion exceeds limits → truncate deterministically and emit a warning

### Out of scope for the first PLT2 cut

- `candidateAnchorScores`
- mandatory `includePathToHead`
- `pathToHead` as a requirement
- schema migration

## 10. TraceSliceProposal compatibility

PLT2 keeps `rufus.trace-slice-proposal` at `schemaVersion = 1`.
The internal expansion result is transformed into that existing public schema.

### Mapping

- `requestedSelection.anchorIds = expansion.anchorIds`
- `requestedSelection.stateIds = expansion.stateIds`
- `requestedSelection.deltaIds = expansion.deltaIds`
- `requestedSelection.artifactRefs = []`
- `rationale` explains anchor selection, structural expansion, and fallback if any
- `warnings` explains truncation, missing anchors, or recent-chain fallback
- `confidence` is inherited from `AnchorSelection`

This keeps the public proposal shape stable while allowing a richer internal design.

## 11. Validator boundary

`RckTraceSliceProposalValidator` remains the authority for validation.
It validates ids and policy.
It does not rank anchors.
It does not decide relevance.
It does not plan.
It does not call an LLM.
It does not read file contents, diffs, stdout/stderr, or raw JSONL.

The validator may still perform small safety checks that keep it within the role of authority, not planner.

## 12. Fallback rules

All fallback behavior must be explicit.
There is no silent fallback.

- no anchors available → recent-chain structural fallback with warning
- no relevant anchors → recent-chain structural fallback with warning
- selected anchor missing → warning and reject or ignore according to policy
- anchor points to missing state → warning and validator decides
- expansion exceeds limits → deterministic truncation with warning

## 13. UI and reporting

The Complete pipeline should distinguish selection, expansion, and validation.

Desired display:

```text
[2/5] Building TraceSlice proposal...
  slicing: anchor-guided structural
  anchors selected: N
  expansion: X states · Y deltas
  fallback: none|recent-chain

[3/5] Validating proposal...
  validation: accepted
  validated selection: X states · Y deltas · N anchors
```

Do not display `anchor-guided structural` unless a real `AnchorExpansionService` run was used.
The UI should distinguish requested, expanded, and validated selections.

## 14. Phased implementation plan

- **PLT2a — design/documentation**
  - this contract and its supporting cross references
- **PLT2b — DagQuickIndexV1**
  - compact structural index builder and unit tests
- **PLT2c — AnchorExpansionService**
  - deterministic expansion service and unit tests
- **PLT2d — PiTraceSliceProposalAgent emits AnchorSelection**
  - internal anchor selection output and fake transport tests
- **PLT2e — Complete integration + UI**
  - public proposal compatibility, validation, and stage reporting

## 15. Validation

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
