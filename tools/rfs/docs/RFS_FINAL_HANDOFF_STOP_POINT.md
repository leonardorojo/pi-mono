# RFS Final Handoff / Stop Point

## 1. State summary

- Active working branch: `feature/rufus-cli-design`
- Expected repo state at closeout:
  - working tree clean
  - `origin/feature/rufus-cli-design` updated
- Current focus:
  - PT0: RFS TUI UX Contract
  - governed prompt -> Intent -> TraceSlice -> ContextPack pipeline remains documented as backend policy

This document is the PT0 closeout note for the current documentation cycle.
It is documentation-only.
It does not add features.
It does not add commands.
It does not modify runtime behavior.
It does not touch `Rufus.RCK.Core`.
It does not write RCK.

The canonical TUI UX contract lives in `RFS_TUI_UX_CONTRACT.md`.

## 2. North Star

```text
Prompt
  -> Intent
  -> TraceSliceProposal
  -> RFS validation
  -> TraceSlice final
  -> ContextPack
  -> LLM principal
```

Principle:

- The agent proposes.
- RFS validates.
- RCK remembers.
- ContextPack materializes.
- The LLM principal responds.

## 3. Current architecture

The current architecture is intentionally layered:

```text
Rufus.Cli
  -> orchestrates commands

Rufus.RCK.Workspace
  -> filesystem, .rfs, git context, readers/builders/validators

Rufus.RCK.Core
  -> kernel minimal type system for cognitive git-like state

Rufus.Agenting
  -> executes AgentTask / AgentTaskResult

Pi integration
  -> RPC / JSON mode / LLM proposals depending on the command
```

Boundary rules:

- `Rufus.RCK.Core` does not know Pi.
- `Rufus.RCK.Core` does not know Agenting.
- `Rufus.RCK.Core` does not know Workspace.
- `Rufus.Agenting` does not depend on RCK.
- `Rufus.RCK.Workspace` adapts and validates.
- `Rufus.Cli` orchestrates.

## 4. RCK Core boundary

`Rufus.RCK.Core` remains pure and minimal.

It contains structural wrappers such as:

- `State`
- `Delta`
- `Anchor`
- `Ref`
- `EvidenceRef`
- `PatchOp` if applicable

Semantic payloads live inside the payload itself, for example:

- `interaction-state`
- `agent-task-state`
- `trace-slice`
- related payload-specific shapes

This phase does not alter that boundary.
No core changes are expected unless there is a separate explicit architectural decision.

## 5. Pipeline available today

### A. Baseline TraceSlice

```text
rfs trace-slice "<prompt>"
rfs context-pack --trace-slice "<prompt>"
```

Meaning:

- `rfs trace-slice` builds the deterministic TraceSlice baseline.
- `rfs context-pack --trace-slice` materializes a ContextPack from that baseline.

### B. Governed deterministic pipeline

```text
rfs trace-slice-proposal "<prompt>"
rfs trace-slice-validate "<prompt>"
rfs context-pack --trace-slice-validated "<prompt>"
```

Meaning:

- `rfs trace-slice-proposal` proposes a candidate TraceSlice.
- `rfs trace-slice-validate` is the authority boundary and emits the final TraceSlice.
- `rfs context-pack --trace-slice-validated` materializes from the validated TraceSlice.

### C. LLM experimental proposal pipeline

```text
rfs trace-slice-proposal-llm "<prompt>"
rfs trace-slice-validate-llm "<prompt>"
```

Meaning:

- the LLM only proposes;
- RFS validation remains authoritative;
- proposal does not equal final decision;
- validation emits the final TraceSlice;
- ContextPack still materializes only from the validated result.

## 6. Main command categories

See `RFS_COMMAND_GOVERNANCE.md` for the canonical command matrix.
This section is a closeout summary, not a second source of truth.

