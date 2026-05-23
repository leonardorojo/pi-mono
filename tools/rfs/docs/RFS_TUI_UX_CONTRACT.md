# RFS TUI UX Contract

PT5 status: bare `rfs` now enters a minimal TUI shell with auto-init, header rendering, `/status` / `/help` / `/exit`, prompt-first mode selection, and a real Direct mode path. PT5 also defines the finalized TUI recording contract for the main-LLM response path. PT6 defines the future Simple Context contract for Simple mode.

## 1. North Star

RFS debe ser una sesión cognitiva del repo.

Flujo canónico:

```text
cd repo
rfs

si no hay .rfs:
  auto-init

entrar TUI

usuario escribe prompt

RFS pregunta modo:
  1. Directo
  2. Simple
  3. Completo
  4. Plan

RFS ejecuta pipeline

RFS registra automáticamente State + Delta

usuario sigue trabajando
```

This contract defines the future primary UX. It does not change runtime behavior in PT0.

## 2. Auto-init

- `rfs` without arguments enters the TUI.
- If `.rfs` does not exist, the workspace is initialized automatically.
- The user does not need to run `rfs init` to use the main UX.
- `rfs init` may remain as a technical, advanced, idempotent command, but it is not the primary flow.

Example:

```text
RFS
────────────────────────
Workspace not initialized.

Initializing RFS workspace...
✓ .rfs created
✓ RCK initialized
✓ genesis state created
✓ genesis anchor created

Entering RFS session.
```

## 3. Session header

The TUI header must expose the current session state at a glance.

Example:

```text
RFS · ChessBoardApp
────────────────────────
Model: gpt-5.4-mini
RCK: states 6 · deltas 5 · anchors 1
Git: master · dirty true

>
```

Header intent:

- show the repo/workspace identity
- show the active model
- show RCK scale and progress
- show git branch and dirty state
- keep the prompt immediately available

## 4. Prompt-first UX

The user types the task directly:

```text
> Implementar una acción para resetear el tablero
```

Only after Enter, RFS asks how to process the prompt:

```text
¿Cómo querés procesar este prompt?

1. Directo
2. Simple
3. Completo
4. Plan
```

Rules:

- the prompt is entered first
- the mode is selected after the prompt
- RFS is already inside the session when the user types
- `rfs chat` is not the primary UX
- `rfs prompt` is not the primary UX
- the user should not be forced through a preflight wizard before writing the prompt

## 5. Modes and pipelines

### 5.1 Mode 1 — Directo

Pipeline:

```text
Prompt
→ Pi JSON ask
→ respuesta final
→ State
→ Delta
```

Characteristics:

- does not use TraceSlice
- does not use ContextPack
- does not use RCK memory as context
- still records State + Delta
- does not modify code

### 5.2 Mode 2 — Simple

Pipeline:

```text
Prompt
→ Simple Context
→ LLM principal
→ respuesta
→ State
→ Delta
```

Simple Context contract: [`RFS_SIMPLE_CONTEXT_CONTRACT.md`](RFS_SIMPLE_CONTEXT_CONTRACT.md).

Characteristics:

- recommended for daily use
- uses lightweight context only
- Simple Context is compact, safe, and intentionally smaller than a full ContextPack
- does not use the full TraceSliceProposal/Validation stack
- validation can remain absent unless later added explicitly
- does not modify code

### 5.3 Mode 3 — Completo

Pipeline:

```text
Prompt
→ Intent
→ TraceSliceProposal
→ RFS validation
→ TraceSlice final
→ ContextPack
→ LLM principal
→ respuesta
→ State
→ Delta
```

Rule of authority:

- the agent proposes
- RFS validates
- ContextPack materializes
- the main LLM answers
- RCK remembers

Characteristics:

- this is the fully governed path
- proposal output is not authoritative
- validation is authoritative
- the resulting context is materialized only after validation

### 5.4 Mode 4 — Plan

Pipeline:

```text
Prompt
→ contexto simple o completo
→ plan de implementación
→ State
→ Delta
```

Characteristics:

- does not modify code
- does not apply patches
- does not create commits
- does not run an autonomous writing agent
- is for planning only

## 6. Automatic recording

Inside the TUI there is no question:

- “¿Querés guardar?”

Rule:

- every finalized interaction generates one State + one Delta automatically
- the recording happens only when the main LLM responds
- prompt entry, mode selection, and internal pipeline steps do not create intermediate persisted states

