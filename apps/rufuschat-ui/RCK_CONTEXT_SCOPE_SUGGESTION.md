# RCK Context Scope Suggestion contract

This phase adds a **placeholder / dev-only** scope suggestion layer for RufusChat.

## What it is

A Context Scope Suggestion is a local UI hint that proposes which RCK boundary the user likely wants to inspect before any real TraceSlice or ContextPack work happens.

It is **not** a real RCK selection engine.

## What is in scope

- A mock suggestion shape for the RufusChat UI.
- A dev-safe endpoint: `POST /api/rck/context-scope/suggest-placeholder`.
- A visible **Suggested RCK scope** section in the Attach RCK Context flow.
- Local-only user actions:
  - Approve scope
  - Reject
  - Adjust manually
- A placeholder relation to the existing ContextPack preview placeholder.
- When the user approves the scope, the approved scope becomes a local ContextPack generation request contract.

## What is not in scope

- No LLM call.
- No real RCK Core execution.
- No direct `.rck` reads.
- No TraceSlice generation.
- No real ContextPack generation.
- No context injection.
- No persistence of suggestions.
- No persistence of injection records.
- No product state schema changes.
- No `.data` writes for suggestions.
- No embeddings, ranking, or semantic summary generation.

## Contract shape

The suggestion placeholder exposes:

- `suggestionId`
- `status`:
  - `placeholder`
  - `suggested`
  - `approved`
  - `rejected`
  - `adjusted`
- `userIntentText`
- `suggestedTarget`
  - `targetType`
  - `targetId`
  - `label`
- `suggestedDepth`
- `includeAnchors`
- `includeEvidenceRefs`
- `includeDocs`
- `selectedArtifacts`
- `candidateArtifacts`
- `excludedArtifacts`
- `rationale`
- `confidence`
- `warnings`
- `preview`
- `userDecision`

## Behavior

- The suggestion is mock data only.
- The server returns stable placeholder output from the request intent.
- The UI stores the suggestion only in memory.
- Approving the scope only changes local UI state to `approved`.
- Approving the scope also triggers the placeholder approved-scope → ContextPack generation request contract in memory.
- Approving the scope does **not**:
  - trigger real TraceSlice generation
  - trigger real ContextPack generation
  - enable confirm injection
  - inject context into the real chat runtime
- The existing ContextPack preview placeholder may be shown after approval, and it is derived from the local request contract in this phase.

## Next steps

1. Connect the placeholder to a real scope selector.
2. Generate a real TraceSlice from an approved scope.
3. Build a real ContextPack from that TraceSlice.
4. Show a real preview of the generated chain.
5. Enable confirm injection only after the real chain exists.
6. Preserve the separate write-back direction and runtime boundary for closed chat turns in [`RCK_WRITEBACK_EXECUTION_BOUNDARY.md`](./RCK_WRITEBACK_EXECUTION_BOUNDARY.md).
