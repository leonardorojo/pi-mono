# RufusChat Minimal UI 7A

## 1. Goal

Define a minimal, documentation-only UI concept for PI/RufusChat that acts as a local cognitive orchestrator and conversational control room.

This phase is intentionally non-runtime:
- no UI implementation
- no runtime code changes
- no test changes
- no dependency changes

The design preserves the current architecture:
- **RCK** remains the auditable operational truth
- **Hermes** remains the local executor
- **PI/RufusChat** remains the conversational orchestrator
- **the user** remains the final decision-maker
- **Codex** stays out of scope for this phase

## 2. Non-goals

- Implementing a real UI
- Modifying any runtime behavior
- Changing tests
- Touching `.pi/rck`
- Touching `RufusLab.RCK.Cli`
- Opening or wiring a Codex executor
- Adding new packages or dependencies
- Exposing raw stdout/stderr in the UI layer
- Replacing RCK with chat memory
- Turning PI into the truth layer

## 3. Design principles

1. **Operational truth stays external**
   - The UI may summarize state, but RCK remains the authoritative record.

2. **Conversation first, operations visible**
   - The main surface should support normal dialogue while making operational status easy to inspect.

3. **Safe by default**
   - Show normalized summaries and attention flags, not raw logs or unfiltered execution output.

4. **Minimal cognitive load**
   - Prefer a small number of stable zones over dense dashboards.

5. **User control is explicit**
   - Every action path should make approval, escalation, and supervision visible.

6. **State is compact**
   - The UI should collapse complex backend conditions into a small state vocabulary.

7. **No hidden execution**
   - Hermes activity and RCK status should be visible as intent, progress, or attention signals.

## 4. Minimal UI layout

A minimal layout should be organized vertically:

1. **Session header**
2. **RCK operational panel**
3. **Conversation area**
4. **Actions bar**
5. **Supervision / attention area**

This layout supports the North Star:
- PI/RufusChat orchestrates locally
- RCK explains what is happening operationally
- Hermes executes tasks
- the user can intervene at any time

## 5. UI zones

### Session header

Purpose:
- identify the active session
- show the current orchestration mode
- show a compact state indicator

Recommended contents:
- session name or identifier
- current state: `OK`, `Attention`, `Running`, or `Unknown`
- short mode tag, such as `orchestrating`, `watching`, or `blocked`
- optional timestamp for the latest meaningful state change

### RCK operational panel

Purpose:
- surface the current operational truth without dumping raw artifacts

Recommended contents:
- current RCK status summary
- active anchors
- active or recent supervised items
- safe evidence references
- stable IDs only when needed for traceability

This panel should answer:
- What is the system doing?
- What is anchored?
- What requires attention?
- What is safe to trust right now?

### Conversation area

Purpose:
- hold the user conversation as the primary interaction surface

Recommended contents:
- user messages
- assistant responses
- short operational annotations when relevant
- compact references to RCK state, not raw payloads

Behavior:
- normal conversation remains readable even when operational activity is active
- operational events should not overwhelm the conversation stream

### Actions bar

Purpose:
- provide the smallest possible set of explicit command affordances

Recommended contents:
- quick actions for state inspection
- quick actions for injection and anchoring
- quick actions for supervision
- quick actions for Hermes mode selection

This area should prefer one-click or one-command access to the documented mappings below.

### Supervision / attention area

Purpose:
- show anything that may require user review, confirmation, or escalation

Recommended contents:
- attention flags
- blocked or waiting states
- supervised tasks
- pending decisions
- confidence or completeness hints

This area is not for general logs. It is for the subset of state that matters for decision-making.

## 6. Command mapping

The UI should map commands to stable conceptual actions.

### `/rck status`

Meaning:
- retrieve a compact operational snapshot

UI should show:
- current state
- latest safe summary
- whether any attention is required

### `/rck list`

Meaning:
- enumerate active or recent operational items

UI should show:
- concise list of entries
- stable identifiers
- short labels or summaries

### `/rck supervise`

Meaning:
- activate or inspect supervised attention paths

UI should show:
- supervision targets
- current supervision state
- items waiting for user review

### `/state`

Meaning:
- return the current local session state in a compact form

UI should show:
- overall state
- latest orchestrator summary
- any transition that changed the state

### `/rck inject`

Meaning:
- inject safe, curated context into the session

UI should show:
- what category of context was injected
- whether injection succeeded
- which safe summary or pack was used

