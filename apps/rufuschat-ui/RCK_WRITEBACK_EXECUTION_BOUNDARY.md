# RCK write-back execution boundary

This document defines the runtime boundary between RufusChat and the future RCK write-back adapter/service.

It is design-only. It does not change runtime behavior, persistence, or the stabilized placeholder write-back flow already present in `chatTurnWritebackResultsByChatId`.

Project/Trace birth and Chat/Branch birth are documented separately in [`RCK_TRACE_INITIALIZATION_PLACEHOLDER.md`](./RCK_TRACE_INITIALIZATION_PLACEHOLDER.md); this doc only covers closed-turn write-back and does not create traces, branches, or anchors.

## One-sentence boundary

RufusChat prepares a closed chat-turn request and hands it to an adapter/service; the adapter/service owns all future RCK Core interaction.

RufusChat must not reach into RCK Core internals directly.

## Responsibilities

### RufusChat responsibilities

RufusChat owns:

- detecting when a chat turn is closed
- collecting the user message, assistant message, and completion metadata
- carrying forward the one-shot `approvedRckContext` usage flag
- building the request payload for write-back
- keeping the current placeholder result in UI memory
- showing a safe preview of the current placeholder outcome
- preserving the current chat flow, including approved-context completion behavior

RufusChat does not own:

- RCK Core registration
- `RckState` creation inside Core internals
- `RckDelta` creation inside Core internals
- anchor creation
- `.rck` reads or writes
- `.data` persistence for the write-back boundary

### Adapter/service responsibilities

The adapter/service owns:

- validating the write-back request
- normalizing the request into a future RCK-friendly shape
- creating or translating the future real `RckState`
- creating or translating the future real `RckDelta`
- deciding whether the write-back is registered, queued, unavailable, or errored
- returning a stable result envelope to RufusChat
- encapsulating any future RCK Core calls behind one boundary

The adapter/service must be the only layer that knows how RufusChat maps to RCK Core internals.

## What RufusChat delivers

The conceptual request object should look like this:

```json
{
  "kind": "rufuschat.rck_writeback_request",
  "schemaVersion": "rufuschat.rck_writeback_request.v0",
  "chatTurnStatePayload": {},
  "chatTurnDeltaPayload": {},
  "metadata": {
    "source": "rufuschat-ui",
    "mode": "chat_turn_auto_writeback"
  }
}
```

### Request payload responsibilities

#### `chatTurnStatePayload`

This is the closed-turn snapshot.

It carries the evidence needed to describe one finished chat turn:

- chat identity
- turn identity
- user message
- assistant message
- completion metadata
- evidence metadata
- tool / artifact / evidence references when present
- `approvedRckContext` usage information

#### `chatTurnDeltaPayload`

This is the append transition.

It says that the chat trace advanced from one closed turn to the next closed turn.

It carries:

- `fromTurnId`
- `toTurnId`
- the append operation
- the injection ID when a confirmed approved context participated in that completion

#### `metadata`

Metadata is for the adapter/service boundary, not for RCK Core internals.

Minimal conceptual fields:

- `source`: `rufuschat-ui`
- `mode`: `chat_turn_auto_writeback`
- optional UI / session trace fields if needed later

## What the adapter/service returns

The conceptual response object should look like this:

```json
{
  "ok": true,
  "status": "registered",
  "stateId": "rck_state_id",
  "deltaId": "rck_delta_id",
  "traceSliceHash": null,
  "warnings": []
}
```

### Response field meaning

- `ok`: overall success flag
- `status`: boundary state for the result
- `stateId`: future RCK state identifier, or `null` while placeholder-only
- `deltaId`: future RCK delta identifier, or `null` while placeholder-only
- `traceSliceHash`: future trace-slice hash, or `null` while placeholder-only
- `warnings`: non-fatal notes for the UI or logs

### Placeholder response today

In this phase, the provider remains placeholder / not connected.

So the current outcome must stay effectively:

- `ok: true`
- `status: placeholder` or `not_connected` at the boundary level
- `stateId: null`
- `deltaId: null`
- `traceSliceHash: null`
- `warnings: []` or a small safe warning list

No real RCK registration occurs yet.

## Boundary states

These conceptual states describe the boundary, not a new product feature.

- `not_connected` — the real adapter is not wired in yet
- `placeholder` — the boundary is active but the result is still dev-safe and local only
- `ready_for_adapter` — RufusChat has the right request shape and can hand it to a real adapter later
- `adapter_unavailable` — the adapter could not be reached or loaded
- `adapter_error` — the adapter failed while processing a valid request
- `registered` — the future success state when real registration happens