Record at least:

- prompt
- selected mode
- final response summary
- answer summary
- git context
- artifacts metadata
- model or agent used, if available
- context used, if applicable
- validation summary, if applicable

Do not record State/Delta for commands that are purely informational:

- `/status`
- `/log`
- `/help`
- `/exit`
- `/model` when it does not change anything

Do record State/Delta for:

- prompt + Directo
- prompt + Simple
- prompt + Completo
- prompt + Plan

PT4 recording contract details live in [`RFS_TUI_RECORDING_CONTRACT.md`](RFS_TUI_RECORDING_CONTRACT.md).

## 7. Anchors

Anchor is not created on every interaction.

Anchor is created only on:

- init / genesis
- git commit change
- explicit `/anchor`

Example:

```text
/anchor "reset-board-plan-ready"
```

Effect:

- creates an Anchor on the current State
- marks a cognitive milestone
- does not modify code
- does not replace the normal State + Delta flow

## 8. Commit boundary anchors

RFS may detect that the git commit changed between interactions.

If:

```text
previousGitCommit != currentGitCommit
```

then RFS creates an automatic anchor:

```text
type: git-commit-boundary
commit: <current>
state: <current-state>
```

Meaning:

- the repo moved to a new commit boundary
- the milestone is structural and automatic
- the anchor does not imply code changes inside RFS itself

## 9. Internal commands in the TUI

Suggested internal commands:

- `/status`
- `/log`
- `/model`
- `/model <model>`
- `/context`
- `/trace`
- `/anchor`
- `/help`
- `/exit`

Rules:

- `/status`, `/log`, `/help`, and `/exit` do not write RCK
- `/model <model>` writes `.rfs/config.json` but does not write RCK
- `/context` shows the last ContextPack used
- `/trace` shows the last TraceSlice / validation summary
- `/anchor` creates an Anchor

## 10. Technical commands that remain available

The current technical surface is not removed.
It remains available as backend, debug, or advanced tooling.

Current commands:

- `rfs status`
- `rfs log`
- `rfs ask`
- `rfs ask --record`
- `rfs intent`
- `rfs intent --record`
- `rfs trace-slice`
- `rfs trace-slice-proposal`
- `rfs trace-slice-validate`
- `rfs context-pack --trace-slice`
- `rfs context-pack --trace-slice-validated`
- `rfs trace-slice-proposal-llm`
- `rfs trace-slice-validate-llm`
- `rfs model get`
- `rfs model set`
- `rfs model list`
- `rfs agent`
- `rfs agent --record`
- `rfs agent-json`
- `rfs pi`

Main UX direction:

```text
cd repo
rfs
> prompt
> elegir modo
> respuesta
> State + Delta automático
```

## 11. No-goals for the TUI cycle

This cycle does not:

- add an autonomous agent that edits code
- add apply-patch behavior
- add a write sandbox
- migrate `rfs agent` legacy behavior
- deprecate bridges
- add a ModelRouter
- touch `Rufus.RCK.Core`
- change the core schema
- remove the technical commands above
- work on `ChessBoardApp`

## 12. PT roadmap

- PT0 — TUI UX Contract
- PT1 — Bare `rfs` auto-init + enter session
- PT2 — Minimal TUI shell
- PT3 — Prompt-first mode selection
- PT4 — Auto-record State + Delta
- PT5 — Direct mode
- PT6 — Simple mode
- PT7 — Complete mode
- PT8 — Plan mode
- PT9 — `/anchor` + commit-boundary anchors
- PT10 — Internal commands polish
- PT11 — Manual lab in `ChessBoardApp`
- PT12 — Documentation / stop point

## 13. PT0 close criteria

PT0 is closed when all of the following are true:

- `tools/rfs/docs/RFS_TUI_UX_CONTRACT.md` exists
- `README.md` or governance points to this contract
- it is clear that `rfs` without arguments is the main UX
- it is clear that auto-init replaces `rfs init` as the primary flow
- it is clear that every interaction generates State + Delta
- it is clear that anchors are milestones only
- no runtime changes were made

## 14. Relationship to the rest of RFS docs

- `RFS_COMMAND_GOVERNANCE.md` remains the command-surface policy reference.
- `RFS_FINAL_HANDOFF_STOP_POINT.md` remains the closeout note.
- This document is the canonical UX contract for the future TUI session model.
