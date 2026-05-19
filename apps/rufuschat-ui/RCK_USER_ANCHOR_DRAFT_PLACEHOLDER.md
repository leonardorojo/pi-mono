# RCK User Anchor Draft placeholder

This document defines the local-only placeholder behavior for a User Anchor Draft in RufusChat.

It is intentionally UI/runtime-only:
- no real Anchor is created
- no RCK Core call is made
- no `.rck` is written
- no `.data` RCK is written
- no RCK DAG mutation happens
- no Manual Delta is reintroduced
- no extra LLM call is made

The draft is created only from the current chat and the latest closed turn write-back placeholder result.
The source of truth for the closed turn is `chatTurnWritebackResultsByChatId`.

## Conceptual shape

```json
{
  "kind": "rufuschat.user_anchor_draft",
  "schemaVersion": "rufuschat.user_anchor_draft.v0",
  "status": "placeholder",
  "anchorDraftId": "...",
  "projectId": "...",
  "traceId": "...",
  "chatId": "...",
  "branchId": "...",
  "branchKind": "main",
  "sourceTurnId": "...",
  "anchorType": "user_decision",
  "title": "Anchor draft from latest chat turn",
  "summary": "Placeholder draft derived from the latest closed turn. No RCK anchor was written.",
  "createdAt": "...",
  "source": {
    "kind": "chat_turn",
    "turnId": "..."
  },
  "rck": {
    "connected": false,
    "anchorId": null
  }
}
```

## Button availability

The button is enabled only when:
- a current project exists
- a current chat exists
- `traceId` is available on the chat or project
- `branchId` is available on the chat
- a closed turn exists in `chatTurnWritebackResultsByChatId`

If no closed turn is available, the UI must show a clear disabled state such as:
- `No closed turn available for anchor draft.`

## Runtime storage

The placeholder draft is stored in memory in a chat-scoped map such as:
- `userAnchorDraftsByChatId`

This storage is local-only and may disappear on reload.
It is separate from chat-turn write-back state.

## UI preview

The preview should make the boundary explicit:
- `Status: placeholder / not connected`
- `RCK anchor ID: null`
- `This is a placeholder draft. No RCK anchor was written.`

The preview should reference:
- `projectId`
- `traceId`
- `chatId`
- `branchId`
- `branchKind`
- `sourceTurnId`

## Relationship to future work

This draft is not a Trace Anchor, Branch Anchor, or Merge Anchor.
It is only an explicit user-facing draft placeholder that can later be connected to a real anchor lifecycle.
