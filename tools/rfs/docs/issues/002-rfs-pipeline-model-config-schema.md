# RFS: document conceptual model configuration schema for pipeline stages

## Context

Current audit result:

- RFS has one workspace default model.
- There is no per-stage model configuration.
- There is no `ModelProfile`.
- There is no `AgentProfile`.
- There is no `PipelineRole -> ModelProfile` mapping.

Before implementing anything, RFS needs a documented configuration shape.

## Goal

Document a conceptual configuration schema for future per-stage model selection.

This issue is documentation-only.

## Scope

Define a conceptual schema for:

- workspace default model
- main LLM model
- optional model overrides per pipeline stage
- fallback behavior when a stage-specific model is not configured

Potential stages to document:

- `intent`
- `trace_slice`
- `context_pack`
- `main_llm`
- `verifier`
- `recorder`

Clarify that:

- `recorder` should remain deterministic.
- `context_pack` should remain deterministic initially.
- missing stage config should fall back to the workspace default model.
- config must not leak into `Rufus.RCK.Core`.

## Non-goals

- Do not implement the schema.
- Do not modify `.rfs/config.json` behavior.
- Do not modify model loading.
- Do not change runtime execution.
- Do not touch `Rufus.RCK.Core`.
- Do not change RCK schema.

## Suggested file

Either:

`tools/rfs/docs/RFS_PIPELINE_LONG_MODEL_CONFIG.md`

or a section inside:

`tools/rfs/docs/RFS_PIPELINE_LONG_MODEL_STRATEGY.md`

## Acceptance criteria

- The conceptual config shape is documented.
- Fallback behavior is documented.
- RCK Core boundary is documented.
- No code changes.
- Build still passes.
