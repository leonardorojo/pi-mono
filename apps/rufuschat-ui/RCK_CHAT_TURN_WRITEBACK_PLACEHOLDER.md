# RCK chat turn write-back placeholder

This phase wires the existing RCK write-back contract into the closed-turn RufusChat flow.

The execution boundary for the future adapter/service is documented in [`RCK_WRITEBACK_EXECUTION_BOUNDARY.md`](./RCK_WRITEBACK_EXECUTION_BOUNDARY.md).

This flow runs inside an existing Branch; it does not create the Project/Trace or Chat/Branch birth artifacts.

It is placeholder-only:
- no real RCK Core write
- no `.rck` reads
- no `.rck` writes
- no `.data` writes
- no product-state schema changes
- no Anchor creation
- no RCK DAG mutation
- no extra LLM call
- no embeddings
- no ranking

The source of truth for the preview is `chatTurnWritebackResultsByChatId`; the turn ID chain is derived from the most recent result in memory.

## Hook point

The placeholder runs after a chat completion finishes and the assistant response is closed.

Flow:

chat turn complete
→ build ChatTurnStatePayload
→ build ChatTurnDeltaPayload
→ call `registerChatTurnPlaceholder(...)`
→ keep the result in UI memory only
→ show the dev-only preview in the RCK side panel

## Registration rule

The placeholder registration only happens when the assistant response is complete.

It does not run when:
- the user is typing
- the request is still streaming
- the assistant response is empty
- the completion fails

In this phase, completion means the request finished successfully and the assistant text is non-empty.

## approvedRckContext behavior

When the next completion uses an approved RCK context injection, the state payload reflects it in:
- `contextUsed.approvedRckContext.used`
- `contextUsed.approvedRckContext.injectionId`
- `contextUsed.approvedRckContext.sourceTraceSliceHashes`
- `contextUsed.approvedRckContext.contextPackReference` when available

When no approved context was used, the placeholder still records the closed turn with `used: false`.

## UI preview

The dev-only panel shows:
- status: placeholder / not connected
- last registered turn ID
- state kind
- delta kind
- state ID: null
- delta ID: null
- `approvedRckContext.used`
- injection ID when present

`approvedRckContext.used` is one-shot: it is true only on the turn that consumes the approved context and false again on the next message.

The full payload is not shown by default.

## Negative behavior

If chat completion fails, the placeholder does not register a state or delta.

That keeps the preview honest: only closed turns become write-back candidates.

## Future step

A later phase can replace the placeholder with a real adapter call into RCK Core, keeping the same contract boundary and the same payload shape.
