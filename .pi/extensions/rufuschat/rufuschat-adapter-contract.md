# RufusChat adapter contract

## Purpose

This document defines the conceptual boundary for the RufusChat product surface.
The UI should talk to a RufusChatBackendAdapter, and the adapter should talk to an RckProvider.

This is a contract sketch, not a runtime implementation.

## Boundary chain

```text
RufusChatProductSurface
  -> RufusChatBackendAdapter
  -> RckProvider
```

## Conceptual responsibilities

### RufusChatProductSurface

Owns:
- chat-oriented UI
- confirmation gates
- presentation of safe context and trace summaries
- action intent, not action side effects

Must not:
- read `.pi/rck/` directly
- depend on raw evidence
- assume the provider implementation details

### RufusChatBackendAdapter

Owns:
- turning provider outputs into UI-ready DTOs
- safe normalization and capability checks
- mapping product actions to provider calls
- keeping the product surface isolated from storage layout

Must not:
- leak raw evidence by default
- expose provider internals as UI contract
- become a thin alias for `.pi/rck/` files

### RckProvider

Owns:
- the concrete RCK-backed implementation
- current bridge-backed behavior
- future kernel-backed behavior

Current provider:
- Pi RCK Bridge

Future provider:
- RCK Core Kernel / RufusLab.RCK.Cli

## Conceptual methods

The surface can expose the following operations conceptually:

- `getProject()`
- `getChatSessions()`
- `getCurrentChat()`
- `getRckStatus()`
- `getCurrentTrace()`
- `getSafeContext()`
- `createState()`
- `injectContext()`
- `createAnchor()`
- `runHermesFake()`
- `runHermesRealGated()` future
- `runCodex()` future

## Method notes

### Read-only methods

These should return safe, UI-ready data only:

- `getProject()`
- `getChatSessions()`
- `getCurrentChat()`
- `getRckStatus()`
- `getCurrentTrace()`
- `getSafeContext()`

### Mutation-capable methods

These are conceptually available, but the surface must gate them behind explicit user confirmation:

- `createState()`
- `injectContext()`
- `createAnchor()`
- `runHermesFake()`

Future gated operations:

- `runHermesRealGated()`
- `runCodex()`

## Safety rules

- No raw evidence by default
- No direct `.pi/rck/` coupling in the UI layer
- No silent mutations
- No real Hermes execution yet
- No Codex execution yet
- Safe summaries and references only unless a later phase explicitly expands the contract

## Adapter shape expectation

The adapter should normalize provider output into a stable set of safe DTOs such as:

- project metadata
- chat session metadata
- current chat metadata
- RCK status
- current trace summary
- safe context pack summary
- anchor summary
- action result summary

The exact TypeScript shape can be added later once the product surface is ready.
