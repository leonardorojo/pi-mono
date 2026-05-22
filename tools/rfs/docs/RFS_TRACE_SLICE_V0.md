# RFS TraceSlice v0

## Purpose

`rfs trace-slice` is a deterministic, read-only operational cut of the current RCK DAG for a concrete prompt.

TraceSlice v0 is not ContextPack, not memory, and not an agent.
It does not call an LLM, does not write `.rfs/rck`, and does not include file contents or diffs.

## Boundary

TraceSlice v0 lives above `Rufus.RCK.Core` and above the persistence layer.
It is a projection built from existing RCK objects plus workspace metadata.

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

## Wrapper and payload

TraceSlice v0 is a projection over the existing RCK graph.
It still respects the structural wrapper vs semantic payload separation:

- `rufus.rck.state` and `rufus.rck.delta` remain the structural RCK containers
- semantic payloads such as `rufus.agent-task-state` or `rufus.interaction-state` remain payload data inside those containers
- the TraceSlice output is its own JSON document and should not be confused with the underlying RCK storage format

TraceSlice may surface decoded state payloads and decoded delta operations as JSON objects, but it is not redefining the RCK core model.

## Selection rule v0

The slice is built deterministically:

1. read the current RCK `HEAD`
2. walk the active chain backwards
3. keep at most `N` states
4. include the incoming deltas that connect those states
5. include anchors associated with the selected states, if present
6. collect metadata-only artifacts from existing payloads and workspace git status

The selection is intentionally simple so the result is stable and easy to reason about.

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

## Stability rule

TraceSlice v0 is a narrow contract.
It should remain deterministic and simple, and any future widening should happen through a deliberate version bump rather than an ad hoc expansion.

The same architectural rule used for Core applies here in spirit: keep the boundary stable once it is defined.

## Notes

- `rfs trace-slice` is a CLI projection only.
- It should not be implemented as `TraceSliceAgent`.
- It should not write new RCK data.
- It should not replace `rfs context-pack`.
- `rfs context-pack` remains the full DAG export.
