# RFS ContextPack from TraceSlice

## Goal

Add a focused, read-only ContextPack materialization mode:

```text
rfs context-pack --trace-slice "<prompt>"
```

This mode must keep `rfs context-pack` full export intact.
It must use deterministic TraceSlice v0 as the selection plan and emit pure JSON without writing `.rfs/rck`.

## Architectural rule

RCK DAG is the source of truth.
TraceSlice is the selection plan.
ContextPack is the materialization of that plan.
The main LLM is only the downstream consumer.

Conceptually:

```text
Prompt
  -> Intent
  -> Anchor-aware TraceSlice planning
  -> Validated TraceSlice
  -> ContextPack
  -> main LLM
```

Not:

```text
Prompt -> ContextPack
```

## Command behavior

### Full export remains unchanged

```text
rfs context-pack
```

This continues to export the full DAG context-pack JSON.
No behavior change is intended for this path.

### New focused export

```text
rfs context-pack --trace-slice "<prompt>"
```

This mode:

1. builds deterministic, intent-first TraceSlice v0 for the prompt
2. reads `selection.stateIds`, `selection.deltaIds`, and `selection.anchorIds`
3. filters the ContextPack payload to those selected objects
4. preserves metadata-only artifact observations
5. reuses TraceSlice `materializationPolicy`
6. emits a scoped ContextPack JSON document

Conceptual clarification:

- active-chain recent remains the current deterministic baseline;
- future planning is expected to be anchor-aware;
- selected anchors are strong relevance signals, not decorative metadata.

## Output shape

The scoped export reuses the existing ContextPack top-level `type`:

```json
{
  "schemaVersion": 1,
  "type": "rck-dag-context-pack-v1",
  "scope": "trace-slice",
  "generatedAtUtc": "...",
  "traceSlice": { "...": "..." },
  "workspace": { "...": "..." },
  "headStateId": "...",
  "headShortId": "...",
  "counts": {
    "states": 5,
    "deltas": 4,
    "anchors": 0
  },
  "activeChain": [],
  "states": [],
  "deltas": [],
  "anchors": [],
  "artifacts": [],
  "materializationPolicy": { "...": "..." },
  "notes": [],
  "exclusions": []
}
```

### Shape notes

- `type` stays `rck-dag-context-pack-v1` to avoid type proliferation.
- `scope = "trace-slice"` explicitly distinguishes the focused export from the full export.
- `traceSlice` embeds the TraceSlice used to build the pack.
- `states`, `deltas`, and `anchors` are filtered by TraceSlice selection.
- `artifacts` stay metadata-only.
- `materializationPolicy` is explicit in the output.

## Selection and filtering rules

The scoped export must filter according to `TraceSlice.selection`:

- `stateIds` -> included `states`
- `deltaIds` -> included `deltas`
- `anchorIds` -> included `anchors`

Selected anchors may also justify why nearby states, deltas, or metadata-only artifact references appear in the pack, but the actual object inclusion must still come from validated selection and policy.

The result may also include supporting metadata fields such as:

- `workspace`
- `headStateId`
- `headShortId`
- `generatedAtUtc`
- `counts`
- filtered `activeChain`

But the object payload collections themselves must stay bounded by the TraceSlice selection.

## Materialization rules

The scoped export must obey the TraceSlice materialization policy.
For v0 this means:

- include state payloads: yes
- include delta decoded ops: yes
- include artifact contents: no
- include git diffs: no
- include stdout/stderr: no
- include JSONL: no

Anchor-specific materialization rules:

- ContextPack may materialize selected anchor metadata;
- an anchor does not automatically include artifact contents;
- an anchor does not automatically include git diffs;
- an anchor does not automatically expand associated states or deltas unless those ids are also selected and validated;
- an anchor does not bypass `materializationPolicy`.

## Security / boundary rules

The new mode must remain read-only.
It must not:

- write `.rfs/rck`
- modify `HEAD`
- modify DAG objects
- read artifact file contents
- include git diffs
- include stdout/stderr dumps
- include raw JSONL
- call Pi
- call an LLM
- require RPC

## RCK Core boundary

`Rufus.RCK.Core` must not be modified for this phase.

The implementation should remain above Core, preferably in:

```text
tools/rfs/src/Rufus.RCK.Workspace/
```

with CLI wiring in:

```text
tools/rfs/src/Rufus.Cli/Program.cs
```

## Non-goals

This phase does not implement:

- TraceSliceAgent
- TraceSliceProposal
- anchor-aware ranking runtime
- explicit `--intent` support
- Pi-backed intent
- LLM-backed selection
- changes to RCK primitives
- changes to `RckState` / `RckDelta`
- artifact content reading
- diff exports
- JSONL exports

## Expected validations

Local:

- `git status --short --branch`
- `dotnet build tools/rfs/Rufus.Cli.sln`
- `dotnet run --project tools/rfs/tests/Rufus.Cli.ParserChecks/Rufus.Cli.ParserChecks.csproj`
- `git diff --check`

External:

- `lrfs status`
- `lrfs context-pack > /tmp/chessboard-rck-context-pack-full.json`
- `python3 -m json.tool /tmp/chessboard-rck-context-pack-full.json > /tmp/chessboard-rck-context-pack-full.pretty.json`
- `lrfs context-pack --trace-slice "Implement rfs show command" > /tmp/chessboard-rck-context-pack-slice.json`
- `python3 -m json.tool /tmp/chessboard-rck-context-pack-slice.json > /tmp/chessboard-rck-context-pack-slice.pretty.json`
- `lrfs status`

Expected outcomes:

- full context-pack still valid
- trace-slice context-pack valid JSON
- `scope = trace-slice`
- embedded `traceSlice`
- filtered `states` / `deltas` / `anchors`
- metadata-only artifacts
- no file contents
- no git diffs
- no stdout/stderr dumps
- no raw JSONL
- no `.rfs/rck` writes
- no `Rufus.RCK.Core` changes
