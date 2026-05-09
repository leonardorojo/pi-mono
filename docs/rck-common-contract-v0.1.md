# RCK Common Contract v0.1 — Pi Bridge / RufusLab CLI Alignment

## Status

- Status: Draft
- Date: 2026-05-09
- Scope: Common conceptual/data contract between Pi RCK Bridge and RufusLab.RCK.Cli
- Intended consumers:
  - Pi RCK Bridge
  - RufusLab.RCK.Cli
  - future RCK adapters
  - future RufusChat / PI orchestration layer

## Non-goals

This contract does not attempt to:

- replace Pi RCK Bridge
- replace RufusLab.RCK.Cli
- mandate a single filesystem layout
- define a full event-sourcing model
- define Hermes execution semantics
- define Codex execution semantics
- define UI behavior
- define retention, cleanup, locking or distributed synchronization
- make .pi/rck/ and .rck/ identical today

---

## 1. Objective

This document defines a minimal shared RCK contract so that:

- Pi RCK Bridge can remain a runtime integration layer for Pi/RufusChat.
- RufusLab.RCK.Cli can remain the candidate formal RCK Core CLI.
- Both systems can converge around the same domain language.
- Future adapters can map between physical storage layouts without forcing an early migration.

The current situation is:

- Pi Bridge has strong runtime concepts:
  - /state
  - /rck inject
  - /rck anchor
  - /rck status
  - /hermes
  - runtime events
  - evidence refs
  - .pi/rck/ storage

- RufusLab.RCK.Cli has stronger core concepts:
  - trace
  - state
  - anchor
  - checkpoint
  - trace inject
  - .rck/ workspace

The goal of v0.1 is to align the common core:

- Trace
- TraceIndex
- State
- Anchor
- Checkpoint
- Evidence
- TraceInjectArtifact

Runtime events remain outside the core common contract for v0.1.

---

## 2. Roles

### RufusLab.RCK.Cli

RufusLab.RCK.Cli is the candidate formal RCK Core CLI.

Its current role is to provide a command-line interface for core RCK operations:

- create traces
- add states
- promote anchors
- create checkpoints
- list/show traces
- export condensed trace context

It is closer to the stable domain model.

### Pi RCK Bridge

Pi RCK Bridge is the runtime adapter inside Pi/RufusChat.

Its current role is to connect Pi commands to local RCK-like persistence and execution flows:

- create operational states
- inject safe context
- register anchors
- show status
- run Hermes fake/real-gated
- persist runtime evidence and events

It is closer to orchestration, runtime UX, and agent execution.

### RCK Common Contract

The RCK Common Contract defines the minimum shared domain model.

It should be stable enough that:

- Pi Bridge can project its runtime storage into it.
- RufusLab.RCK.Cli can expose or adopt it.
- future adapters can translate between .pi/rck/ and .rck/.

### Future adapters

Adapters are responsible for mapping:
- Pi runtime layout to common contract
- RufusLab CLI layout to common contract
- future UI/RufusChat runtime state to common contract

Adapters may preserve different physical layouts while sharing domain semantics.

---

## 3. Canonical entities

---

## 3.1 Trace

### Purpose

A trace is the root unit of causal operational memory.

It groups states, anchors, injected context and related metadata under a stable identity.

### Canonical fields

{
  "traceId": "string",
  "label": "string",
  "createdAtUtc": "string",
  "updatedAtUtc": "string",
  "headAnchorId": "string|null",
  "anchorCount": 0
}

### Producer

- RufusLab.RCK.Cli via trace start
- Pi Bridge via implicit or explicit trace creation
- future RufusChat orchestration layer

### Consumer

- trace list
- trace show
- trace inject
- /rck status
- future adapters
- future UI/RufusChat

### Persistence recommendation

Persist as trace index metadata.

Physical layout may differ:

- RufusLab: .rck/traces/<trace-id>/index.json
- Pi Bridge: .pi/rck/indexes/ today, future trace index adapter recommended

### Notes

The most important alignment change for Pi Bridge is to adopt an explicit traceId.

---

## 3.2 TraceIndex

### Purpose

A TraceIndex is the fast metadata view of a trace.

It is not the full trace history. It is a compact pointer to the current trace state.

### Canonical fields