| Category | Stability | Writes RCK | Pi / RPC / JSON / legacy | Notes |
| --- | --- | --- | --- | --- |
| Workspace / RCK | stable | mixed | workspace / Git / local projection | `init`, `status`, `log`, `context-pack` |
| Models | stable | no | local config / Pi RPC | `model get`, `model set`, `model list` |
| Ask | stable | only `ask --record` | Pi JSON mode | `ask`, `ask --record`, `ask-json` |
| Intent / TraceSlice | mixed | only record variants | deterministic / Pi LLM / validation | proposal, validation, and intent paths |
| Agent | legacy current / experimental | only `agent --record` | legacy bridge / Pi JSON-tools experimental | `agent`, `agent --record`, `agent-json` |
| Pi | passthrough | no | direct Pi surface | `rfs pi` |

Notes:

- Stable means the command is part of the current supported surface.
- Experimental means the command exists but the pipeline is still being hardened.
- Legacy current means the command still exists, but it is not the preferred long-term bridge design.

## 7. What writes RCK

Writes RCK:

- `rfs ask --record`
- `rfs intent --record`
- `rfs agent --record`

Does not write RCK:

- `rfs trace-slice`
- `rfs trace-slice-proposal`
- `rfs trace-slice-validate`
- `rfs trace-slice-proposal-llm`
- `rfs trace-slice-validate-llm`
- `rfs context-pack`
- `rfs context-pack --trace-slice`
- `rfs context-pack --trace-slice-validated`
- `rfs ask`
- `rfs ask-json`
- `rfs intent`
- `rfs model get`
- `rfs model set` does not write RCK, although it does write `.rfs/config.json`
- `rfs model list`
- `rfs status`
- `rfs log`

## 8. Agent legacy status

Current status:

- `rfs agent` remains legacy current.
- `rfs agent --record` remains legacy current.
- `rfs agent-json` remains experimental.
- No migration has been completed yet.
- No deprecation has been issued yet.
- Legacy bridges remain in place.

Reopen only with a clear decision on:

- security,
- tool parity,
- sandbox behavior,
- and whether `agent-json` can actually replace the legacy path.

## 9. LLM proposal status

Current status:

- `trace-slice-proposal-llm` exists.
- `trace-slice-validate-llm` exists.
- The LLM only proposes.
- Parsing is strict.
- Shape is mandatory.
- RFS validation remains the authority.
- `contents`, `diffs`, `stdout`, `stderr`, and `jsonl` are not accepted as proposal payloads.
- No RCK is written by the proposal path.

## 10. Known risks

- `agent-json` depends on Pi tools enforcement and is not a safe replacement for legacy `agent` yet.
- LLM proposal remains experimental and must stay subordinated to validation.
- There are already many experimental command paths; do not add more without a clear decision.
- ContextPack scoped exports select payloads according to policy; this is not a summary-only mode.
- Anchors are conceptually integrated, but anchor-aware selection may continue to evolve.
- Legacy bridges are still live.

## 11. Stop point

This is the stop point of the current cycle.

Do not continue adding features in this cycle.
Do not add new commands.
Do not touch `Rufus.RCK.Core`.
Do not migrate legacy agent behavior.
Do not deprecate legacy bridges.
Do not create `ModelRouter`.

The next work should begin only if a new phase is explicitly opened.

## 12. Post-closeout backlog

### P25 — Reconsider `agent` / `agent --record`

- resolve security / parity / sandbox concerns
- decide whether `agent-json` can replace the legacy path
- define RCK evidence for agent execution

### P26 — Deprecate legacy bridges

- only after ask / agent / recording / fallback behavior is stabilized

### Future possibilities

- agent selection by agent id, without `ModelRouter`
- ContextPack summary mode if a real need appears
- TraceSlice persistence if the justification becomes strong enough
- more tests around LLM proposal behavior

## 13. Validation checklist

Planned closeout validations:

- `git status --short --branch`
- `dotnet build tools/rfs/Rufus.Cli.sln`
- `dotnet run --project tools/rfs/tests/Rufus.Cli.ParserChecks/Rufus.Cli.ParserChecks.csproj`
- `git diff --check`

No external runtime validation is required for this phase because the phase is documentation-only.
