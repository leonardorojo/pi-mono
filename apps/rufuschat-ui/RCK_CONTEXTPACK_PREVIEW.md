# RCK ContextPack preview contract

This phase implements a **placeholder / dev-preview contract** for RufusChat.

## What is in scope

- A mock ContextPack preview shape for the RufusChat UI.
- A dev-safe endpoint: `GET /api/rck/context-pack/preview-placeholder`.
- A secondary UI panel labeled **Attach RCK Context**.
- The confirmation action is disabled and shows **Not available in this phase**.
- The preview can appear after the user approves the placeholder scope suggestion, but it remains a dev-only placeholder.

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

1. Add the real endpoint that loads a ContextPack.
2. Connect the preview to live data.
3. Wire confirm injection.
4. Add injection record persistence when the product phase allows it.
5. Optionally add Anchor recording later.
