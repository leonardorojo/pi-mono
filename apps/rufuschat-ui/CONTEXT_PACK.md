# RufusChat Context Pack Boundary v0

## Purpose

Context Pack is a safe abstraction for preparing context that may later be injected into a RufusChat conversation.
It defines the boundary for a user-governed product artifact that can summarize and select information without exposing raw internal evidence by default.

This boundary exists to keep RufusChat product-shaped while preserving the separation between:
- RufusChat as the conversation / governance surface
- RCK as the audit / trace system
- Context Pack as the safe abstraction layer between future sources and chat injection

## North Star

- RufusChat conversa / gobierna.
- RCK audita.
- Context Pack abstrae.
- El usuario decide cuándo inyectar.
- The UI does not read `.pi/rck` directly.

## What a Context Pack is

A Context Pack is:
- a safe summarized / selected context package
- a candidate to be injected into a chat
- a product artifact governed by the user
- a bridge between future auditable sources and conversation

A Context Pack can be associated with a project, a chat, and a set of safe items that explain why the pack exists and what it is meant to support.

## What a Context Pack is NOT

A Context Pack is not:
- an RCK Trace DAG
- raw evidence
- a full transcript
- final semantic memory
- Hermes execution
- raw output from tools
- storage of `.pi/rck`
- a technical dashboard

## Relationship with RCK

- RCK registers and audits.
- Future RCK Core may produce or back safe sources for Context Packs.
- Context Packs may reference RCK artifacts through safe IDs.
- The UI must not read `.pi/rck` directly.
- Raw states, deltas, anchors, and evidence are not exposed by default.

## Relationship with ProductState

- ProductState may store safe metadata about Context Packs.
- Chat transcript remains Product Data.
- `Message.links.contextPackId` can link a message to a Context Pack.
- Context Pack does not replace chat messages.
- Context Pack does not replace the RCK Trace DAG.

## Schema placeholder v0

This is a conceptual Markdown contract only. It is not a runtime schema implementation.

### ContextPack

```ts
type ContextPack = {
  id: string;
  projectId: string;
  chatId: string;
  status: 'candidate' | 'injected' | 'rejected' | 'expired';
  title: string;
  summary: string;
  createdAt: string;
  updatedAt: string;
  injectedAt?: string | null;
  source: ContextPackSource;
  items: ContextPackItem[];
  safeMetadata?: object;
};
```

### ContextPackSource

```ts
type ContextPackSource = {
  kind: 'manual' | 'rck' | 'product-state' | 'hermes' | 'future';
  provider?: 'pi-rck-bridge' | 'rck-core-kernel' | 'rufuschat' | string;
  refId?: string | null;
};
```

### ContextPackItem

```ts
type ContextPackItem = {
  id: string;
  kind: 'summary' | 'decision' | 'checkpoint' | 'trace-ref' | 'message-ref' | 'note';
  title: string;
  content: string;
  safeRef?: string | null;
  included: boolean;
};
```

### Message.links

```ts
type Message = {
  links?: {
    contextPackId?: string | null;
  } | null;
};
```

## Lifecycle

The intended lifecycle is:

- `candidate` → `injected` → optionally referenced by message links
- `candidate` → `rejected`
- `candidate` → `expired`

Rules:
- Injection always requires explicit user decision.
- No silent auto-injection.
- A Context Pack may exist as a candidate without being injected.

## /inject UX boundary

Future desired UX for `/inject`:

1. prepare a Context Pack candidate
2. show a safe summary
3. offer `Inject` / `Cancel`
4. if the user confirms, mark the Context Pack as `injected`
5. add a message to the chat with safe metadata
6. do not reveal raw evidence

Fase 13A only documents this boundary.
It does not implement `/inject` runtime behavior.

## Safety rules

- no raw evidence by default
- no stdout / stderr bruto
- no environment variables
- no tokens
- no credentials
- no stack traces
- no unnecessary internal paths
- no direct frontend read of `.pi/rck`
- no silent auto-injection
- user approval is required before injection
- no technical dashboard surface in the product UI

## Future phases

- 13A — Context Pack boundary / contract
- 13B — Inject UX candidate placeholder
- 13C — Message links for `contextPackId`
- 13D — Per-chat injection history
- 14 — Checkpoint UX
- 15 — RCK Provider boundary real
- 16 — Semantic memory v0

## Notes

- Context Pack abstracts.
- RCK audits.
- RufusChat remains the conversation surface.
- The UI should stay ChatGPT-like, not become a technical RCK console.
- Any runtime injection behavior belongs to later phases.
