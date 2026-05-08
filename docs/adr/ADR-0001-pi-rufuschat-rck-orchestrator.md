---
id: ADR-0001
title: PI/RufusChat as Local Cognitive Orchestrator and RCK as Operational Truth Layer
status: Proposed
date: 2026-05-08
scope: PI/RufusChat, RCK, Hermes, Codex CLI, CHC
type: architecture-decision-record
---

# PI/RufusChat as Local Cognitive Orchestrator and RCK as Operational Truth Layer

## 1. Context and Motivation

ChatGPT web is useful today because it provides a strong conversational interface, coherent reasoning, and a convenient place to coordinate work. In the CHC flow, however, it has important limits: it does not have local filesystem access, it cannot directly coordinate Hermes, it cannot delegate deep code editing to Codex CLI, and it does not provide a structured operational memory layer for traces, states, anchors, handoffs, and evidence.

PI/RufusChat can serve as a local replacement for that conversational orchestration layer. It already fits the shape of a session-based cognitive interface: branches, forks, labels, compaction, resume, and extension hooks. RCK is needed because the operational layer must be separate from the conversation layer. We need a durable place for evidence, state snapshots, anchors, and handoffs that can be audited and reconstructed without polluting the model context.

The goal is not to replace Hermes or Codex CLI. The goal is to place PI/RufusChat above them as the local cognitive orchestrator, while RCK keeps the operational truth.

## 2. Decision

We will use PI/RufusChat as the local cognitive orchestrator and RCK as the operational truth layer.

- PI/RufusChat owns the main conversation, session tree, branching, labels, compaction-visible summaries, and context orchestration.
- RCK owns traces, states, anchors, evidence, handoffs, context packs, and commit links.
- Hermes remains the filesystem and terminal executor.
- Codex CLI remains the deep code editing executor.
- The user remains the final decision-maker.

We will not fork Pi directly as the first implementation path. We will start with a Pi extension POC, then move to real RCK double-write, then formalize Hermes ↔ PI integration, and only later consider an external RufusChat SDK app.

## 3. Layer Responsibilities

### PI/RufusChat

Responsibilities:
- conversation with the user
- cognitive orchestration
- branch and fork management
- coherence across turns
- prompt generation and handoffs
- context injection from RCK
- synthesis of Hermes and Codex outputs
- detection of blockages, drift, and delays

Produces:
- prompts
- branch summaries
- context injections
- user-facing orchestration decisions

Consumes:
- user intent
- safe summaries from RCK
- synthesized outputs from Hermes and Codex

Must not:
- become the sole source of operational truth
- store raw operational evidence as chat content
- edit deeply when delegation is possible
- mix operational memory with conversational memory

### RCK

Responsibilities:
- traces
- states
- anchors
- evidence
- handoffs
- context packs
- commit links
- auditability and reconstruction

Produces:
- versioned operational events
- durable state snapshots
- evidence references
- safe context packs

Consumes:
- Hermes outputs
- Codex outputs
- Pi session references
- branch and anchor references

Must not:
- replace the conversation layer
- manage chat UX
- depend on unstructured prompt dumps

### Hermes

Responsibilities:
- filesystem and terminal execution
- repository inspection
- coordination with Codex CLI when needed
- reporting incidents upstream

Produces:
- inspection results
- shell outputs
- evidence references
- operational summaries

Consumes:
- orchestration prompts from PI/RufusChat
- safe context packs from RCK

Must not:
- decide global strategy
- be the final truth layer
- hide outputs or evidence

### Codex CLI

Responsibilities:
- deep code editing
- patches
- diffs
- tests
- technical verification

Produces:
- code changes
- diffs
- test results
- verifiable technical artifacts

Consumes:
- technical instructions from Hermes
- safe context from PI/RufusChat

Must not:
- manage conversation
- own operational memory
- make strategic decisions

### User

Responsibilities:
- define goals
- approve trade-offs
- resolve ambiguity
- make the final decision

Produces:
- intent
- priorities
- feedback
- approvals

Consumes:
- synthesis from PI/RufusChat
- safe operational evidence from RCK

Must not:
- reconstruct internal state manually
- handle technical orchestration details

## 4. Main Flows

### Normal flow

User → PI/RufusChat → Hermes → Codex CLI if needed → Hermes → PI/RufusChat → User