{
  "schemaVersion": "rck.trace-index/v0.1",
  "traceId": "string",
  "label": "string",
  "createdAtUtc": "string",
  "updatedAtUtc": "string",
  "headAnchorId": "string|null",
  "anchorCount": 0
}

### Producer

- trace creation
- anchor promotion
- checkpoint creation
- future trace update operations

### Consumer

- list
- show
- status
- inject
- UI navigation

### Persistence recommendation

One index.json per trace.

### Notes

Pi Bridge may keep latest-* indexes as runtime cache, but the common contract should prefer a per-trace index.

---

## 3.3 State

### Purpose

A State is an immutable snapshot of operational content plus metadata.

A State belongs to exactly one Trace.

### Canonical fields

{
  "schemaVersion": "rck.state/v0.1",
  "id": "string",
  "traceId": "string",
  "payload": {},
  "meta": {
    "createdAtUtc": "string",
    "createdBy": "string",
    "labels": [],
    "evidence": []
  }
}

### Producer

- Pi Bridge /state
- RufusLab CLI state add
- RufusLab CLI checkpoint add
- future adapters

### Consumer

- anchor promotion
- trace inject
- trace show
- status
- future UI/RufusChat

### Persistence recommendation

Persist as one JSON file per state.

Physical layout may differ:

- RufusLab: .rck/storage/states/
- Pi Bridge: .pi/rck/states/

### Notes

The canonical state should include traceId.

If a legacy state does not have traceId, an adapter may infer it from surrounding trace metadata, but new producers should write it explicitly.

---

## 3.4 Anchor

### Purpose

An Anchor is a significant checkpoint that promotes a State within a Trace.

It gives the trace a stable semantic point of reference.

### Canonical fields

{
  "schemaVersion": "rck.anchor/v0.1",
  "id": "string",
  "traceId": "string",
  "stateId": "string",
  "parentAnchorIds": [],
  "meta": {
    "label": "string",
    "createdAtUtc": "string",
    "valueScore": null
  }
}

### Producer

- Pi Bridge /rck anchor
- RufusLab CLI anchor promote
- RufusLab CLI checkpoint add

### Consumer

- trace traversal
- trace inject
- trace show
- /rck status
- future UI/RufusChat

### Persistence recommendation

Persist as one JSON file per anchor.

Physical layout may differ:

- RufusLab: .rck/storage/anchors/
- Pi Bridge: .pi/rck/anchors/

### Notes

The common semantic is anchor promotes state.

Pi Bridge may keep the user-facing command /rck anchor, but internally it should map to the same domain operation as anchor promote.

---

## 3.5 Checkpoint

### Purpose

A Checkpoint is an operation that creates a State and immediately promotes it to an Anchor.

It is not a separate persisted domain entity in v0.1.

### Canonical fields

As an operation input:
{
  "traceId": "string",
  "title": "string",
  "kind": "string",
  "summary": "string",
  "createdAtUtc": "string",
  "labels": []
}

### Producer

- RufusLab CLI checkpoint add
- future Pi Bridge convenience command
- future UI/RufusChat workflow

### Consumer

- CLI
- bridge
- future adapters

### Persistence recommendation

Do not persist checkpoint as a standalone object in v0.1.

Persist the resulting:

- State
- Anchor
- TraceIndex update

### Notes

Checkpoint is an operational shortcut.

---

## 3.6 Evidence

### Purpose

Evidence records external proof, outputs or references used to support a State.

Examples:

- command stdout/stderr references
- file hashes
- diagnostic results
- links
- tool artifacts
- Hermes execution outputs

### Canonical fields

{
  "evidenceHash": "string",
  "evidenceRootUri": "string",
  "observedAtUtc": "string",
  "providerId": "string",
  "manifestVersion": "string",
  "notes": "string"
}

### Producer

- Pi Bridge Hermes execution
- future Codex integration
- evidence collectors
- manual capture tools

### Consumer

- state metadata
- audit tools
- trace inject
- future UI/RufusChat
- future RunSupervision

### Persistence recommendation

Canonical reference lives in:

"state.meta.evidence": []

A filesystem evidence folder may exist as a backing store.

Examples:

- Pi Bridge backing store: .pi/rck/evidence/
- future RufusLab backing store: implementation-defined

### Notes

For v0.1, evidence should be referenced from State.Meta.

Raw evidence should not be injected into LLM context by default.

---

## 3.7 TraceInjectArtifact

### Purpose

