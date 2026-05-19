# RCK trace / branch contract shapes

This document closes the contract-shape layer for the future runtime initialization of:

- Project → Trace
- Chat → Branch

Phase 15D now implements the placeholder runtime for these shapes in RufusChat UI. The document remains the source contract, but the runtime now persists the placeholder birth fields described below.

It does not connect to real RCK Core, does not create real anchors, and does not mutate any RCK DAG.

## Conceptual model

```text
Project = Trace
Chat = Branch
First Chat in Project = Main Branch
Additional Chat in Project = Cognitive Branch
Chat Turn = State + Delta within the current Branch
```

## Boundary summary

- Project birth creates a Trace birth contract.
- First Chat birth creates the Main Branch contract for that Trace.
- Additional Chat birth creates a Cognitive Branch contract inside the existing Trace.
- Constructors stay pure.
- Hydration/import never creates births.
- Fallback may repair missing runtime selection, but must not be treated as a fresh birth unless an explicit birth path is invoked.
- `linkedRckTrace` remains a chat-scoped placeholder bridge and is not redefined as the trace birth record.

## Lifecycle states

Use the same high-level lifecycle vocabulary for Trace and Branch contracts.

Suggested states:

- `not_initialized` — no birth contract has been materialized yet
- `placeholder` — a safe placeholder contract exists, but no adapter/service registration has happened
- `ready_for_adapter` — the contract shape is complete enough to hand to a future adapter/service
- `adapter_unavailable` — the adapter/service could not be reached or loaded
- `adapter_error` — the adapter/service failed while processing a valid contract
- `registered` — the future success state when the adapter/service registers the birth

Recommended status rule:
- `not_initialized` is the default before any explicit birth path runs.
- `placeholder` is used when the UI has materialized the birth intent in safe local form.
- `ready_for_adapter` is used when the payload is complete and eligible for adapter handoff.
- `adapter_unavailable` / `adapter_error` are transport/execution states, not birth meanings.
- `registered` is reserved for future real runtime integration.

## Source tags

Birth contracts should carry a source tag so later phases can distinguish explicit UI creation from seed/reset/import paths.

Allowed source tags:

- `ui-project-create`
- `ui-chat-create`
- `seed`
- `reset`
- `fallback`
- `import`

Suggested rule:
- source tags describe why the birth contract was materialized.
- source tags are not user-facing labels.
- source tags should be stable and machine-readable.

## Runtime-only draft shapes

These shapes are conceptual and should stay runtime-only until the placeholder runtime phase.

### TraceInitializationDraft

```ts
type TraceInitializationDraft = {
  projectId: string;
  projectName: string;
  source: 'ui-project-create' | 'seed' | 'reset' | 'fallback' | 'import';
  traceInitializationStatus: 'not_initialized' | 'placeholder' | 'ready_for_adapter' | 'adapter_unavailable' | 'adapter_error' | 'registered';
  initialTraceStatePayload: InitialTraceStatePayload;
  initialTraceAnchorDraft: InitialTraceAnchorDraft;
};
```

### InitialTraceStatePayload

```ts
type InitialTraceStatePayload = {
  kind: 'rck.trace_initialization_state';
  schemaVersion: 'rck.trace_initialization_state.v0';
  projectId: string;
  traceId: string | null;
  traceBirthKind: 'project' | 'seed' | 'reset' | 'fallback' | 'import';
  source: 'ui-project-create' | 'seed' | 'reset' | 'fallback' | 'import';
  initialBranchKind: 'main';
  initialBranchId: string | null;
  createdAt: string;
};
```

### InitialTraceAnchorDraft

```ts
type InitialTraceAnchorDraft = {
  anchorKind: 'trace_birth';
  projectId: string;
  traceId: string | null;
  anchorId: string | null;
  source: 'ui-project-create' | 'seed' | 'reset' | 'fallback' | 'import';
};
```

### BranchInitializationDraft

```ts
type BranchInitializationDraft = {
  projectId: string;
  chatId: string;
  branchKind: 'main' | 'branch';
  parentBranchId: string | null;
  source: 'ui-project-create' | 'ui-chat-create' | 'seed' | 'reset' | 'fallback' | 'import';
  branchInitializationStatus: 'not_initialized' | 'placeholder' | 'ready_for_adapter' | 'adapter_unavailable' | 'adapter_error' | 'registered';
  branchStartStatePayload: BranchStartStatePayload;
  branchReferenceAnchorDraft: BranchReferenceAnchorDraft;
};
```

### BranchStartStatePayload

```ts
type BranchStartStatePayload = {
  kind: 'rck.branch_initialization_state';
  schemaVersion: 'rck.branch_initialization_state.v0';
  projectId: string;
  chatId: string;
  traceId: string | null;
  branchId: string | null;
  branchKind: 'main' | 'branch';
  parentBranchId: string | null;
  source: 'ui-project-create' | 'ui-chat-create' | 'seed' | 'reset' | 'fallback' | 'import';
  createdAt: string;
};
```

