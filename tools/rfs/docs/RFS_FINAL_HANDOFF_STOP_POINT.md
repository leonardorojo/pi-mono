# RFS Final Handoff / Stop Point

## 1. State summary

- Active working branch: `feature/rufus-cli-design`
- Phase: **PT13 — Documentation / stop point**
- This cycle is documentation-only.
- It does not add runtime behavior.
- It does not add commands.
- It does not touch `Rufus.RCK.Core`.
- It does not change transport.
- It does not correct `Argument list too long`.

PT12 was validated externally in `ChessBoardApp` and confirmed the live TUI state before this stop point.

Observed PT12 end state:

- RCK HEAD: `b9919c2a680ae7ad5da2c4e75ba3d0cfae775d5e6b81b714cfd6cacc6be136ad`
- states: `20`
- deltas: `19`
- anchors: `3`
- untracked files left in the lab repo:
  - `RFS_TRACE_LAB_NOTE.md`
  - `RFS_TRACE_TEST.md`

## 2. North Star

RFS debe ser una sesión cognitiva del repo.

Canonical flow:

```text
cd repo
rfs

if no .rfs:
  auto-init

prompt first
choose mode:
  1 Direct
  2 Simple
  3 Complete
  4 Plan

main LLM responds

only then:
  State + Delta
```

Principle:

- the user writes the prompt first
- the mode is selected after the prompt
- the main LLM response closes the interaction
- recording happens only after the main LLM responds
- anchors are structural milestones, not per-prompt records

## 3. Current architecture boundary

The current RFS architecture stays layered:

```text
Rufus.Cli
  -> orchestrates commands and TUI UX

Rufus.RCK.Workspace
  -> .rfs, Git context, readers, builders, validators

Rufus.RCK.Core
  -> minimal persistent cognitive kernel

Rufus.Agenting
  -> operational AgentTask / AgentTaskResult layer

Pi integration
  -> JSON / RPC / LLM execution depending on the command
```

Boundary rules:

- `Rufus.RCK.Core` does not know Pi.
- `Rufus.RCK.Core` does not know `Rufus.Agenting`.
- `Rufus.RCK.Core` does not know `Rufus.RCK.Workspace`.
- `Rufus.Agenting` does not depend on RCK Core.
- `Rufus.RCK.Workspace` owns persistence, projection, and validation.
- `Rufus.Cli` owns orchestration and presentation.

## 4. Live TUI state confirmed by PT12

PT12 validated the TUI in a real repository and confirmed:

- `/status`, `/log`, `/model`, `/context`, `/trace`, `/help`, `/exit` respond normally
- `/context` and `/trace` show clear summaries when no session context exists
- `/model` without args shows the current model and source
- `/model <model>` is config-only and does not write RCK
- `/anchor "name"` creates only an Anchor on the current HEAD
- Direct mode records `State + Delta`
- Simple mode records `State + Delta`
- Plan mode records `State + Delta`
- Complete mode shows its governed stages and can fail before the main LLM call with `Argument list too long`
- the TUI remains operable after that failure

## 5. Mode contracts

### Direct

```text
Prompt -> LLM principal -> State + Delta
```

### Simple

```text
Prompt -> Simple Context v0 -> LLM principal -> State + Delta
```

### Complete

```text
Prompt -> Intent -> TraceSliceProposal -> RFS validation -> TraceSlice final -> ContextPack -> LLM principal -> State + Delta
```

### Plan

```text
Prompt -> Simple Context v0 -> LLM principal -> plan response -> State + Delta
```

## 6. Recording rules

- No recording when the prompt is typed.
- No recording when the mode is selected.
- No recording during internal pipeline steps.
- Only the main LLM response closes the interaction and triggers persistence.
- State + Delta summarize the full interaction window.

Record the controlled summary, not raw dumps:

- prompt
- selected mode
- context summary
- validation summary, when applicable
- final response summary
- git context
- artifact metadata
- pipelineSummary

## 7. Anchors

Anchors are created only on:

- init / genesis
- git commit boundary
- explicit `/anchor "name"`

Explicit `/anchor`:

- creates an Anchor on the current HEAD
- does not create State
- does not create Delta
- does not call the LLM

## 8. Command governance

Available internal commands:

- `/status`
- `/log`
- `/model`
- `/model <model>`
- `/context`
- `/trace`
- `/anchor "name"`
- `/help`
- `/exit`

Rules:

- informational commands do not write RCK
- `/model <model>` only writes `.rfs/config.json`
- `/context` and `/trace` are summaries, not dumps
- `/anchor` writes Anchor only

The technical command surface remains available as backend/debug tooling.

## 9. Context budget usage reporting

Simple, Complete, and Plan modes surface a controlled context report when they build a context:

- estimated chars
- estimated tokens
- model budget
- context usage
- transport size
- transport risk
- truncated

Interpretation:

- model budget may be `unknown` when no clean source exists
- context window budget and process-argument transport risk are different problems
- a model can have enough window and still fail on the OS argument limit

## 10. Known risk

Complete mode can fail with:

```text
Argument list too long
```

Status:

- known risk
- not corrected in this cycle
- does not block Direct, Simple, or Plan
- PT12 confirmed that if the failure happens before the main LLM response, no State or Delta is created and RCK is not corrupted

Future mitigations that remain out of scope for this cycle:

- pass the prompt by stdin
- use a controlled temporary file
- compact the ContextPack
- add a transport budget warning
- degrade Complete to Simple when needed
- add real per-model budget metadata in config

## 11. Stop point

This is the stop point of the current cycle.

Do not add more runtime features in this cycle.
Do not add new commands.
Do not touch `Rufus.RCK.Core`.
Do not migrate legacy agent behavior.
Do not deprecate bridges.
Do not create `ModelRouter`.

The next work should begin only if a new phase is explicitly opened.

## 12. Validation summary

PT12 external validation in `ChessBoardApp` confirmed:

- internal commands OK
- `/model` config-only OK
- `/anchor` explicit OK
- Direct mode OK
- Simple mode OK
- Plan mode OK
- Complete mode failed in a controlled way with `Argument list too long`
- leak/safety grep checks were clean
- no commits
- no push
- no source-code changes
- no RCK Core changes

## 13. Non-goals

This cycle does not:

- add an autonomous agent that edits code
- add apply-patch behavior
- add a write sandbox
- migrate legacy `rfs agent`
- deprecate legacy bridges
- create `ModelRouter`
- correct `Argument list too long`
- change prompt transport
- pass prompts by stdin
- use temporary files for transport
- compact ContextPack
- touch `Rufus.RCK.Core`
