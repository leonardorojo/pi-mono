# RufusChat PI extension foundation

RufusChat is the product surface for PI: the user-facing place where chat, governance, safe context review, and confirmation-gated actions meet.

This directory is the first foundation step for that surface. It is intentionally small and does not try to replace the current prototype shell yet.

## What RufusChat is

- A PI extension surface for chat-centered workflows
- A place to orchestrate safe context, chat sessions, and RCK-related views
- A future adapter boundary between PI UI concerns and RCK provider concerns
- A surface that can grow into a local product shell without turning `scripts/rufuschat-ui-server.mjs` into the final destination

## What RufusChat is not yet

- Not the final UI implementation
- Not a direct implementation of the full PI product surface
- Not a replacement for RCK Core Kernel or RufusLab.RCK.Cli
- Not a direct reader/writer of `.pi/rck/` from the UI layer
- Not a place for raw evidence by default
- Not a real Hermes executor
- Not Codex execution
- Not a storage migration

## Relationship to PI

PI is the host surface. RufusChat should sit inside PI as an extension/product surface with clear boundaries:

- PI owns extension loading and session plumbing
- RufusChat owns chat-facing orchestration and safe views
- RCK Bridge remains the current provider behind safe bridge operations
- Future RCK Core Kernel / RufusLab.RCK.Cli implementations can replace the provider layer without rewriting the UI contract

## Relationship to the current RCK Bridge

Current provider chain:

- RufusChat product surface
- RufusChat backend adapter
- Pi RCK Bridge provider

The bridge is the current implementation source for safe state, trace, and context views.
RufusChat should treat it as an adapter dependency, not as the shape of the UI itself.

## Relationship to the future RCK Core Kernel

Future provider chain:

- RufusChat product surface
- RufusChat backend adapter
- RckProvider implementation backed by RCK Core Kernel / RufusLab.RCK.Cli

That future layer should preserve the same safe contract while moving operational truth into the kernel and CLI surface.

## Relationship to `scripts/rufuschat-ui-server.mjs`

`scripts/rufuschat-ui-server.mjs` is the current prototype validation shell.

It is useful as a reference for safe UI behavior and DTO shape, but it is not the final product boundary.
RufusChat should evolve as its own extension surface instead of growing indefinitely inside that script.

## Core concepts

### Project

The project is the PI workspace boundary RufusChat operates inside.
It answers: which workspace, which extension surface, which local operating context?

### Chat Session

A chat session is the conversational interaction state for RufusChat.
It can span multiple prompts and actions, but it is not the same thing as RCK state.

### RCK Trace DAG

The RCK Trace DAG is the operational graph of states, deltas, anchors, and derived context artifacts.
It is the source for safe operational reasoning.

### Context Pack

A context pack is an injected, user-approved, safe summary that can be handed to downstream reasoning tools.
It is not raw evidence and not the full trace.

### Semantic memory

Semantic memory is the higher-level remembered meaning stored for future conversation or orchestration.
It is distinct from the trace DAG and should not be confused with operational state.

## Confirmation principle

RufusChat may suggest actions, but it does not execute mutating operations without explicit user confirmation.

That includes, at minimum:

- creating state
- injecting context
- creating anchors
- any future Hermes real execution path
- any future Codex execution path

## Current foundation scope

This directory currently contains foundation documentation, a shared metadata module, and a placeholder extension entrypoint.
The goal is to establish the contract and boundary before implementing a larger product surface.

## Current prototype integration

`scripts/rufuschat-ui-server.mjs` now consumes the RufusChat extension metadata boundary in a minimal way.
It reflects the current product-surface status in the UI and exposes a safe `/api/rufuschat` view for the prototype.

Full migration from the prototype server into the extension surface is deferred to a later phase.
