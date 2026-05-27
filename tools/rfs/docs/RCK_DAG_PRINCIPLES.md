# RCK DAG Principles

## Purpose

RCK is the memory DAG for the repository.
It is not a replacement for Git.
It is not a mirror of the filesystem.
It should preserve a minimal, verifiable cognitive memory that helps an LLM reconstruct context correctly.

The DAG stores facts and references.
The ContextPack exports interpretation-ready views.
TraceSlice is the operational cut of the DAG.
See [`RFS_TRACE_SLICE.md`](RFS_TRACE_SLICE.md) for the TraceSlice contract.

## RCK DAG structure

The on-disk DAG lives under:

```text
.rfs/rck/
  HEAD
  states/
  deltas/
  anchors/
```

Meaning:

- `HEAD` points to the current state id.
- `states/` stores state objects.
- `deltas/` stores transition objects.
- `anchors/` stores milestone objects.

## Semantics

- `states` = snapshots cognitivos / recorded interactions
- `deltas` = transitions between states
- `anchors` = milestones, durable waypoints, and potential semantic indices
- `HEAD` = current state

## Core principles

### 1. Reference or reproduce, do not duplicate

Nothing that can be referenced or reproduced should be stored in the DAG as full primary content.
The DAG should keep the smallest correct representation and point to the rest.

Do not persist, as primary DAG data:

- file contents
- git diffs
- blobs
- full directory snapshots
- long tool outputs
- reproducible data that can be reconstructed from Git, the filesystem, or a tool invocation

Prefer references or reproducible evidence instead:

- Git diff -> reference commit + path(s)
- file content -> reference path + commit/worktree status
- long tool output -> store tool name + target + status + optional summary
- tool result that can be rerun -> store the minimal invocation and the evidence boundary, not the full payload
- LLM answer -> may be stored as cognitive cause or answer summary because it is not deterministically reproducible

The rule is simple:
If Git stores it, RCK references it.
If the filesystem contains it, RCK references it.
If a tool can reproduce it, RCK stores the minimal invocation.
If an LLM produced it as reasoning or interaction, RCK can store the cognitive trace.

### 2. Consistent, robust, non-redundant DAG

The DAG must have one clear source of truth for primary objects and links.
Derived views are useful, but they must not be persisted as if they were source data.

Bad redundancy:

- repeating the same primary truth in multiple locations
- storing both a source object and a persisted copy of a derived view as equivalent truth
- encoding the same relationship in several competing structures

Acceptable redundancy:

- export-time derived views in the ContextPack
- clearly labeled projections that are recomputable from the DAG

Source objects in the DAG:

- states
- deltas
- anchors
- refs
- evidenceRefs
- HEAD

Derived views that should stay derived:

- quickIndex
- activeChain
- counts
- orphan ids
- anchorsByStateId
- deltasByToStateId
- deltasByFromStateId

The DAG should remain consistent, robust, and non-redundant even when the data grows.

### 3. Complete but optimized

Complete does not mean large.
Complete means sufficient to reconstruct:

- where I am
- what happened
- why it happened
- what changed
- what evidence exists
- what is still missing to justify a claim

Optimized means maximizing cognitive value per token.

Conceptual goal:

```text
cognitive value / tokens
```

A smaller DAG is not better by default.
A smaller DAG is better only if it remains complete enough to reconstruct the relevant context.

## Store vs Export

RCK Store / DAG is the persistent minimum.
It should be referential, compact, and correct.

The ContextPack is a projection for LLM consumption.
It may be more comfortable to read because it is allowed to include derived interpretation layers.

The ContextPack may include:

- schema
- interpretationRules
- quickIndex
- derivedRelationships
- other export-time projections that help an LLM reason

Do not confuse what is persisted with what is exported.

## What belongs in the DAG

Belongs in the DAG:

- state id
- delta from/to
- anchor to state
- prompt
- answer, or answer summary depending on role
- minimal Git context
- artifact refs
- minimal tool evidence
- minimal metadata

Does not belong in the DAG:

- file contents
- git diffs
- binary blobs
- full directory snapshots
- huge tool outputs
- generated artifacts when they can be referenced instead
- reproducible data

## TraceSlice relation

TraceSlice uses the RCK DAG as its source.
Current v0 uses a compact `DagQuickIndex`.
Future anchor-aware selection should rank anchors first and expand into states/deltas.
See [`RFS_TRACE_SLICE.md`](RFS_TRACE_SLICE.md).

## Context Pack policy

`rfs context-pack` exports pure JSON.
It is a full DAG export plus schema plus interpretation rules plus derived views.
It is a projection, not the storage model.

The Context Pack may contain redundancy if the redundancy is derived and clearly labeled for LLM consumption.
It must remain parseable and structurally valid.

Current correct shape includes `activeChain[*].gitContext`.
`.rfs/` is internal metadata and is not a user artifact.

## Decision checklist

Before adding new data to the RCK DAG, ask:

1. Is this cognitive information that is unreproducible?
2. Can this be referenced by path, commit, state id, delta id, anchor id, or tool invocation?
3. Is this already stored in Git or the filesystem?
4. Is this source data or a derived view?
5. Will this improve LLM reasoning enough to justify the token and storage cost?
6. Can this be recomputed during context-pack export?

If it can be referenced or recomputed, do not persist it as primary DAG data.

## Future implications

These design principles imply the following next steps:

- improve tool evidence by storing targets and minimal boundaries instead of long outputs
- implement `rfs show` for point inspection of single objects
- implement DAG validation
- keep full context-pack JSON as the canonical export
- add a shorter optimized context-pack later only if it is genuinely needed

## Related docs

- [`RFS_TRACE_SLICE.md`](RFS_TRACE_SLICE.md)
- [`RFS_TUI_UX_CONTRACT.md`](RFS_TUI_UX_CONTRACT.md)
