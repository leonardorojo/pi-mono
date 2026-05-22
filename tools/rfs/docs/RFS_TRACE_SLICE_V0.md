# RFS TraceSlice v0

## Purpose

`rfs trace-slice` is a deterministic, read-only operational cut of the current RCK DAG for a concrete prompt.

TraceSlice v0 is not ContextPack, not memory, and not an agent.
It does not call an LLM, does not write `.rfs/rck`, and does not include file contents or diffs.

TraceSlice v0 remains valid as currently implemented.
P14.2 made the contract explicit: TraceSlice is intent-first.
P15A+B does not widen the TraceSlice contract either; it adds a new read-only materialization path that consumes TraceSlice as a plan.

## TraceSlice is intent-first

TraceSlice should not be interpreted conceptually as:

```text
Prompt -> TraceSlice
```

The correct conceptual chain is:

```text
Prompt
  -> Intent
  -> Anchor-aware TraceSlice planning
  -> Validated TraceSlice
  -> ContextPack
  -> main LLM
```

That rule applies even when the CLI is invoked as the short form:

```text
rfs trace-slice "<prompt>"
```

In v0, that command is a shorthand for:

1. accept the current prompt
2. infer or obtain an intent v0 internally
3. read the current RCK DAG state from the active chain
4. apply the materialization policy
5. emit a TraceSlice JSON document that always contains an `intent` block

So the command surface may stay short, but the contract is still intent-first.

That also means TraceSlice should not be understood as "active-chain-recent only".
Recency is the current deterministic baseline in v0, but the contract now recognizes that anchors are cognitive milestones and future planning must be anchor-aware.

## Boundary

TraceSlice v0 lives above `Rufus.RCK.Core` and above the persistence layer.
It is a projection built from existing RCK objects plus workspace metadata.

A TraceSlice must be constructed from:

- the current prompt
- the inferred or supplied intent
- the current RCK DAG state
- relevant anchors
- metadata-only artifact observations
- the materialization policy

What it may include:

- the prompt used to request the slice
- a minimal deterministic intent summary
- the active chain starting from `HEAD`
- up to `N` recent states, default `N = 5`
- incoming deltas that connect those states
- anchors associated with the selected states, when available
- metadata-only artifact observations
- an explicit materialization policy
- notes and exclusions

What it must not include:

- file contents
- git diffs
- stdout/stderr
- JSONL protocol traffic
- any RCK writes
- any TraceSlice-specific agent

## Intent v0

TraceSlice v0 always exposes an `intent` block.

The current v0 intent is intentionally naive and deterministic:

- `source = deterministic` for the current shorthand path used by `rfs trace-slice`
- `source = intent-inference-agent` for `rfs trace-slice-proposal`, which still uses the deterministic/mock `IntentInferenceAgent`
- `kind` may remain a naive operational label
- `summary` may be a normalized excerpt of the prompt
- no LLM is used
- no Pi/RPC/JSONL path is used
- no RCK data is written

If RFS later chooses to route the same v0 inference through the existing deterministic/mock intent harness, the contract may still describe the source as `intent-inference-agent`, but that is still a deterministic/mock path, not an LLM-backed step.

The key rule is not sophistication. The key rule is that TraceSlice is conditioned by explicit intent, even when that intent is simple.

## What intent conditions

Intent is not decorative metadata.
It conditions how the TraceSlice should be built.

Conceptually, intent influences:

- which states and deltas should be searched or prioritized
- which anchors should be searched, selected, or prioritized
- which artifacts should be considered relevant
- how much history depth should be used
- which materialization policy should apply
- which categories of evidence should be excluded

In v0, those decisions remain conservative and deterministic.
The current builder is intentionally simple, but the contract now makes clear that the slice is still intent-conditioned, not prompt-only.

## Anchors are cognitive milestones

Anchors are cognitive milestones.

They represent stable tags, milestones, or other durable points in the DAG that may remain strongly relevant even when the latest active-chain states are not the best guide for the current prompt.

That means:

- anchors are not decorative;
- `anchorIds` in TraceSlice are part of the selection contract;
- anchors can justify why certain states or deltas belong in a slice;
- active-chain-recent remains a baseline heuristic, not the only conceptual rule.

For TraceSlice v0 today, this does not require runtime changes.
The current builder may still produce empty `anchorIds`, and that remains valid v0 behavior.
But the future planning contract must remain anchor-aware so that later planners can treat anchors as strong relevance signals when appropriate.

## Wrapper and payload

TraceSlice v0 is a projection over the existing RCK graph.
It still respects the structural wrapper vs semantic payload separation:

- `rufus.rck.state` and `rufus.rck.delta` remain the structural RCK containers
- semantic payloads such as `rufus.agent-task-state` or `rufus.interaction-state` remain payload data inside those containers
- the TraceSlice output is its own JSON document and should not be confused with the underlying RCK storage format

