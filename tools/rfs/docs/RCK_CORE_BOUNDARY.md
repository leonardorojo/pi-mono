# RCK Core Boundary

## 1. What RCK Core is

`Rufus.RCK.Core` is the minimal cognitive git kernel.
It defines structural persistence primitives and stable identifiers, not workflows.

Core is the smallest durable layer that can represent a versioned cognitive graph.
It should stay focused on the shape of the graph, not on how higher layers create, interpret, render, or transport it.

## 2. What belongs in Core

Core may contain only the structural primitives required to represent the graph:

- `RckState`
- `RckDelta`
- `RckAnchor`
- `RckRef`
- `EvidenceRef`
- minimal patch operations / `PatchOp` support, if needed for structural deltas
- canonical payload serialization / hashing, if already part of the established model

If a concept is necessary to preserve identity, references, or structural compatibility across states and deltas, it may belong here.

## 3. What does NOT belong in Core

Core must not know about:

- Agenting
- Pi
- RPC
- JSONL
- CLI behavior
- workspace filesystem rules
- Git process orchestration
- ContextPack formatting / projection
- concrete workspace readers or writers
- `IntentInferenceAgent`
- `TraceSliceAgent`
- model/provider selection
- recording policy or persistence policy specifics
- `Rufus.Agenting`
- `Rufus.Cli`
- `Rufus.RCK.Workspace`

Core should not contain domain workflows, execution policies, transport concerns, or projection logic.

## 4. Wrapper vs payload

RCK uses a strict separation between structural wrappers and semantic payloads.

### Structural wrappers
- `rufus.rck.state` = RCK container for a persisted state
- `rufus.rck.delta` = RCK container for a persisted delta

### Semantic payloads
- `rufus.interaction-state`
- `rufus.agent-task-state`
- `rufus.interaction-delta`
- `rufus.agent-task-delta`
- and similar payload types

Core may store payloads as canonical opaque content.
Higher layers are responsible for interpreting the payload shape.

That means:
- the wrapper is structural and stable;
- the payload is semantic and may evolve;
- the wrapper should not be repurposed into a domain object.

## 5. Stability rule

Once RCK Core is stabilized, it must not be modified unless there is an explicit architectural decision.

Rule:

> Once stabilized, RCK Core is not changed unless an ADR or explicit architectural decision says the structural model is insufficient.

Implications:
- new features should first try to live in `Rufus.RCK.Workspace`, `Rufus.Cli`, `Rufus.Agenting`, or adapters;
- Core should only change if the universal structural model is missing something;
- no new domain concepts should be added to Core just because a single feature wants them.

## 6. Gate before touching Core

Before changing Core, answer all of these:

1. Is this a universal structural concept of the cognitive DAG?
2. Or is this a Workspace / CLI / Agenting policy?
3. Can it be represented as an opaque payload instead?
4. Can it live as a `Ref` or `EvidenceRef`?
5. Would it break existing states or deltas?
6. Does it require a migration plan?

If the answer to any of these is “this can live above Core”, keep it above Core.

## 7. Relationship with Agenting

`AgentTaskResult` is a semantic result object, not a Core primitive.
It is projected by higher layers and recorded through Workspace / CLI code.

Rules:
- Agenting does not persist RCK.
- RCK Core does not know `AgentTaskResult`.
- agent payloads are semantic and live inside payloads, not as new Core primitives.

## 8. Relationship with TraceSlice

`TraceSlice` v0 must not require touching Core.
If TraceSlice becomes persistent later, it should start as a payload, ref, or projection outside Core.

Do not add a new Core primitive just to store TraceSlice semantics.

## 9. Current boundary summary

Core owns the stable structural kernel.
Workspace owns persistence mechanics, file layout, readers/writers, and recording rules.
CLI owns command parsing and user-facing orchestration.
Agenting owns inference/execution behavior.

That separation should remain explicit and stable.