A TraceInjectArtifact is a condensed, safe representation of a trace for agent/LLM consumption.

It aligns Pi Bridge “context pack” with RufusLab trace inject.

### Canonical fields

{
  "schemaVersion": "rck.trace-inject/v0.1",
  "trace": {
    "traceId": "string",
    "label": "string",
    "headAnchorId": "string|null"
  },
  "anchors": [],
  "states": [],
  "promptingNotes": []
}

### Producer

- Pi Bridge /rck inject
- RufusLab CLI trace inject
- future adapters

### Consumer

- LLM context injection
- RufusChat
- PI orchestration layer
- future agents

### Persistence recommendation

The canonical schema is independent of physical representation.

Allowed physical forms:

- Markdown with YAML fenced block
- JSON
- future structured transport object

### Notes

In v0.1, “context pack” is treated as a usage name.

The canonical domain concept is trace-inject.

---

## 3.8 Event extension

### Purpose

Events capture append-only runtime operations.

Examples:

- HermesRunRequested
- HermesRunRecorded
- StatePackCreated
- ContextPackInjected
- AnchorRegistered

### Canonical fields

{
  "eventId": "string",
  "traceId": "string",
  "type": "string",
  "atUtc": "string",
  "payload": {}
}

### Producer

- Pi Bridge
- future runtime integrations

### Consumer

- audit
- replay
- debugging
- future supervision

### Persistence recommendation

Events are explicitly outside RCK Common Contract v0.1 core.

They may remain as runtime extension storage.

### Notes

Events are important for Pi/RufusChat orchestration, but RufusLab.RCK.Cli does not currently model them as a core primitive.

Events may be promoted to common contract v0.2 if replay/audit becomes cross-tool.

---

# 4. JSON shapes

## 4.1 TraceIndex

{
  "schemaVersion": "rck.trace-index/v0.1",
  "traceId": "trace-main",
  "label": "Main working trace",
  "createdAtUtc": "2026-05-09T10:00:00.000Z",
  "updatedAtUtc": "2026-05-09T10:15:00.000Z",
  "headAnchorId": "anchor_abc123",
  "anchorCount": 3
}

---

## 4.2 State
{
  "schemaVersion": "rck.state/v0.1",
  "id": "state_abc123",
  "traceId": "trace-main",
  "payload": {
    "title": "Current bridge status",
    "kind": "operational-summary",
    "summary": "RCK Bridge supports state, inject, anchor, status and Hermes gated execution."
  },
  "meta": {
    "createdAtUtc": "2026-05-09T10:10:00.000Z",
    "createdBy": "pi-rck-bridge",
    "labels": ["bridge", "status"],
    "evidence": [
      {
        "evidenceHash": "sha256:example",
        "evidenceRootUri": ".pi/rck/evidence/hermes/stdout/example.log",
        "observedAtUtc": "2026-05-09T10:10:10.000Z",
        "providerId": "hermes",
        "manifestVersion": "rck.evidence/v0.1",
        "notes": "Hermes stdout evidence reference"
      }
    ]
  }
}

---

## 4.3 Anchor

{
  "schemaVersion": "rck.anchor/v0.1",
  "id": "anchor_abc123",
  "traceId": "trace-main",
  "stateId": "state_abc123",
  "parentAnchorIds": ["anchor_previous"],
  "meta": {
    "label": "bridge-status-baseline",
    "createdAtUtc": "2026-05-09T10:11:00.000Z",
    "valueScore": null
  }
}

---

## 4.4 Evidence

{
  "evidenceHash": "sha256:example",
  "evidenceRootUri": ".pi/rck/evidence/hermes/stdout/example.log",
  "observedAtUtc": "2026-05-09T10:10:10.000Z",
  "providerId": "hermes",
  "manifestVersion": "rck.evidence/v0.1",
  "notes": "Hermes stdout evidence reference"
}

---

## 4.5 TraceInjectArtifact

{
  "schemaVersion": "rck.trace-inject/v0.1",
  "trace": {
    "traceId": "trace-main",
    "label": "Main working trace",
    "headAnchorId": "anchor_abc123"
  },
  "anchors": [
    {
      "anchorId": "anchor_abc123",
      "stateId": "state_abc123",
      "label": "bridge-status-baseline",
      "createdAtUtc": "2026-05-09T10:11:00.000Z"
    }
  ],
  "states": [
    {
      "stateId": "state_abc123",
      "title": "Current bridge status",
      "kind": "operational-summary",
      "summary": "RCK Bridge supports state, inject, anchor, status and Hermes gated execution."
    }
  ],
  "promptingNotes": [
    "Use this trace as operational context.",
    "Do not expose raw evidence unless explicitly requested."
  ]
}

