# RFS: define TraceSlice generation model strategy

## Context

The audit found that TraceSlice concepts exist, but there is no formal long pipeline abstraction with model profiles per stage.

TraceSlice generation may eventually require a stronger model than intent inference because it decides what operational slice of the DAG and related artifacts should be sent to the main LLM.

## Goal

Define the model strategy for TraceSlice generation.

This issue is design-first.

## Scope

Document:

- current TraceSlice-related code paths
- whether TraceSlice generation is deterministic, LLM-assisted, or mixed today
- what inputs TraceSlice generation currently receives
- what outputs it produces
- whether TraceSlice should remain deterministic first or become LLM-assisted later
- how model selection would work if TraceSlice becomes LLM-assisted

## Non-goals

- Do not implement advanced TraceSlice.
- Do not implement new subagents.
- Do not modify RCK schema.
- Do not modify ContextPack generation.
- Do not change runtime behavior.

## Acceptance criteria

- Current TraceSlice generation behavior is documented.
- Open questions around model usage are captured.
- RCK boundary remains explicit.
- No runtime behavior changes.
