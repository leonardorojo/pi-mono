# RCK ContextPack preview contract

This phase implements a **placeholder / dev-preview contract** for RufusChat.

## What is in scope

- A mock ContextPack preview shape for the RufusChat UI.
- A dev-safe endpoint: `GET /api/rck/context-pack/preview-placeholder`.
- A secondary UI panel labeled **Attach RCK Context**.
- The confirmation action is disabled and shows **Not available in this phase**.
- The preview can appear after the user approves the placeholder scope suggestion and the approved-scope → ContextPack generation request contract is built.
- It still remains a dev-only placeholder and is not a real generated ContextPack.

## What is not in scope

- No real RCK Core execution.
- No direct `.rck` reads.
- No StateStore, DeltaStore, or AnchorStore integration.
- No TraceSlice generation.
- No real ContextPack generation.
- No LLM generation.
- No embeddings, ranking, or memory automation.
- No persisted injection records.
- No real injection chain.

## Contract source

The contract is derived from:

- `docs/RUFUSCHAT_ADAPTER_DESIGN.md`
- `docs/CONTEXT_PACK_BOUNDARY.md`
- `schemas/rck.context_pack.v0.schema.json`
- published RCK Core commit: `048d4c3`
- `RCK_CONTEXTPACK_GENERATION_CONTRACT.md`

## Preview shape

The placeholder preview exposes:

- `contextPackId` / `contextPackHash`
- `title`
- `sourceTraceSliceHashes`
- `sectionsVisible`
- `estimatedTokenCost` or `null`
- `warnings`
- `constraints`
- `provenanceSummary`
- `exactTextToInject`
- `userApprovalStatus`
- `injectionPolicy`
- `injectionRecordDraft`

## Next steps

1. Connect the approved-scope request contract to exported `rck-core` ContextPack JSON.
2. Load a real ContextPack payload.
3. Validate the schema.
4. Render a real preview.
5. Add an explicit confirm-injection step only after the real chain exists.
