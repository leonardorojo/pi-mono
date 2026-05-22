# RFS TraceSlice v0 Shape Review

## Scope

Reviewed the live `lrfs trace-slice "Implement rfs show command"` output generated from:

- repo: `/home/rufus/DEV/leonardorojo/ChessBoardApp`
- command: `lrfs trace-slice "Implement rfs show command"`
- post-checks: `python3 -m json.tool`, `lrfs status` before/after, and `lrfs context-pack`

This review is documentation-first. No runtime behavior was changed.

## Decision

**Freeze TraceSlice v0 as-is.**

The current shape is sufficiently clear and stable for P15 planning: `ContextPack from TraceSlice` can be built from the existing top-level contract without adding new fields or introducing a TraceSlice agent.

## Abbreviated current shape

```json
{
  "type": "rufus.trace-slice",
  "schemaVersion": 1,
  "prompt": { "text": "Implement rfs show command", "isExcerpt": false },
  "intent": {
    "kind": "trace-slice-request",
    "summary": "Implement rfs show command",
    "source": "deterministic"
  },
  "selection": {
    "strategy": "active-chain-recent",
    "maxStates": 5,
    "headStateId": "...",
    "stateIds": ["..."],
    "deltaIds": ["..."],
    "anchorIds": []
  },
  "artifacts": [
    {
      "path": "RFS_TRACE_TEST.md",
      "changeType": "untracked",
      "source": "git-status",
      "includeMode": "metadata-only"
    }
  ],
  "materializationPolicy": {
    "includeStatePayloads": true,
    "includeDeltaDecodedOps": true,
    "includeArtifactContents": false,
    "includeGitDiffs": false,
    "includeStdoutStderr": false,
    "includeJsonl": false
  },
  "states": ["rufus.rck.state", "..."],
  "deltas": ["rufus.rck.delta", "..."],
  "anchors": [],
  "notes": ["..."],
  "exclusions": ["..."]
}
```

## Top-level field review

### Required in v0

The live output already includes the fields needed for a stable v0:

- `type`
- `schemaVersion`
- `prompt`
- `intent`
- `selection`
- `artifacts`
- `materializationPolicy`
- `notes`
- `exclusions`

These are enough to explain what the slice is, how it was selected, and what it intentionally excludes.

### Optional / present when available

The current output also includes these payload-bearing sections:

- `states`
- `deltas`
- `anchors`

For v0, these are useful and should stay. They are not noise; they are the concrete selection result that P15 can project into a ContextPack.

## Separation of concerns

The current shape keeps the important boundaries visible:

- **Prompt**: the user request text is preserved under `prompt.text`.
- **Intent**: a minimal deterministic summary is present, not an LLM explanation.
- **Selection**: the DAG selection rule and chosen IDs are explicit.
- **Artifacts**: workspace observations are metadata-only.
- **Materialization policy**: the output states what may be expanded later.
- **Notes / exclusions**: the contract states the read-only and no-leak boundaries.

This is the right level for TraceSlice v0. It does *not* behave like ContextPack yet, and that is correct.

## Selection review

The selection block is clear enough for v0:

- `strategy`: `active-chain-recent`
- `maxStates`: explicit and fixed in the output
- `headStateId`: explicit anchor at the current RCK head
- `stateIds`: the selected chain from HEAD backward
- `deltaIds`: incoming deltas that connect the selected states
- `anchorIds`: present and valid as an array; empty is acceptable in v0

Observed behavior in the sample:

- `stateIds` follow the active chain from HEAD.
- `deltaIds` connect the selected states.
- `anchorIds` is empty, which is acceptable and does not weaken the contract.
- `maxStates = 5` is explicit and stable.

## Artifact review

The current artifact shape is acceptable for v0:

- `path` is present and usable.
- `source` is present.
- `includeMode = metadata-only` is explicit.
- No artifact content is included.
- No diffs are included.
- `.rfs`, `bin`, and `obj` are excluded by the builder.

Known limitation: artifact extraction is intentionally limited to metadata observations from existing payloads and workspace git status. That is enough for v0 and should be documented, not expanded.

## Materialization policy review

The policy is explicit and useful:

- `includeStatePayloads = true`
- `includeDeltaDecodedOps = true`
- `includeArtifactContents = false`
- `includeGitDiffs = false`
- `includeStdoutStderr = false`
- `includeJsonl = false`

### Decision

For v0, `includeStatePayloads` and `includeDeltaDecodedOps` can remain in **TraceSlice** as a materialization policy, not only in the eventual ContextPack. That is the right abstraction boundary: TraceSlice is a plan for what context may be surfaced later, not the final materialized ContextPack.

## Intent review

Current intent shape:

```json
{
  "kind": "trace-slice-request",
  "summary": "Implement rfs show command",
  "source": "deterministic"
}
```

This is sufficient for v0.

It is intentionally naive and deterministic. It does not need LLM interpretation yet. For P15, the intent object only needs to give the downstream projection a stable request label and summary.

## What TraceSlice v0 must not contain

The live output and builder both correctly avoid:

- file contents
- git diffs
- stdout/stderr
- JSONL protocol traffic
- RCK writes
- TraceSliceAgent
- LLM calls

Also important: TraceSlice v0 is *not* attempting to summarize the entire history. It is a bounded active-chain slice.

## Limitations known and accepted in v0

- Artifact extraction is metadata-only and conservative.
- Intent is naive/deterministic.
- `anchorIds` may be empty.
- The selection is bounded by `active-chain-recent` and `maxStates = 5`.
- The output is a projection, not a final ContextPack.

These are acceptable v0 constraints.

## P15 criteria

TraceSlice v0 is ready to support **P15: ContextPack from TraceSlice** if P15 continues to honor these rules:

1. Treat TraceSlice as the input projection, not as storage.
2. Preserve the explicit selection metadata.
3. Preserve metadata-only artifact observations unless P15 deliberately widens them.
4. Expand only through the materialization policy, not by changing the TraceSlice contract ad hoc.
5. Keep `lrfs trace-slice` read-only and deterministic.
6. Keep `lrfs context-pack` valid and independent.
7. Do not require a TraceSlice-specific agent or LLM step.

## Verification notes

Observed checks during this review:

- `lrfs status` before trace-slice: HEAD, states, and deltas were stable.
- `lrfs status` after trace-slice: unchanged.
- `python3 -m json.tool` succeeded on the trace-slice JSON.
- `lrfs context-pack` also parsed successfully as JSON.

## Final assessment

The v0 shape is good enough to freeze.

No code change is required for the shape review itself.
If P15 needs more context, it should evolve from this policy boundary rather than by widening TraceSlice v0 now.
