# RCK Semantic Projection v0

## What it is

RCK Semantic Projection is a **derived, reconstructible layer** that reads existing RCK anchors
and builds a semantic projection — a lightweight, structured view of the anchor timeline —
without modifying RCK Core.

It lives in `tools/rfs/src/Rufus.RCK.Semantic/`.

## What it is NOT

- **Not RCK Core.** RCK Core (states, deltas, anchors, DAG, hashes, persistence) remains the single structural source of truth.
- **Not a replacement for TraceSlice.** TraceSlice operates on validated structural cuts from RCK Core.
- **Not a replacement for ContextPack.** ContextPack is the full DAG export for agents.
- **Not a replacement for ConversationalMemory.** ConversationalMemory handles agent-facing memory assembly.
- **Not yet integrated with Complete mode.** No automatic rebuilds, no retrieval.

## Source of truth

**RCK Core is the only source of truth.** The semantic projection is always reconstructible
from `.rfs/rck` anchors. If the projection is deleted, corrupted, or stale, `rfs semantic rebuild`
regenerates it deterministically.

## Persistence

```
.rfs/semantic/projection.json
```

- Write target: only this file.
- Never writes to `.rfs/rck`.
- Schema version `1`.

## Inputs

Reads from `.rfs/rck` via `RckWorkspaceContextPackReader`:

- `RckAnchor.Id`
- `RckAnchor.Meta.Label` (anchor name)
- `RckAnchor.StateId`
- `RckAnchor.Meta.CreatedAtUtc`

## Outputs

| Output | Description |
|--------|-------------|
| `RckSemanticNode` | One per anchor. Id derived from AnchorId (SHA-256, first 16 hex chars). Stable across rebuilds. |
| `RckSemanticDelta` | One per consecutive anchor pair. Id derived from FromAnchorId + ToAnchorId. |
| `projection.json` | JSON file under `.rfs/semantic/`. |

## v0 Guarantees

- Projection is **reconstructible** — same anchors always produce the same nodes and deltas.
- Node and Delta **IDs are stable** (deterministic SHA-256 derivation).
- One SemanticNode per Anchor.
- One SemanticDelta between each consecutive anchor pair (ordered by `CreatedAtUtc`).
- **No writes to `.rfs/rck`.**
- **No LLM calls.**
- **No embeddings.**
- **No Pi / DeepSeek dependency.**

## v0 Limitations

- `SourceDeltaIds` is empty — no reliable helper yet to trace the exact delta span between anchors.
- `Summary` = `Anchor.Label` (no LLM summarization).
- `Topics` = simple whitespace-split tokenization from `Anchor.Label` (lowercased, punctuation stripped).
- No full traversal of the RCK DAG between anchors.
- No automatic rebuild on `/anchor` creation.
- Not integrated with Complete or TraceSlice pipelines.

## v0 Commands

```
rfs semantic rebuild   Build/rebuild from .rfs/rck anchors
rfs semantic show      Display current projection
```

## Future (not implemented)

- Automatic rebuild triggered by `/anchor`.
- Optional LLM-backed semantic enrichment (summary, topics, embeddings).
- Pre-index for intent-conditioned minimal retrieval.
- Assist TraceSlice by providing a compact semantic timeline, without replacing the validator or RCK Core.
