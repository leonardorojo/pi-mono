# RCK ContextPack generation contract

This phase keeps the approved-scope → generation flow as a **placeholder / dev-only** contract.
It is still not a real RCK generation pipeline.

## What it is

The approved scope is converted into a request-shaped object that would later be sent to a real RCK generation layer.

In this phase, the request stays local and the response is still a placeholder. No real generation happens.

## What is in scope

- A placeholder generation request contract.
- A dev-safe endpoint: `POST /api/rck/context-pack/generate-placeholder`.
- A UI section that appears after the scope is approved.
- A placeholder response that can carry the existing ContextPack preview placeholder.
- A manual/dev-safe JSON load flow that can render a loaded preview without generating anything.
- Explicitly disabled injection semantics.

## What is not in scope

- No real RCK Core execution.
- No `.rck` reads.
- No TraceSlice generation.
- No real ContextPack generation.
- No automatic ContextPack generation from RufusChat.
- No real preview generation.
- No LLM calls.
- No semantic ranking, embeddings, or summary generation.
- No persistence of the request.
- No injection records.
- No chat context injection.
- No product-state schema changes.
- No `.data` writes.
- No real injection chain.

## Contract shape

### `RckApprovedContextScope`

Approved scope data that is allowed to seed a generation request.

Minimum shape:

- `suggestionId`
- `targetType`
- `targetId`
- `targetLabel`
- `depth`
- `includeAnchors`
- `includeEvidenceRefs`
- `includeDocs`
- `selectedArtifacts`

### `RckContextPackGenerationRequest`

Minimum shape:

- `requestId`
- `createdAtUtc`
- `source: "approved-scope"`
- `userIntentText`
- `approvedScope`
- `requestedOutput`
  - `contextPackSchemaVersion`
  - `previewOnly: true`
- `safety`
  - `requireUserApprovalForInjection: true`
  - `allowAutomaticInjection: false`

### `RckContextPackGenerationResponse`

Minimum shape:

- `requestId`
- `status`
  - `placeholder`
  - `not_connected`
  - `ready`
  - `failed`
- `contextPackReference` optional
- `contextPackPreview` optional
- `warnings`
- `constraints`
- `provenanceSummary`
- `message`

## Behavior

- Approved scope becomes a request contract first.
- The request is returned as a placeholder response.
- The response may reuse the existing ContextPack preview placeholder.
- A manually loaded ContextPack JSON preview can replace the placeholder preview in the UI.
- Confirm injection remains disabled.
- Nothing is persisted.
- Nothing is injected.
- No real RCK execution occurs.

## Related docs

- [`RCK_CONTEXTPACK_PREVIEW.md`](./RCK_CONTEXTPACK_PREVIEW.md)
- [`RCK_LOAD_CONTEXTPACK_JSON_PREVIEW.md`](./RCK_LOAD_CONTEXTPACK_JSON_PREVIEW.md)

## Next steps

1. Connect the request contract to exported `rck-core` ContextPack JSON.
2. Load a real ContextPack payload.
3. Validate the schema.
4. Render a real preview.
5. Add an explicit confirm-injection step only after the real chain exists.
