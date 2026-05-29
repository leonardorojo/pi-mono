# RFS: prepare intent inference for future lightweight model selection

> **Status: implemented** — `PiIntentInferenceAgent` exists and is used in Complete mode stage 1/5.
> Kept for historical reference.

## Context

The audit found that intent-related concepts exist, including local deterministic/mock agents, but there is no dedicated LLM subagent or per-stage model selection.

Intent inference is a good first candidate for model separation because it may use a lightweight model in the future.

## Goal

Prepare the intent inference path for future lightweight model selection.

This issue should start with design and only move to implementation if the design is already accepted.

## Scope

Investigate and document:

- current intent inference flow
- current deterministic/mock intent agent behavior
- whether intent inference currently calls an LLM
- what would be needed to allow intent-specific model selection later
- how to avoid coupling intent inference to `Rufus.RCK.Core`

## Non-goals

- Do not implement LLM intent subagents yet.
- Do not change default runtime behavior.
- Do not add model routing yet.
- Do not modify RCK schema.
- Do not replace deterministic/mock agents yet.

## Acceptance criteria

- Current intent inference path is documented.
- Required seams for future model override are identified.
- No runtime behavior changes unless explicitly approved in a later issue.
