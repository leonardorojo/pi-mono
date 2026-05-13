# RufusChat Product Data Persistence Boundary v0

## Purpose

This document defines the *product data* persistence boundary for RufusChat.
It is the contract for a future backend-local JSON store that remembers the user-facing product state of the official RufusChat UI.

This boundary exists to keep the product surface stable while preserving the separation between:
- RufusChat as the conversation and governance surface
- RCK as the audit / trace system
- Product Data as local application state, not trace evidence

## Product Data vs RCK Data

### Product Data
Product Data is the local state the RufusChat product surface needs in order to reopen the same projects, chats, and message history.
It is *user-facing application state*.

### RCK Data
RCK Data is the audit / trace layer.
It belongs to trace DAGs, checkpoint evidence, supervision metadata, and related RCK artifacts.

### Boundary rule
- Product Data may reference RCK artifacts by safe ID or status.
- Product Data must not become a storage substitute for RCK Trace DAGs.
- A persisted chat transcript is **not** the same thing as a trace DAG.
- Context injection remains an explicit user action.

## What is persisted in v0

In Product Data v0, persist the browser-visible product workspace state:
- projects
- chats
- chat messages
- current project selection
- current chat selection
- safe status fields that help the product restore its own state

This is the minimum state needed for the product surface to reopen consistently.

## What is not persisted in v0

The following are explicitly out of scope for Product Data v0:
- runtime execution state
- live backend process state
- raw tool output
- raw evidence
- raw stdout / stderr
- environment variables
- tokens
- credentials
- secrets intentionally entered for storage
- LLM provider state
- Hermes execution state
- Codex execution state
- RCK Trace DAGs
- semantic memory as a final system of record
- browser-local `localStorage`
- any production dashboard / technical cockpit behavior

## Chosen storage

**Decision:** backend-local JSON file.

This is the official Product Data v0 storage choice.

It keeps persistence local to the RufusChat backend boundary without turning the UI into a technical RCK datastore.

## Data path

Future runtime path:

```text
apps/rufuschat-ui/.data/rufuschat-product-state.json
```

Rules:
- runtime local only
- not committed
- not RCK
- not evidence
- not semantic memory final
- not Trace DAG

## Git ignore rule

The repo must ignore the runtime data directory:

```gitignore
apps/rufuschat-ui/.data/
```

## Schema v0

The schema below is the contract for the future persisted JSON document.

### ProductState

```ts
type ProductState = {
  version: string; // serialized as '0' in v0
  projects: Project[];
  currentProjectId: string | null;
  currentChatId: string | null;
  createdAt: string;
  updatedAt: string;
};
```

### Project

```ts
type Project = {
  id: string;
  name: string;
  repoPath?: string | null;
  chats: Chat[];
  createdAt: string;
  updatedAt: string;
};
```

### Chat

```ts
type Chat = {
  id: string;
  projectId: string;
  title: string;
  kind: 'normal' | 'phase' | 'decision' | 'debug';
  messages: Message[];
  createdAt: string;
  updatedAt: string;
  memoryStatus: string;
  semanticSummaryStatus: string;
  semanticSummaryPreview: string | null;
  linkedRckTraceStatus: string;
  linkedRckTrace: LinkedRckTrace;
};
```

### Message

```ts
type Message = {
  id: string;
  role: 'user' | 'assistant' | 'system' | 'tool';
  content: string;
  createdAt: string;
  kind?: 'normal' | 'command' | 'command-result' | 'error' | 'placeholder';
  command?: string | null;
  safeMetadata?: Record<string, unknown> | null;
  links?: {
    rckTraceId?: string | null;
    contextPackId?: string | null;
    checkpointId?: string | null;
  } | null;
};
```

### linkedRckTrace

```ts
type LinkedRckTrace = {
  status: 'not-linked' | 'linked' | 'placeholder';
  traceId: string | null;
  provider: 'pi-rck-bridge';
  futureProvider: 'rck-core-kernel';
  mode: 'placeholder';
};
```

### Runtime expectation in 11A

In Fase 11A, only `status: 'not-linked'` is expected at runtime.

The `linked` and `placeholder` states are documented for future evolution only.

## Migration notes

- 11A only defines the boundary and schema.
- 11B will implement backend-local JSON read/write.
- 11C will hydrate and save the UI from that backend state.
- 11D will validate the persistence UX.
- The schema should remain stable enough that future migrations can be explicit and versioned.
- If the schema evolves, prefer a versioned migration path instead of implicit shape drift.

## Safety rules

- Product Data may contain user-authored chat text.
- Do not persist secrets intentionally.
- Do not store raw tool output.
- Do not store raw evidence.
- Do not store environment variables.
- Do not store tokens.
- Do not store credentials.
- Store only safe metadata links to future RCK/context/checkpoint artifacts.
- Do not repurpose Product Data as RCK storage.
- Do not write trace DAG records into the product state file.
- Keep the product surface product-shaped, not a technical dashboard.

## Phase split

### 11A — boundary/documentation only
- Define the Product Data boundary.
- Document the schema.
- Document storage choice and safety rules.
- No runtime persistence implementation.

### 11B — backend-local JSON implementation
- Add backend-local read/write for the future JSON file.
- Keep the path local to `apps/rufuschat-ui`.
- Do not expose RCK internals as storage.

### 11C — frontend hydrate/save
- Connect the UI to the persisted product state.
- Hydrate current project/chat from the backend-local state.
- Save user-visible product state changes.

### 11D — persistence UX validation
- Validate restore behavior.
- Validate project/chat switching.
- Validate that the UI still feels like RufusChat, not a technical RCK console.
- Validate that slash commands and governance flows still feel explicit and product-level.
