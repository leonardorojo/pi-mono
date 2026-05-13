# RCK Core adapter contract

## Purpose

RufusChat must talk to RCK through a stable adapter boundary.
The UI/product surface must not know about provider storage internals, and it must not depend on direct `.pi/rck/` layout details.

Current provider:
- Pi RCK Bridge

Future provider:
- RCK Core Kernel
- RufusLab.RCK.Cli

The adapter contract is what lets RufusChat keep the same UI-facing shape while the provider implementation changes underneath it.

## Provider roles

### Current provider: Pi RCK Bridge

Current bridge commands/API surface includes:
- `/rck state`
- `/rck inject`
- `/rck anchor`
- `/rck status`
- `/rck list`
- `/rck supervise`
- `/hermes fake`

Current storage/runtime boundary:
- `.pi/rck/`

The bridge is the active provider today, but it is only one implementation of the provider role.

### Future provider: RCK Core Kernel

The future provider should be backed by:
- RCK Core Kernel
- RufusLab.RCK.Cli

That provider should expose a formal Trace DAG model built from:
- states
- deltas
- anchors

It should also formalize:
- evidence handling
- safe context generation
- trace lifecycle management

## Adapter boundary

```text
RufusChat Product Surface
  -> RufusChat Backend Adapter
  -> RckProvider
  -> current/future provider implementation
```

The UI must only consume safe DTOs returned by the adapter.
It must not call the provider directly.

## Required adapter methods

### Read-only

- `getStatus()`
- `getInventory()`
- `getSupervision()`
- `getCurrentTrace()`
- `getSafeContext()`
- `listTraces()`
- `getTrace(traceId)`
- `getTraceTimeline(traceId)`

### Mutating

- `createState(request)`
- `createDelta(request)` future
- `createAnchor(request)`
- `injectContext(request)`
- `switchTrace(traceId)` future
- `createTrace(request)` future
- `closeTrace(traceId)` future

### Executor-related

- `runHermesFake(request)`
- `runHermesRealGated(request)` future
- `runCodex(request)` future

## DTO families

The adapter should normalize provider output into safe DTO families rather than exposing provider-native shapes.

- Project DTOs
- Chat DTOs
- RCK Trace DTOs
- RCK State DTOs
- RCK Delta DTOs
- RCK Anchor DTOs
- Context Pack DTOs
- Supervision DTOs
- Evidence Ref DTOs
- Adapter Error DTOs

## Safety contract

- No raw evidence by default
- No raw stdout/stderr by default
- Evidence refs are reference-only unless a later phase explicitly expands access
- Mutating actions require user confirmation
- Context injection requires user decision
- Provider errors must be normalized into safe adapter errors
- UI must not read `.pi/rck` directly
- UI must not depend on CLI text output
- No silent provider switching

## Migration strategy

### M1
Current Pi RCK Bridge remains the provider behind the same adapter interface.

### M2
Implement RCK Core provider behind the same adapter interface.

### M3
Run dual-read or smoke comparison between bridge and core.

### M4
Switch the default provider to RCK Core.

### M5
Retire bridge-specific assumptions.

## Non-goals

- no RCK Core integration in 9D
- no CLI invocation implementation
- no storage migration
- no UI rewrite
- no chat memory implementation
- no Codex
- no Hermes real