This is the main CHC flow. PI/RufusChat keeps the conversation coherent while Hermes and Codex perform technical work.

### Evidence flow

Hermes/Codex → RCK evidence → RCK state/context pack → PI/RufusChat

Raw outputs are stored in RCK. PI/RufusChat receives only safe summaries or packs.

### Incident flow

Codex fails or stalls → Hermes reports upstream → PI/RufusChat interprets → User decides

Incidents must travel upstream rather than being hidden inside technical tooling.

### Branch flow

User or PI/RufusChat creates a branch → Pi session tree/fork → RCK branch/anchor → continuity preserved

The conversation may fork, but the operational trace remains linked.

### Context injection flow

RCK state/context pack → PI before_agent_start or context hook → strong model receives safe synthesis

Only safe synthesized context enters the model.

## 5. Design Rules

- Raw operational evidence never enters the LLM directly.
- stdout, stderr, diffs, logs, and other large artifacts are stored as references.
- PI/RufusChat keeps conversation and UX.
- RCK keeps operational truth.
- Hermes does not govern global coherence.
- Codex CLI does not make strategic decisions.
- Incidents travel upstream.
- Evidence travels toward RCK.
- Context injection must be a safe summary or context pack, never a raw dump.

## 6. Model Strategy

PI/RufusChat should use a strong model, ideally GPT-5.5 or equivalent, for:
- coherence
- strategy
- delegation decisions
- evidence synthesis
- branch navigation
- blockage detection

Hermes and Codex may use tactical models or specialized configurations for their narrow tasks. The strong model is reserved for coordination and judgment, not for raw execution.

## 7. Incremental Strategy

### Phase 0
- ADR/spec concept
- RCK event contract v0.1

### Phase 1
- local Pi extension mock
- commands such as /hermes, /state, /rck inject
- mock events stored as Pi custom entries
- no real RCK yet

### Phase 2
- real RCK double-write
- HermesRunRecorded, StatePackCreated, ContextPackInjected
- real evidence/state/context packs

### Phase 3
- formal Hermes ↔ PI integration
- delay and blockage detection
- upstream incident status events

### Phase 4
- external RufusChat app using Pi SDK
- custom UI with branches and projects
- RCK as the real operational engine

## 8. Risks and Mitigations

### Over-architecture too early
Mitigation: validate with a small POC and narrow contracts first.

### Drift between Pi and RCK
Mitigation: use stable IDs, versioned contracts, and post-write validation.

### Coupling to Pi internals
Mitigation: treat Pi as a layer, not the truth source.

### Context pollution
Mitigation: never inject raw operational evidence directly into the LLM.

### Coupling to Hermes CLI details
Mitigation: communicate through event contracts and references.

### UI complexity in a future standalone app
Mitigation: defer the standalone app until the Pi-extension POC and double-write path are proven.

### False autonomy expectations
Mitigation: keep the user as the final decision-maker.

## 9. Alternatives Rejected

- Use only ChatGPT web: rejected because it lacks local filesystem access and operational memory.
- Use only Hermes as orchestrator: rejected because Hermes is an executor, not the cognitive layer.
- Use Pi as the sole source of truth: rejected because operational truth must remain separate.
- Fork Pi immediately: rejected because it increases risk before validation.
- Put all evidence in context: rejected because it pollutes the LLM and breaks separation of concerns.

## 10. Expected Outcome

The system should behave like a local ChatGPT-style cognitive layer, but with stronger operational grounding:

- a coherent main conversation
- branchable session history
- safe context injection from operational truth
- traceable evidence and states
- stable handoffs and anchors
- controlled delegation to Hermes and Codex

## 11. Practical Implication

This ADR establishes a clear split:

- PI/RufusChat is the local cognitive orchestrator.
- RCK is the operational truth layer.
- Hermes executes.
- Codex edits.
- The user decides.

That split is the foundation for a local CHC flow that is more auditable and more adaptable than a web-only chat interface.

## 12. Next Steps

1. Save this ADR in the repository documentation tree.
2. Use the RCK event contract v0.1 as the implementation reference.
3. Build a Pi extension POC for /hermes, /state, and /rck inject.
4. Add real RCK double-write in the next phase.
5. Formalize Hermes ↔ PI incident flow after the bridge is proven.