---

# 5. Mapping Pi Bridge to Common Contract

## 5.1 /state

Current Pi Bridge behavior:

- creates state-like artifact
- writes .pi/rck/states/
- writes event
- updates latest-state index

Common mapping:

- maps to state inside a trace
- must include explicit traceId
- may keep runtime event outside common contract

Action required:

- introduce explicit traceId
- align state JSON with rck.state/v0.1
- keep .pi/rck/events/ as Pi runtime extension

---

## 5.2 /rck anchor

Current Pi Bridge behavior:

- creates anchor artifact
- may reference latest state
- writes event
- updates latest-anchor index

Common mapping:

- maps to anchor promote
- anchor must reference:
  - traceId
  - stateId
  - parentAnchorIds

Action required:

- ensure anchor includes explicit traceId
- treat /rck anchor as user-facing alias for anchor promotion

---

## 5.3 /rck inject

Current Pi Bridge behavior:

- creates context pack
- writes .pi/rck/context-packs/
- writes event
- updates latest-context-pack index
- may emit safe custom message

Common mapping:

- maps to trace-inject
- “context pack” is runtime/user-facing name
- canonical schema should be rck.trace-inject/v0.1

Action required:

- define or adapt context pack shape to trace-inject shape
- preserve safe LLM injection behavior

---

## 5.4 /rck status

Current Pi Bridge behavior:

- read-only status over .pi/rck/
- displays latest state/context/anchor/Hermes metadata
- does not create RCK event

Common mapping:

- maps to read-only trace/status view
- may be implemented using:
  - trace-index
  - latest runtime indexes
  - event extension metadata

Action required:

- no core change required
- may later map to trace show or trace list

---

## 5.5 /hermes events and evidence

Current Pi Bridge behavior:
- persists runtime events:
  - HermesRunRequested
  - HermesRunRecorded
- persists stdout/stderr evidence files
- references evidence from event payload
- does not expose raw stdout/stderr in visible message

Common mapping:

- events remain runtime extension in v0.1
- evidence should be projectable into State.Meta.Evidence
- raw evidence remains backing store only

Action required:

- keep events outside core common contract
- define adapter rule from Hermes evidence refs to State.Meta.Evidence when needed

---

# 6. Mapping RufusLab.RCK.Cli to Common Contract

## 6.1 trace start

Current behavior:

- creates trace index
- initializes trace metadata

Common mapping:

- directly maps to Trace / TraceIndex

Action required:

- expose schema as rck.trace-index/v0.1
- ensure stable field naming

---

## 6.2 trace list / trace show

Current behavior:

- lists or displays trace metadata and anchored chain

Common mapping:

- maps to TraceIndex and trace traversal

Action required:

- no major change
- optionally expose structured output in future

---

## 6.3 state add

Current behavior:

- creates state in a trace
- persists state JSON

Common mapping:

- maps to State

Action required:

- ensure state includes explicit traceId
- align metadata names:
  - createdAtUtc
  - createdBy
  - labels
  - evidence

---

## 6.4 anchor promote

Current behavior:

- promotes state to anchor
- updates trace head

Common mapping:

- maps directly to Anchor

Action required:

- ensure anchor includes explicit traceId
- align parentAnchors with parentAnchorIds

---

## 6.5 checkpoint add

Current behavior:

- creates state
- promotes it to anchor

Common mapping:

- maps to Checkpoint operation

Action required:

- document checkpoint as operation
- do not persist checkpoint as independent entity in v0.1

---

## 6.6 trace inject

Current behavior:

- exports condensed trace context in Markdown/YAML
- uses schema similar to trace-condensed/v0.1

Common mapping:

- maps to TraceInjectArtifact

Action required:

- align schema naming:
  - rck.trace-inject/v0.1
- preserve Markdown/YAML as allowed physical representation

---

# 7. Decisions v0.1

## Decision 1 — Explicit traceId is required

All States and Anchors should include explicit traceId.

Reason:

