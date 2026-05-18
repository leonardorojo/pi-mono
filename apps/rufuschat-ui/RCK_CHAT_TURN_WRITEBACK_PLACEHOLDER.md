# RCK chat turn write-back placeholder

This phase wires the existing RCK write-back contract into the closed-turn RufusChat flow.

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

The full payload is not shown by default.

## Negative behavior

If chat completion fails, the placeholder does not register a state or delta.

That keeps the preview honest: only closed turns become write-back candidates.

## Future step

A later phase can replace the placeholder with a real adapter call into RCK Core, keeping the same contract boundary and the same payload shape.