### `/rck anchor`

Meaning:
- persist or reference an anchor for continuity and auditability

UI should show:
- anchor identity
- anchor purpose
- anchor timestamp or version if available

### `/hermes fake`

Meaning:
- simulated Hermes path for safe local orchestration testing

UI should show:
- fake mode indicator
- simulated execution status
- explicit distinction from real execution

### `/hermes real gated`

Meaning:
- real Hermes execution path behind an explicit gate

UI should show:
- gated state
- approval or readiness requirement
- execution eligibility only after the gate is satisfied

## 7. Minimal state model

Use a small state vocabulary.

### `OK`

Definition:
- normal state
- no immediate attention required
- system is within expected bounds

### `Attention`

Definition:
- something needs review, clarification, or user decision
- execution may still be possible, but caution is required

### `Running`

Definition:
- an operation or orchestration flow is active
- progress is ongoing
- final outcome is not yet complete

### `Unknown`

Definition:
- state cannot be determined safely
- information is incomplete, stale, or not yet established

## 8. needsAttention representation

`needsAttention` should be represented as a safe, compact UI signal, not as raw diagnostic output.

Recommended shape:
- boolean flag: yes/no
- short reason label
- optional severity hint: low / medium / high
- optional user action hint: review / approve / wait / intervene

Guidelines:
- do not expose raw stack traces or raw executor output
- do not mirror entire logs into the UI
- prefer one-line summaries that explain *why* attention is needed
- if the reason is ambiguous, prefer `Unknown` over a false sense of precision

## 9. Safe evidence policy

The UI must obey a safe evidence policy.

Rules:
- do not display raw stdout/stderr
- do not expose unfiltered logs
- do not surface large execution payloads directly in the conversation area
- do not turn evidence into chat content by default
- show references, summaries, and normalized facts instead

Allowed evidence forms:
- stable identifiers
- safe summaries
- compact status labels
- reference pointers to RCK-managed evidence
- user-facing explanations derived from audited state

The intent is to preserve auditability without leaking noisy or unsafe artifacts into the conversational layer.

## 10. User flows

### Flow A: Check current state
1. User opens the session.
2. Header shows `OK`, `Attention`, `Running`, or `Unknown`.
3. RCK panel shows a compact status summary.
4. Conversation area remains available for normal interaction.

### Flow B: Inspect operational truth
1. User requests status.
2. `/rck status` or `/state` is mapped to a compact summary.
3. UI shows only safe state and references.
4. User can decide whether to continue or intervene.

### Flow C: Inject context
1. User selects or requests injection.
2. `/rck inject` maps to a safe context pack.
3. UI confirms the injection category and result.
4. Conversation continues with the injected context available to orchestration.

### Flow D: Anchor continuity
1. User or orchestrator creates an anchor.
2. `/rck anchor` persists the continuity point.
3. UI shows the anchor as a stable reference.
4. Later turns can reconnect to the anchored state.

### Flow E: Supervised execution
1. A task enters supervision.
2. `/rck supervise` exposes the supervised path.
3. The attention area shows what is pending.
4. User can approve, wait, or intervene.

### Flow F: Hermes simulation vs real execution
1. User tests with `/hermes fake`.
2. UI makes it obvious that execution is simulated.
3. When ready, `/hermes real gated` is used.
4. The gate makes real execution an explicit decision, not an accidental default.

## 11. Out-of-scope list

- Real component implementation
- Visual design system selection
- Theme work
- Animations
- Mobile adaptation
- Keyboard shortcut design
- Complex navigation
- Drag-and-drop session management
- Rich trace viewers
- Raw log consoles
- Diff viewers
- Multi-pane editor tooling
- Codex orchestration
- Hermes executor redesign
- RCK schema changes
- Any runtime feature work

## 12. Recommendation for Phase 7B

Phase 7B should convert this design into a minimal, read-only runtime shell with real data binding, while still preserving the separation of concerns:

- keep RCK as the auditable operational source
- keep Hermes as the executor
- keep PI/RufusChat as the orchestrator
- render only safe summaries in the UI
- introduce the smallest possible interactive surface first

Recommended 7B focus:
- real state hydration
- real command-to-panel binding
- attention highlighting
- anchor visibility
- supervised-task visibility
- explicit gated action entry points

The next implementation should still avoid exposing raw executor output and should remain small enough to validate the orchestration model before any broader UI expansion.