### BranchReferenceAnchorDraft

```ts
type BranchReferenceAnchorDraft = {
  anchorKind: 'branch_birth';
  projectId: string;
  chatId: string;
  traceId: string | null;
  branchId: string | null;
  anchorId: string | null;
  source: 'ui-project-create' | 'ui-chat-create' | 'seed' | 'reset' | 'fallback' | 'import';
};
```

## Future persisted shape: Project

The future persisted Project shape should be able to carry Trace birth state without overloading `linkedRckTrace`.

```ts
type Project = {
  id: string;
  name: string;
  repoPath?: string | null;
  chats: Chat[];
  createdAt: string;
  updatedAt: string;

  traceId?: string | null;
  traceInitializationStatus?: 'not_initialized' | 'placeholder' | 'ready_for_adapter' | 'adapter_unavailable' | 'adapter_error' | 'registered';
  initialTraceAnchorId?: string | null;
  traceInitializationSource?: 'ui-project-create' | 'seed' | 'reset' | 'fallback' | 'import';
  traceBirthKind?: 'project' | 'seed' | 'reset' | 'fallback' | 'import';
};
```

Notes:
- `traceId` is the stable placeholder for the future Trace identity.
- `traceInitializationStatus` records the birth lifecycle.
- `initialTraceAnchorId` is the structural birth reference.
- `traceInitializationSource` captures how the birth entered the system.
- `traceBirthKind` captures the semantic reason the Trace exists.

## Future persisted shape: Chat

The future persisted Chat shape should carry Branch birth state separately from `linkedRckTrace`.

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

  branchId?: string | null;
  branchKind?: 'main' | 'branch';
  parentBranchId?: string | null;
  branchInitializationStatus?: 'not_initialized' | 'placeholder' | 'ready_for_adapter' | 'adapter_unavailable' | 'adapter_error' | 'registered';
  branchReferenceAnchorId?: string | null;
  branchInitializationSource?: 'ui-chat-create' | 'seed' | 'reset' | 'fallback' | 'import';
};
```

Notes:
- the first chat in a project should be the `main` branch.
- additional chats in the same project should be `branch`.
- `parentBranchId` remains `null` for the main branch unless a future branch-convergence model changes that.
- `branchInitializationSource` must distinguish explicit chat creation from seed/reset/import/fallback repair.

## Relationship to write-back by chat turn

The future closed-turn write-back contract should conceptually carry the current Trace and Branch identities.

Minimum conceptual relationship:

- `ChatTurnStatePayload` points to `projectId`, `traceId`, `chatId`, and `branchId`.
- `ChatTurnDeltaPayload` describes the append transition inside the current Branch.
- `fromTurnId` / `toTurnId` stay branch-local.
- the write-back contract should not re-derive Trace birth or Branch birth; it should consume the already-initialized identities.

This keeps birth contracts separate from turn-growth contracts.

## Relationship to linkedRckTrace

`linkedRckTrace` remains a chat-level bridge placeholder.

It should continue to mean:
- the chat may later be linked to a real trace bridge
- the placeholder currently says the chat is not linked
- it is not the same thing as Trace birth
- it is not the same thing as Branch birth

In this phase:
- do not rename `linkedRckTrace`
- do not collapse it into `traceId`
- do not use it as the single source of truth for Project or Chat birth

## Relationship to User Anchor and Merge Anchor

### User Anchor

- out of scope for this phase
- semantically explicit and user-driven
- not a structural birth artifact
- should be modeled separately from Trace/Branch anchors

### Merge Anchor

- out of scope for this phase
- depends on a future branch-convergence model
- should not be introduced until branch merge semantics exist

## Persisted vs runtime-only

Persisted later:
- `traceId`
- `traceInitializationStatus`
- `initialTraceAnchorId`
- `traceInitializationSource`
- `traceBirthKind`
- `branchId`
- `branchKind`
- `parentBranchId`
- `branchInitializationStatus`
- `branchReferenceAnchorId`
- `branchInitializationSource`

Runtime-only now:
- `TraceInitializationDraft`
- `InitialTraceStatePayload`
- `InitialTraceAnchorDraft`
- `BranchInitializationDraft`
- `BranchStartStatePayload`
- `BranchReferenceAnchorDraft`

## Implementation guardrails for the next phase

- Constructors stay pure.
- Hydration/import do not create births.
- Seed/reset/fallback must be routed through explicit birth orchestration, not ad hoc constructor side effects.
- `createProject(...)` and `createChat(...)` remain constructors, not lifecycle engines.
- `linkedRckTrace` stays a bridge placeholder.
- No Anchor UI yet.
- No Manual Delta revival.

## Phase outcome

This contract layer is now ready for the placeholder runtime step:

- Fase 15D — Trace/Branch initialization placeholder runtime

If a later review finds the persisted schema still needs more fields, that later step should revise the contract doc before wiring runtime behavior.
