# RCK write-back design for RufusChat

This document defines the broader future direction for writing real RufusChat conversation traces back into RCK Core without implementing product write-back yet.

The runtime execution boundary for the current phase is documented separately in [`RCK_WRITEBACK_EXECUTION_BOUNDARY.md`](./RCK_WRITEBACK_EXECUTION_BOUNDARY.md).

It is a design-only contract. It does not change runtime behavior, product state schema, `.data`, `.rck`, embeddings, ranking, or chat flow.

Project creation creates a Trace and Chat creation creates a Branch inside that Trace; this document starts after that birth layer and focuses on how closed turns grow the Trace.

The contract-shape layer for the birth step itself is documented in [`RCK_TRACE_BRANCH_CONTRACT_SHAPES.md`](./RCK_TRACE_BRANCH_CONTRACT_SHAPES.md).

## Goal

RufusChat should eventually be able to convert a completed chat conversation into an RCK trace that grows from chat turns:

chat turn
→ RckState
→ RckDelta
→ future TraceSlice
→ future ContextPack

The key boundary is that RufusChat prepares and submits trace write-back through an adapter/contract layer. RufusChat must not reach into RCK Core internals directly.

## Scope of this phase

This phase defines the shape, lifecycle, and ownership rules for write-back.

Phase 28 then wires the stub into the closed-turn chat flow so RufusChat can build a placeholder write-back preview after each completed assistant response, without connecting to real RCK Core.

In scope:
- formal State / Delta concepts for a closed chat turn
- adapter boundary names and responsibilities
- auto vs manual registration modes
- anchor policy
- evidence and artifact references
- future lifecycle from chat turn to trace to context

Out of scope:
- real RCK Core write operations
- running RCK CLI
- reading `.rck`
- generating TraceSlice for real
- generating ContextPack for real
- `.data` persistence
- product-state schema changes
- anchor creation
- embeddings
- ranking
- any new LLM call
- any change to existing chat completion flow

## Core design principle

RufusChat is the conversation product shell. RCK Core remains the kernel.

The write-back path must respect a strict boundary:
- RufusChat collects evidence from a closed turn
- RufusChat builds a State payload and a Delta payload
- an adapter/contract layer mediates registration
- RCK Core remains opaque to the UI and product flow

This keeps product behavior stable while making trace write-back formally describable.

## What is a ChatTurnState?

A ChatTurnState represents one closed conversational turn after the assistant response is complete.

A turn is considered closed only when there is enough evidence to record both sides of the interaction:
- user message
- assistant response
- timestamps
- completion metadata
- optional approved RCK context used for that completion
- references to any artifacts or evidence that matter

A ChatTurnState is not a streaming fragment and is not a user draft.

### Required conceptual fields

A future ChatTurnState payload should be able to represent at least:
- chatId
- turnId
- userMessage
- assistantMessage
- timestamps
- completion metadata
- approvedRckContext used, if any
- artifacts referenced
- evidence refs
- model/provider info
- minimal UI/session metadata

### Conceptual payload

```json
{
  "kind": "rufuschat.chat_turn_state",
  "schemaVersion": "rufuschat.chat_turn_state.v0",
  "chatId": "...",
  "turnId": "...",
  "userMessage": {
    "messageId": "...",
    "text": "..."
  },
  "assistantMessage": {
    "messageId": "...",
    "text": "..."
  },
  "usedApprovedRckContext": {
    "injectionId": "...",
    "sourceTraceSliceHashes": [],
    "contextPackReference": null
  },
  "artifacts": [],
  "evidence": {
    "provider": "...",
    "model": "...",
    "requestMetadata": {},
    "responseMetadata": {}
  }
}
```

### Notes

- `usedApprovedRckContext` is present only when a confirmed injection actually participated in the completion.
- `contextPackReference` is conceptual in this phase and may be `null`.
- `artifacts` can later include files, links, generated outputs, or source documents.
- The payload should remain product-oriented and not expose RCK internals directly in the UI.

## What is a ChatTurnDelta?

A ChatTurnDelta connects one closed chat turn state to the next closed chat turn state.

It captures the transition:
previousStateId → newStateId

The Delta is the connective tissue for trace growth. It records that a new chat turn has been appended, not that the entire trace was reconstructed.

### Required conceptual fields

A future ChatTurnDelta payload should be able to represent at least:
- previousStateId / fromTurnId
- newStateId / toTurnId
- reason
- operations
- chatId
- usedContextInjectionId when applicable

### Conceptual payload

```json
{
  "kind": "rufuschat.chat_turn_delta",
  "schemaVersion": "rufuschat.chat_turn_delta.v0",
  "reason": "assistant_response_added",
  "chatId": "...",
  "fromTurnId": "...",
  "toTurnId": "...",
  "operations": [
    {
      "op": "append_chat_turn",
      "turnId": "..."
    }
  ],
  "usedContextInjectionId": "..."
}
```

