# RFS: introduce per-stage model configuration behind non-invasive read-only plumbing

> **Status: deferred / not implemented**
> Kept for historical reference.

## Context

Previous issues should document:

- long pipeline model strategy
- conceptual model config schema
- intent model separation
- TraceSlice model strategy

After those are accepted, RFS can start adding read-only plumbing for per-stage model configuration without changing runtime behavior.

## Goal

Add non-invasive read-only plumbing for future per-stage model configuration.

## Scope

Possible implementation scope:

- Add types to represent pipeline model roles.
- Add a reader that can resolve:
  - stage-specific model if configured
  - workspace default model otherwise
- Keep current behavior unchanged.
- Do not require existing `.rfs/config.json` files to change.
- Do not modify `Rufus.RCK.Core`.

## Non-goals

- Do not change actual model execution yet.
- Do not route `ask` through multiple models.
- Do not implement LLM subagents.
- Do not change RCK schema.
- Do not break current workspace config.

## Acceptance criteria

- Existing workspaces continue working.
- Default model resolution remains unchanged for current commands.
- New read-only resolver is covered by simple tests if practical.
- `dotnet build tools/rfs/Rufus.Cli.sln` passes.
- No changes to `Rufus.RCK.Core`.
