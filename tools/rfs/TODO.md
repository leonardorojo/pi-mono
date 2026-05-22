# TODO — Rufus CLI / RCK Cognitive Git

## Current baseline

`rfs` currently supports:

- `rfs init`
- `rfs status`
- `rfs ask`
- `rfs ask --record`
- `rfs agent`
- `rfs agent --record`
- `rfs pi`
- `rfs help`
- `rfs --version`

Current architecture:

```text
Rufus.Cli
  -> Rufus.RCK.Workspace
      -> Rufus.RCK.Core
```

Core rule:

```text
RCK Core is the cognitive Git plumbing.
rfs is the operational porcelain.
Workspace is the adapter between them.
```

`Rufus.RCK.Core` must remain simple and pure.

## 0. RCK DAG principles

Preserve these rules before adding new RCK features:

- reference or reproduce, do not duplicate
- keep the DAG consistent, robust, and non-redundant
- keep the DAG complete but optimized for cognitive value
- treat the Context Pack as an export projection, not the storage model
- future features must respect the reference/reproduce rule, non-redundancy, and the complete-but-optimized constraint

---

## 1. Immediate next step — rfs log

### Goal

Add a read-only command:

```bash
rfs log
```

Purpose:

Show the active cognitive history from `.rfs/rck/HEAD` backwards through Deltas.

### Requirements

`rfs log` should:

- Start from `.rfs/rck/HEAD`
- Follow the active State/Delta chain backwards
- Ignore orphan States/Deltas not reachable from HEAD
- Show a compact human-readable history

Suggested output:

```text
rfs log

HEAD: <state-short-id>

1. <state-short-id>
   mode: agent
   prompt: inspect tools/rfs
   answer: Here’s a quick inspection...
   git: <commit-short> dirty=true
   artifacts:
     - modified tools/rfs/README.md
   delta: <delta-short-id>
   created: 2026-05-21T...

2. <state-short-id>
   mode: ask
   prompt: Respond in one short sentence...
   answer: RCK is...
   git: <commit-short> dirty=false
   delta: <delta-short-id>
```

### Constraints

- Read-only
- Do not modify `.rfs/`
- Do not touch `Rufus.RCK.Core`
- Logic should live in `Rufus.RCK.Workspace`
- CLI should only dispatch and format output

---

## 2. rfs show

### Goal

Add:

```bash
rfs show <state-id|delta-id|anchor-id>
```

Purpose:

Inspect one RCK object in readable form.

### Requirements

Should support:

```bash
rfs show <state-id>
rfs show <delta-id>
rfs show <anchor-id>
```

Should display:

- Type
- Full id
- Metadata
- Decoded payload
- Related IDs

For State:

- `payloadCanonicalJson` decoded
- `refs`
- `meta`

For Delta:

- `fromStateId`
- `toStateId`
- `ops`
- decoded `valueJson`
- `refs`
- `evidenceRefs`
- `meta`

For Anchor:

- `stateId`
- `parentAnchorIds`
- `meta`

---

## 3. Context Pack generation

### Goal

Add a future command:

```bash
rfs context-pack
```

Purpose:

Generate a compact LLM-friendly context from the DAG.

### Concept

The DAG is storage and traceability.

The Context Pack is what should be passed to an LLM.

### Suggested content

```json
{
  "head": "...",
  "currentState": {},
  "recentTransitions": [],
  "anchors": [],
  "changedArtifacts": [],
  "git": {}
}
```

### Requirements

Context Pack should include:

- HEAD State
- Last N reachable transitions
- Recent prompts and summaries
- Recent artifact paths
- Relevant anchors
- Git branch/commit/dirty
- No full diffs
- No full file contents unless explicitly requested

---

## 4. Artifact model improvements

### Current behavior

RCK currently records changed artifact paths using:

```bash
git status --porcelain
```

It records:

- kind
- path
- changeType
- gitStatus
- source

It does not record:

- file content
- diffs
- hashes
- blob IDs
- patches

### TODO

Improve artifact classification:

```text
changedArtifacts
  files changed in worktree

referencedArtifacts
  files read or inspected

generatedArtifacts
  files created by an action

committedArtifacts
  files included in a Git commit
```

### Rule

```text
RCK references artifacts.
Git stores contents and diffs.
```

---

## 5. Agent tool evidence improvements

### Current behavior

`rfs agent --record` captures basic tools:

```json
{
  "name": "read_file",
  "status": "completed"
}
```

### TODO

Improve tool evidence gradually:

- tool name
- status
- arguments/path if safe
- result summary
- error if failed
- whether tool read or modified anything

Do not store large outputs by default.

---

## 6. DAG validation

### Goal

Add future validation command:

```bash
rfs status --validate
```

or:

```bash
rfs rck validate
```

### Validate

- HEAD exists
- HEAD points to existing State
- every Delta points to existing From/To States
- Anchors point to existing States
- no cycles
- active chain reachable from HEAD
- orphan objects reported but not treated as fatal by default

### Note

Use `Rufus.RCK.Core` graph validation where possible.

---

## 7. Anchors

### Current behavior

Anchors are created for:

- genesis
- detected Git commit changes

### TODO

Future anchors:

```bash
rfs anchor "decision-rck-core-is-plumbing"
```

Anchor types:

- genesis
- git-commit
- user-milestone
- branch-start
- cognitive-merge

### Rule

```text
Many States.
Few meaningful Anchors.
```

---

## 8. Cognitive branch / merge model

### Future concept

Do not implement yet.

Potential future concepts:

- cognitive branch
- cognitive merge
- branch heads
- merge anchors
- divergence/convergence of reasoning paths

This should remain separate from Git branches, although it may reference them.

---

## 9. Recording policy

### Current behavior

Recording is explicit:

```bash
rfs ask --record
rfs agent --record
```

Normal commands do not record:

```bash
rfs ask
rfs agent
```

### TODO

Decide later whether recording should become:

- always explicit
- default on
- configurable per workspace
- configurable per command

Do not change yet.

---

## 10. rfs pi recording

### Current behavior

`rfs pi` is passthrough to Pi TUI.

It does not record detailed interaction.

### Reason

`rfs` does not currently control or capture the full TUI conversation.

### TODO

Leave untouched for now.

Potential future options:

- record only launch/exit
- add a non-TUI Pi-backed mode
- avoid recording TUI entirely

---

## 11. Schema discipline

### Rule

Every RCK payload should remain versioned:

```json
{
  "schemaVersion": 1,
  "type": "..."
}
```

Current important payloads:

- `rufus.initial-state`
- `rufus.interaction-state`
- `rufus.interaction-delta`

### TODO

Document schemas explicitly later:

```text
docs/rufus-cli/schemas/
  interaction-state-v1.md
  interaction-delta-v1.md
  artifact-change-v1.md
```

---

## 12. Do not do yet

Avoid premature expansion:

- Do not add TraceSlice yet
- Do not add sessions yet
- Do not store full file contents
- Do not store full diffs
- Do not add semantic diff generation yet
- Do not make RCK Core depend on Workspace or CLI
- Do not make RCK Core aware of Git, Pi, Node, agents, or `.rfs/`

---

## Recommended next microphase

Implement:

```bash
rfs log
```

Why:

`rfs` now records States and Deltas, but the cognitive history is still hard to inspect without reading JSON files manually.

`rfs log` will make the DAG usable.