- improves interoperability
- avoids path-dependent inference
- allows adapters to work across layouts

---

## Decision 2 — State belongs to Trace

A State is not standalone in the common contract.

It belongs to one Trace.

---

## Decision 3 — Anchor promotes State

Anchor is the semantic promotion of a State.

Pi may expose /rck anchor, but the domain operation is anchor promote.

---

## Decision 4 — Checkpoint is not persisted as entity

Checkpoint is a convenience operation:

checkpoint = state add + anchor promote

---

## Decision 5 — Evidence is canonical in State.Meta.Evidence

Evidence files may exist in a filesystem backing store.

The common contract references evidence from State.Meta.

---

## Decision 6 — TraceInjectArtifact replaces ContextPack conceptually

“Context pack” remains a useful runtime/UI term.

The common domain concept is TraceInjectArtifact.

---

## Decision 7 — Events are outside core v0.1

Events remain important, but they are a runtime extension in v0.1.

They may become common contract v0.2 after replay/audit requirements stabilize.

---

# 8. Storage compatibility

The common contract does not require a single physical layout.

Allowed layouts:

## Pi Bridge current layout

.pi/rck/
  events/
  states/
  context-packs/
  anchors/
  evidence/
  indexes/

## RufusLab current layout

.rck/
  storage/
    states/
    anchors/
  traces/
    <trace-id>/
      index.json

Adapters may map both layouts to the same common entities.

Important principle:

contract != path

Paths may differ as long as entities satisfy the common contract.

---

# 9. Open questions

## Events in v0.2

Should runtime events become part of the common contract?

Possible future entities:

- EventLog
- EventEnvelope
- RuntimeEvent
- ExecutionEvent

---

## Evidence manifest
Does evidence need a standalone manifest?

Current v0.1 position:

- canonical evidence reference lives in State.Meta.Evidence
- backing store may exist separately

Open question:

- should large evidence collections have manifest files?

---

## Context pack JSON vs Markdown/YAML trace inject

Should Pi Bridge keep JSON context-packs?

Current v0.1 position:

- TraceInjectArtifact is canonical
- physical representation can be JSON or Markdown/YAML

Open question:

- should both formats be generated from the same logical artifact?

---

## /rck status mapping

Should /rck status map to trace show or remain runtime-specific?

Current v0.1 position:

- status remains runtime-specific
- common mapping may use trace-index and latest runtime cache

---

## Pi latest indexes

Should Pi keep:

.pi/rck/indexes/latest-state.json
.pi/rck/indexes/latest-context-pack.json
.pi/rck/indexes/latest-anchor.json

Current v0.1 position:

- yes, as runtime cache
- not canonical core storage

---

# 10. Roadmap

## 5E.1 — Common spec

Create this document as:

docs/rck-common-contract-v0.1.md

No code changes.

---

## 5E.2 — Pi Bridge explicit traceId

Introduce explicit traceId into Pi Bridge artifacts.

Expected changes:

- State includes traceId
- Anchor includes traceId
- TraceInjectArtifact includes traceId
- latest indexes include traceId consistently
- events continue as runtime extension

---

## 5E.3 — RufusLab schema alignment

Align RufusLab.RCK.Cli with the common contract where needed.

Expected changes:

- expose schema names
- ensure traceId appears explicitly in state/anchor
- align field names where useful
- keep physical layout if it remains compatible

---

## 5E.4 — Adapter / interop test

Create an interop test or smoke.

Possible test cases:

- state created by Pi Bridge can be projected to common State
- anchor promoted by RufusLab can be read as common Anchor
- trace inject from RufusLab can be consumed as TraceInjectArtifact
- Pi context pack can be projected as TraceInjectArtifact

---

# 11. Final recommendation

Use a common adapter contract before direct integration.

Do not make Pi Bridge call RufusLab.RCK.Cli yet.

Do not force RufusLab.RCK.Cli to adopt Pi storage layout.

The recommended architecture is:

Pi RCK Bridge
  -> runtime adapter
  -> Pi/RufusChat commands
  -> Hermes evidence/events

RufusLab.RCK.Cli
  -> RCK Core CLI
  -> trace/state/anchor/checkpoint/inject

RCK Common Contract v0.1
  -> shared domain schema
  -> adapter boundary
  -> future interop

This preserves the validated Pi runtime work while allowing RufusLab to become the formal RCK core.
