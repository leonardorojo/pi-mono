# RCK write-back adapter contract

This phase introduces the first formal adapter layer for RufusChat → RCK write-back.

It is contract-only and stubbed. It does not write to real RCK Core, does not read `.rck`, does not touch `.data`, does not mutate the RCK DAG, and does not register Anchors.

## Files introduced in this phase

- `rck-writeback-contract.mjs`
- `rck-writeback-provider.mjs`

## What the contract defines

The contract normalizes the shape of the future write-back payloads and supporting references:

- `RckWritebackChatTurnStatePayload`
- `RckWritebackChatTurnDeltaPayload`
- `RckWritebackToolExecution`
- `RckWritebackArtifactRef`
- `RckWritebackEvidenceRef`
- `RckWritebackContextUsed`
- `RckWritebackDecision`
- `RckWritebackOpenQuestion`
- `RckWritebackRegistrationDraft`
- `RckWritebackRegistrationResult`

## What the provider does

The provider is a dev-safe stub that can:

- build a normalized chat turn State payload
- build a normalized chat turn Delta payload
- build a registration draft
- return a placeholder registration result

It does not register anything in RCK Core.

## Normalized defaults

If the caller does not provide real evidence, the contract keeps the arrays empty by default:

- `toolExecutions: []`
- `artifacts: []`
- `evidenceRefs: []`
- `decisions: []`
- `openQuestions: []`

That keeps the phase honest: no invented evidence and no invented artifacts.

## Placeholder result shape

The provider returns a placeholder result with:

- `ok: true`
- `status: placeholder`
- `message: RCK write-back is not connected in this phase.`
- `statePayload`
- `deltaPayload`
- `stateId: null`
- `deltaId: null`

This phase adds the first contract/stub modules: `rck-writeback-contract.mjs` and `rck-writeback-provider.mjs`.
They normalize payload shapes and return placeholder registration results only.

## Boundaries preserved

This phase still avoids:

- real RCK execution
- `.rck` reads
- `.data` writes
- product-state schema changes
- Anchor recording
- extra LLM calls
- TraceSlice generation
- ContextPack generation

## Next step

A later phase can connect this adapter contract to a real RCK write-back provider after the chat-session evidence model is settled.