The current runtime remains in placeholder / not connected mode.

## Error model

The boundary should distinguish between request problems and execution problems.

Recommended conceptual error classes:

- `validation_error` — the request is malformed or incomplete
- `adapter_unavailable` — the adapter cannot be reached, loaded, or initialized
- `adapter_error` — the adapter failed unexpectedly during processing
- `registration_error` — the adapter reached the real RCK side but registration failed

A safe error response can look like this:

```json
{
  "ok": false,
  "status": "adapter_error",
  "error": {
    "code": "adapter_error",
    "message": "RCK write-back adapter failed."
  },
  "warnings": []
}
```

Error rules:

- do not leak raw internals into the UI
- keep error messages short and product-safe
- preserve the placeholder flow even when the future adapter is unavailable
- keep response shapes stable across phases

## Guarantees of no mutation in this phase

This phase must not mutate any real RCK storage or DAG state.

Specifically, it must not:

- write `.rck`
- read `.rck`
- write `.data`
- mutate the RCK DAG
- create anchors
- implement Manual Delta
- add Anchor preview UI
- change the stabilized placeholder write-back semantics
- change approved-context one-shot behavior
- add a new LLM call

The boundary exists only as a contract and placeholder-safe design.

## Relationship to `ChatTurnStatePayload`

`ChatTurnStatePayload` is the authoritative closed-turn snapshot.

It is the main thing RufusChat has to assemble before any future registration attempt.

It should remain product-oriented and should not expose RCK Core internals directly in the UI.

The currently stabilized placeholder state already records the important closed-turn fields in memory, including the one-shot approved-context usage flag.

## Relationship to `ChatTurnDeltaPayload`

`ChatTurnDeltaPayload` is the append transition that links one closed turn to the next.

It should stay small, explicit, and traceable.

It should not imply any automatic anchor creation or any change to the RCK DAG beyond the future adapter/service boundary.

## Relationship to `approvedRckContext`

The approved context is one-shot.

The boundary must preserve the current behavior:

- normal message: `approvedRckContext.used = false`
- message that consumes a confirmed injection: `approvedRckContext.used = true`
- next message without a new confirmation: `approvedRckContext.used = false`

When the approved context is used, the request should carry:

- `injectionId`
- `sourceTraceSliceHashes`
- any safe reference needed for the future adapter/service boundary

This keeps the approved-context flow separate from write-back registration while still allowing the boundary to record that the context participated in the turn.

## Why Anchor stays out of scope

Anchor is intentionally excluded because it is a separate semantic decision layer.

Reasons it stays out of this phase:

- not every turn deserves an anchor
- not every approved context deserves an anchor
- anchors should represent decisions or durable milestones, not ordinary closed turns
- mixing anchors into write-back would blur the boundary between append-only turn registration and semantic marking

So there is no Anchor button, no Anchor preview, and no implicit anchor creation here.

## Why Manual Delta stays out of scope

Manual Delta is paused and remains outside this phase.

Reasons it stays out:

- the old manual-delta path was the source of state drift risk
- it mixes a human-driven registration workflow with the automatic closed-turn placeholder flow
- it can reintroduce divergence between preview state, panel state, and chat selection state
- the current phase must preserve the stabilized placeholder semantics first

So this phase does not touch Manual Delta or the paused manual-delta branch path.

## How this prepares Fase 15B

Fase 15A is the boundary-definition step.

It prepares the next phase by giving the implementation a stable place to connect real behavior later:

1. keep the request/response contract stable
2. keep placeholder results safe and local
3. keep `ChatTurnStatePayload` and `ChatTurnDeltaPayload` shapes explicit
4. keep `approvedRckContext` semantics stable
5. keep the adapter/service boundary isolated from RCK Core internals

A future Fase 15B can then add the first invocation stub or adapter handoff without changing the semantics defined here.

A later phase can connect the real RCK adapter/service once the core implementation is ready.

## Relationship to the existing write-back docs

This document is the canonical runtime boundary.

The older design, contract, placeholder, and state-model docs still matter, but they now sit underneath this execution boundary:

- `RCK_WRITEBACK_DESIGN.md` — broader architectural direction
- `RCK_WRITEBACK_ADAPTER_CONTRACT.md` — adapter payload normalization and stub contract
- `RCK_CHAT_TURN_WRITEBACK_PLACEHOLDER.md` — closed-turn placeholder flow
- `RCK_WRITEBACK_STATE_MODEL.md` — in-memory source of truth and one-shot behavior

## Current runtime status

Current provider status remains:

- placeholder
- not connected
- in-memory only
- no RCK Core calls
- no persistence

That is the correct runtime state for this phase.