TraceSlice may surface decoded state payloads and decoded delta operations as JSON objects, but it is not redefining the RCK core model.

## Selection rule v0

The current v0 slice is built deterministically:

1. read the current RCK `HEAD`
2. walk the active chain backwards
3. keep at most `N` states
4. include the incoming deltas that connect those states
5. include anchors associated with the selected states, if present
6. collect metadata-only artifacts from existing payloads and workspace git status

This is still valid after P15A+B.
The important clarification is conceptual: this deterministic selection is the current implementation of an intent-first TraceSlice, not a statement that TraceSlice should always be prompt-only.

Additional v0 clarification:

- `selection.anchorIds` may be empty in the current implementation;
- that is valid for TraceSlice v0;
- this does not weaken the future anchor-aware planning contract.

## Materialization policy

TraceSlice v0 must state what it materializes and what it excludes.
A minimal policy is:

- include state payloads: yes
- include delta decoded ops: yes
- include artifact contents: no
- include git diffs: no
- include stdout/stderr: no
- include JSONL: no

This keeps the slice operational without turning it into a dump of raw workspace evidence.

## ContextPack from TraceSlice

P15A+B adds a new read-only consumer path:

```text
rfs context-pack --trace-slice "<prompt>"
```

That command does not replace `rfs context-pack` full export.
Instead, it executes the intent-first TraceSlice path internally, then materializes a focused ContextPack from the resulting TraceSlice selection and materialization policy.

Conceptually:

```text
Prompt + Intent + Anchor-aware TraceSlice planning + Validated TraceSlice -> ContextPack
```

Operationally in v0:

1. build deterministic TraceSlice v0 from the prompt
2. read `selection.stateIds`, `selection.deltaIds`, and `selection.anchorIds`
3. filter the full DAG projection to those selected objects
4. preserve metadata-only artifacts
5. emit a scoped ContextPack JSON document with `scope = "trace-slice"`

The scoped ContextPack is therefore a materialization of the TraceSlice plan, not a replacement for TraceSlice itself.
Selected anchors may be materialized as metadata, but they do not automatically authorize artifact contents, diffs, or policy widening.

## Future explicit intent

Future phases may add explicit intent inputs without changing the intent-first rule.
Possible evolution points include:

- `rfs trace-slice --intent <intent-json> "<prompt>"`
- `rfs context-pack --trace-slice "<prompt>"` internally executing intent inference before projection
- a future `TraceSliceProposal` that receives `prompt + intent + DAG quick index + anchors metadata`
- intent sourced from `IntentInferenceAgent`
- intent sourced from a previously recorded intent
- intent sourced from a JSON file
- intent sourced from a future Pi-backed `IntentAgent`

Those are future contract directions only.
They are not implemented in P15A+B.

## Relation to P15

P15 should use the chain:

```text
Prompt + Intent + Anchor-aware TraceSlice planning + Validated TraceSlice -> ContextPack
```

Not:

```text
Prompt -> ContextPack
```

TraceSlice remains the bounded projection layer between prompt/intent handling and ContextPack materialization.

`rfs context-pack` without flags remains the full DAG export.
`rfs context-pack --trace-slice "<prompt>"` is the new focused materialization mode.

## Stability rule

TraceSlice v0 is a narrow contract.
It should remain deterministic and simple, and any future widening should happen through a deliberate version bump rather than an ad hoc expansion.

P14.2 did not unfreeze the shape from P14.1.
P15A+B also does not unfreeze it.
It reuses TraceSlice v0 as an input contract.

The same architectural rule used for Core applies here in spirit: keep the boundary stable once it is defined.

P17 adds a contractual clarification, not a runtime widening:

- current TraceSlice v0 behavior remains valid;
- current TraceSlice v0 may still emit empty `anchorIds`;
- future planning should become anchor-aware;
- P18 is the phase expected to make the deterministic planner emit anchor-aware `TraceSliceProposal` output.

## Non-goals for P14.2 + P15A+B

These phases do not implement:

- TraceSliceAgent
- TraceSliceProposal
- anchor-aware ranking runtime
- `--intent` CLI support for TraceSlice
- Pi-backed intent inference
- any LLM path
- changes in `Rufus.RCK.Core`
- writes into `.rfs/rck`
- reading artifact file contents
- including git diffs
- including stdout/stderr dumps
- including raw JSONL traffic

## Notes

- `rfs trace-slice` is a CLI projection only.
- `rfs trace-slice "<prompt>"` is a shorthand surface, not a prompt-only contract.
- It should not be implemented as `TraceSliceAgent`.
- It should not write new RCK data.
- It should not replace `rfs context-pack`.
- `rfs context-pack` remains the full DAG export.
- `rfs context-pack --trace-slice` is a scoped materialization mode built from TraceSlice v0.
