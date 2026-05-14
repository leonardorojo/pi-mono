# RufusChat checkpoints

## Purpose

Checkpoints are product-side markers for decisions, milestones, and notable states in the active chat.
They help the user record a safe product action without turning the UI into a technical RCK view.

## What a checkpoint is

- A user-approved product checkpoint
- Stored in ProductState under the active chat
- Linked to related chat messages through `Message.links.checkpointId`
- Visible in the chat UI as a minimal, ChatGPT-like action result

## What a checkpoint is not

- Not an RCK Core anchor
- Not a Trace DAG node
- Not semantic memory
- Not raw evidence storage
- Not a frontend read of `.pi/rck`
- Not a technical dashboard

## Relationship with RCK anchors

Checkpoint UX is a product boundary only.
Future phases may map checkpoints to real RCK anchors, but 14A does not create any anchor or trace object.

## ProductState schema placeholder

Per chat:

- `checkpoints?: CheckpointHistoryItem[]`

`CheckpointHistoryItem` concept:

- `checkpointId`
- `status`
- `label`
- `summary`
- `createdAt`
- `updatedAt`
- `sourceMessageId`
- `resultMessageId`
- `sourceKind`
- `safeMetadata`

## Safety rules

- Keep messages short and safe
- Do not store raw evidence, stdout, stderr, or tokens
- Do not expose `.pi/rck` paths in the UI
- Keep the UX conversational, not technical

## Future phases

- Supersede/archive flows
- RCK anchor mapping
- Trace integration
- Optional richer product governance summaries