### Notes

- A Delta describes the append operation between turns.
- It should be small, explicit, and traceable.
- It should not imply automatic anchor creation.

## When write-back is registered

Recommended registration rule:

- do not register when the user message is first written
- do not register during partial streaming
- do register only after the assistant response is closed
- if completion fails, error registration belongs to a future phase, not this one
- if approved RCK context was used, include the injectionId and the relevant metadata

This keeps write-back aligned with stable evidence, not transient in-flight output.

## Evidence and artifacts

The write-back payload must be able to reference the kinds of evidence RufusChat already knows about or may know later:

- ContextPack used
- TraceSlice hashes used
- files or docs referenced
- outputs generated
- provider / model / requestId / metadata
- source documents, when applicable

In this phase, these are only design references. No real persistence or trace registration happens yet.

## Adapter boundary

RufusChat must not call RCK Core internals directly.

The future implementation should pass through a small contract boundary. Proposed module names:

- `apps/rufuschat-ui/rck-writeback-contract.mjs`
- `apps/rufuschat-ui/rck-writeback-provider.mjs`

These are future-facing names only. They are not implemented in this phase unless added as harmless stubs that do not affect runtime.

### Future responsibilities

Planned functions:
- `buildChatTurnStatePayload(...)`
- `buildChatTurnDeltaPayload(...)`
- `registerChatTurn(...)`
- `buildManualDeltaDraft(...)`
- `confirmManualDeltaRegistration(...)`

Suggested separation:
- contract module: build payloads and define shapes
- provider module: execute or adapt the write-back action behind the boundary

## Automatic vs manual registration

There are two future modes.

### Automatic mode

After the assistant response is complete:
- build the State payload
- build the Delta payload
- the write-back adapter registers the trace update

This is the default conceptual path for ordinary closed turns.

### Manual mode

A future UI action named `Register RCK Delta` should let the user preview and confirm a more deliberate write-back event.

Expected use cases:
- mark a decision
- mark a conceptual change
- mark a milestone
- prepare a future Anchor candidate

Manual mode should preview the State and Delta before registration.

## Button concept: Register RCK Delta

Tentative future button name:
- `Register RCK Delta`

Purpose:
- create a deliberate write-back event
- let the user review the evidence before registration
- support future “this was a decision” or “this was a milestone” moments

Important:
- this button does not auto-create anchors
- it should preview the derived State/Delta first
- user confirmation is required before registration

## Anchor policy

Anchors must be handled conservatively.

Strict rules:
- no anchor for every turn
- no anchor for every injection
- no anchor for technical context use
- no implicit anchor creation from write-back
- anchor only when the user explicitly says something is a decision, milestone, or stable concept

Example:
User says: “Esto dejémoslo como decisión.”

Expected future behavior:
- preview Anchor
- user confirms
- create Anchor

This keeps anchors meaningful instead of noisy.

## Future lifecycle

The target end-to-end direction is:

Chat turn complete
→ register State / Delta
→ trace grows
→ user asks Attach RCK Context
→ scope suggestion over real trace
→ user approves scope
→ generate real TraceSlice
→ generate real ContextPack
→ preview
→ confirm injection
→ next completion one-shot

This is the intended reverse direction from the already-published approved-context flow.

## Relationship to the already published context flow

The currently published direction is:

Attach RCK Context
→ Scope suggestion
→ Load ContextPack JSON
→ Confirm injection
→ approvedRckContext in next chat completion
→ one-shot consumption

This document defines the inverse direction:

RufusChat
→ RCK State / Delta
→ trace real
→ future TraceSlice real
→ future ContextPack real

Both directions should meet at the same adapter boundary, but they solve different problems.

## What is explicitly not part of this phase

Do not implement:
- real write-back to RCK Core
- real CLI execution
- reading `.rck`
- real TraceSlice generation
- real ContextPack generation
- `.data` writes
- anchor recording
- chat flow changes
- UI production changes
- embeddings
- ranking
- semantic selection
- additional LLM calls

## Brief documentation updates elsewhere

This phase introduces the first contract/stub modules: `rck-writeback-contract.mjs` and `rck-writeback-provider.mjs`.
- `apps/rufuschat-ui/README.md`
- `apps/rufuschat-ui/RCK_APPROVED_CONTEXT_COMPLETION.md`
- `apps/rufuschat-ui/RCK_CONTEXT_SCOPE_SUGGESTION.md`

Those files should only get brief notes pointing to this design and preserving the phase boundary.

## Open questions for implementation

When the design becomes code, the implementation will need to settle:
- how a closed turn is identified in the UI session model
- where completion metadata is stored transiently before write-back
- how the adapter packages evidence refs without exposing internals
- whether manual delta previews reuse the same payload builder as automatic mode
- whether trace registration is synchronous, queued, or optimistic

Those questions are intentionally left open in this phase.
