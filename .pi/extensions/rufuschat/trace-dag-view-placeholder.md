# Trace DAG view placeholder

## Purpose

RufusChat will eventually need a view for the RCK Trace DAG.
The Trace DAG belongs to RCK Core Kernel, not to the chat UI.
The UI should only consume a safe projection of the DAG through `RckProvider`.

This document is a placeholder for the conceptual shape of that view.
It does not define a real graph renderer or a persistence implementation.

## Conceptual DAG model

### Nodes

- State node
- Delta node
- Anchor node
- Context Pack node, as a derived artifact or linked artifact
- Evidence ref, as metadata or reference-only information rather than a raw node by default

### Edges

- state -> delta -> state
- state -> anchor
- state/trace -> context pack
- executor run -> evidence refs
- anchor -> context pack, when applicable

## Placeholder view

The first UI version for this area can stay deliberately small:

- Current Trace
- Linearized DAG summary
- Latest State
- Latest Anchor
- Latest Context Pack
- Node counts
- `DAG graph view coming later`

At this stage there is no graph rendering.
The placeholder is only meant to make the future shape explicit.

## Safe projection

The UI must not read raw DAG storage directly.
It should receive a safe DTO projection such as:

- `traceId`
- `nodes[]`
- `edges[]`
- `latestStateId`
- `latestAnchorId`
- `latestContextPackId`
- `counts`
- `generatedAt`

That projection may start empty or be derived from the current safe state view.

### TraceDagProjectionDto sketch

```ts
type TraceDagProjectionDto = {
  traceId: string;
  nodes: unknown[];
  edges: unknown[];
  latestStateId?: string | null;
  latestAnchorId?: string | null;
  latestContextPackId?: string | null;
  counts: {
    states: number;
    deltas: number;
    anchors: number;
    contextPacks: number;
    evidenceRefs: number;
  };
  generatedAt: string;
};
```

## Relationship with the current prototype

`scripts/rufuschat-ui-server.mjs` currently shows linear cards for latest state, latest anchor, and latest context.
That is a minimal linear view, not a DAG view.

9E prepares the transition from those cards toward an explicit DAG projection.

## Non-goals

- no RCK Core integration
- no graph library
- no real delta implementation
- no storage migration
- no evidence raw viewer
- no UI rewrite
