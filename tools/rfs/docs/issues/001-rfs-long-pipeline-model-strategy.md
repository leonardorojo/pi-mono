# RFS: define long pipeline model strategy from audit

## Context

The audit in `tools/rfs/docs/RFS_PIPELINE_LONG_MODEL_AUDIT.md` found that RFS currently uses a single workspace default model:

`.rfs/config.json -> llm.defaultModel`

This model is reused by:

- `rfs ask`
- `rfs ask-json`
- `rfs agent`
- `rfs agent-json`
- TUI paths that call Pi

The audit also found that there is no current:

- `ModelProfile`
- `AgentProfile`
- formal `PipelineRole -> ModelProfile` mapping

## Goal

Create a design document that turns the audit findings into an explicit model strategy for the long pipeline.

This is a documentation/design issue only.

## Scope

Document:

- The conceptual long pipeline stages:
  - intent inference
  - trace-slice generation
  - context-pack generation
  - main LLM execution
  - optional verification/review
  - State + Delta recording

- Which stages are currently:
  - implemented
  - deterministic
  - legacy
  - experimental
  - missing

- Which stages may eventually require model-specific configuration.

- The distinction between:
  - workspace default model
  - main LLM model
  - possible future per-stage model configuration

- The boundary that `Rufus.RCK.Core` must remain independent from models and agents.

## Non-goals

- Do not implement model routing.
- Do not implement per-stage model configuration.
- Do not modify `rfs ask`.
- Do not modify `ask --record`.
- Do not modify `agent`.
- Do not touch bridges.
- Do not modify `Rufus.RCK.Core`.
- Do not change RCK schema.

## Suggested file

`tools/rfs/docs/RFS_PIPELINE_LONG_MODEL_STRATEGY.md`

## Acceptance criteria

- A strategy document exists and references the audit findings.
- The document clearly distinguishes current state from future design.
- No runtime behavior changes.
- `dotnet build tools/rfs/Rufus.Cli.sln` passes.
- `git diff --check` passes.
