# RCK write-back state model

This phase stabilizes the in-memory state model for the dev-only RCK write-back preview.

## Source of truth

The current source of truth is `chatTurnWritebackResultsByChatId`.

- It stores the most recent placeholder registration result for each chat.
- The preview panel reads from that map.
- The next closed turn derives its parent chain from the latest stored result.

## Input and result flow

The write-back preview is built entirely in memory from the closed chat turn input.

- user message
- assistant message
- completion metadata
- approved RCK context, if any
- generated turn ID

The placeholder registration result stays in UI memory only.

## When a turn is considered closed

A turn is closed only when both conditions are true:

- completion finished with OK status
- assistant text is non-empty

If the completion fails or the assistant text is empty, no placeholder write-back is registered.

## Parent turn and delta chain

The parent chain is derived from the most recent stored write-back result for the chat.

- `parentTurnId` comes from `result.statePayload.chat.turnId` when available
- otherwise it falls back to `result.deltaPayload.toTurnId`
- the new turn uses its own generated `turnId`
- the delta chain follows the same turn IDs without requiring a separate parent-ID cache

## approvedRckContext one-shot behavior

Approved context is consumed once.

- `approvedRckContext.used = true` only on the turn that consumes the approved context
- the next message shows `approvedRckContext.used = false`
- the injection ID is present only on the consuming turn
- source trace slice hashes are preserved on the consuming turn

## Non-persistence boundaries

This phase does not write persistent RCK state.

- no `.data`
- no `.rck`
- no RCK DAG mutation
- no Anchor creation
- no manual RCK Delta implementation

The preview remains a local UI-only registration result.

## Phase notes

- Manual Delta stays out of scope for this phase.
- The phase-29 stash/manual-delta path was not used.
