# RFS Test Baseline

Last updated: 2026-05-29
Branch baseline: `feature/rfs-test-baseline-docs` (post 6f303e0f)

## Purpose

This document describes the current test baseline for `tools/rfs` after two
microphase fixes that made the full parser-checks suite deterministic:

1. `2486337a` — fix(rfs): avoid burst paste capture for redirected TUI input
2. `6f303e0f` — test(rfs): mock Pi for complete mode legacy check

All suites now pass without depending on Pi real auth/model/env.

## Quick reference

```bash
# Build (required before running tests)
dotnet build tools/rfs/Rufus.Cli.sln

# Default: core + tui (fast, no Pi)
dotnet run --project tools/rfs/tests/Rufus.Cli.ParserChecks/Rufus.Cli.ParserChecks.csproj

# All suites at once
dotnet run --project .../Rufus.Cli.ParserChecks.csproj
dotnet run --project .../Rufus.Cli.ParserChecks.csproj -- --core-only
dotnet run --project .../Rufus.Cli.ParserChecks.csproj -- --tui-only
dotnet run --project .../Rufus.Cli.ParserChecks.csproj -- --integration-only
dotnet run --project .../Rufus.Cli.ParserChecks.csproj -- --long-paste-only
dotnet run --project .../Rufus.Cli.ParserChecks.csproj -- --legacy-only
```

## Suite catalog

### 1. `--core-only`

**Purpose**: Deterministic parser, codec, and agent contract checks. No Pi, no
TUI, no filesystem side effects beyond temp dirs.

**Requires Pi real**: No.
**Uses mocks**: Yes — mock `pi` bash scripts with canned JSONL fixtures.
**Covers**:
- `PiJsonEventRunner` answer extraction (structured, delta, delta-then-final, no-answer, invalid JSONL)
- `IntentInferenceAgent` deterministic path
- `PromptIntentJsonCodec` round-trip
- `PiIntentInferenceAgent` parse/error paths
- `PiTraceSliceProposalAgent` checks
- `PiConversationalMemoryAgent` checks
- `PiPrincipalAnswerAgent` checks
- `PrincipalAnswerAgentContractChecks`
- `PiJsonRunnerWorkspaceModelCase`
- `RckTraceSliceProposalValidator` critical cases
- `RckAnchorExpansionServiceChecks`
- `RckDagQuickIndexV1BuilderChecks`
- `RckConversationalMemoryInputBuilderChecks`

**When to run**: On every change to agenting, RCK workspace, or Pi integration code.

---

### 2. `--tui-only`

**Purpose**: Deterministic TUI parser and renderer checks. No Pi, no real TUI
session — uses unit-level assertions and simulated sessions with single-line
redirected input.

**Requires Pi real**: No.
**Uses mocks**: No — tests are pure parser/renderer assertions or use empty
repos with no LLM interaction.
**Covers**:
- `RfsTuiModeSelectionParser` cases
- `RfsTuiCommandSuggestion` cases (parser-level)
- `RfsTuiModelPickerChecks`
- `RfsTuiMarkdownLiteChecks`
- `RfsTuiAnsiLeakChecks`
- `RfsTuiPiRunRuntimeChecks`
- `RfsTuiInitializedSessionCase` (slash commands: `/status`, `/help`, `/log`, `/context`, `/trace`, `/xyz`)
- `RfsTuiAnchorUsageSessionCase` (no name → usage)
- `RfsTuiAnchorCommandSessionCase` (named → milestone anchor)
- `RfsTuiAutoInitSessionCase` (auto-init on empty repo)

**When to run**: On every change to TUI rendering, command catalog, or slash-command dispatch.

---

### 3. `--integration-only`

**Purpose**: Integration-path checks that exercise the CLI (not TUI session)
with real process execution. Some cases use Pi-backed paths with mock LLM
fixtures; one case uses a real TUI session for slash suggestions.

**Requires Pi real**: No (uses mock fixtures for LLM paths; slash-suggestion
session does not call Pi).
**Uses mocks**: Yes — mock `pi` scripts for LLM proposal/validation paths.
**Covers**:
- `rfs intent --llm` CLI harness
- `rfs agent` CLI dispatcher via mock `node` on PATH
- `rfs agent --record` CLI dispatcher + RCK record path via mock `node` on PATH
- `rfs trace-slice-proposal-llm` (valid, invalid-json, invalid-shape, contaminated)
- `rfs trace-slice-validate-llm` (valid, unsafe-policy rejection)
- TUI slash-command suggestion filtering (`/he` → `/help`, `/xyz` → unknown)

**When to run**: Before pushing changes that affect CLI dispatch, LLM proposal
paths, or slash-command discoverability.

---

### 4. `--long-paste-only`

**Purpose**: Guards the long-paste capture path. Ensures that large pasted
prompts (≥1200 chars) bypass the burst reader and are captured correctly.

**Requires Pi real**: No.
**Uses mocks**: No (uses simulated TUI sessions with long input).
**Covers**: Paste capture with prompts exceeding the burst threshold.

**When to run**: On changes to `RfsTuiInputReader`, paste capture, or burst
logic. Optional in normal dev cycles — can be run before merging.

---

### 5. `--legacy-only`

**Purpose**: Full TUI session checks with mode selection and recording. These
are the heaviest parser checks — each creates a temp git repo, initializes RFS,
mocks Pi, runs the TUI with redirected multi-line input, and asserts stdout
fragments + RCK counts.

**Requires Pi real**: No (all modes use mocks — see below).
**Uses mocks**: Yes — stateful mock `pi` bash scripts.
**Covers**:
- Simple mode (`2`): mock Pi returns `"Simple mode works."` → State+Delta recorded
- Complete mode (`3`): stateful 4-call mock covering intent inference, anchor
  selection, conversational memory, and main LLM → State+Delta recorded
- Plan mode (`4`): mock Pi returns plan text → State+Delta recorded
- Invalid mode rejection: `abc` → "Invalid mode" → `/cancel` → "Prompt cancelled."
- Mode exit: `/exit` from mode selection → shows mode menu, exits cleanly
- Internal commands polish: `/status`, `/log`, `/model`, `/context`, `/trace`,
  `/model <m>`, `/help` in a single session → all produce expected output

**Mock details**:
- Simple mode: single-call bash script → `{"type":"session"}\n{"type":"message_end",...}`
- Plan mode: single-call bash script (identical pattern to Simple)
- Complete mode: stateful bash script with counter file → 4 sequential calls

The Complete mode mock was added in `6f303e0f`. Previously this test used real
Pi and was non-deterministic.

**When to run**: Before any commit that touches TUI mode selection, mode
recording, or RCK interaction recording. Required before merging.

---

### 6. Default (no flags)

Runs `--core-only` + `--tui-only`. This is the fast default path for everyday
development.

```bash
dotnet run --project tools/rfs/tests/Rufus.Cli.ParserChecks/Rufus.Cli.ParserChecks.csproj
```

---

## Redirected input note

Tests that send multi-line input to the TUI (all `--legacy-only` tests and some
`--tui-only` / `--integration-only` tests) rely on `RfsTuiInputReader` treating
redirected stdin as a plain pipe — one line per `ReadLine()` call.

The burst paste capture (`TryReadRedirectedBurst`) is gated behind
`RfsTuiTerminal.IsInteractive`, which is `false` when stdin is redirected (or
when `RFS_TUI_PLAIN=1` is set). This was fixed in `2486337a`.

No test should depend on the burst reader capturing multi-line input as a single
block.

---

## Mock Pi policy

Tests that exercise Pi-backed paths must mock Pi. The mock pattern is:

1. Create a bash script at `$tempRoot/pi`
2. Make it executable (`chmod +x`)
3. Prepend `$tempRoot` to `PATH`
4. The script emits JSONL on stdout matching the `PiJsonEventRunner` contract
5. For multi-call pipelines (Complete mode), use a counter file

Tests that require real Pi (auth, model availability, network) belong in
external smoke suites (e.g., `ChessBoardApp` validation), not in parser checks.

## Pi real usage

No `Rufus.Cli.ParserChecks` suite requires Pi real as of this baseline.
`--legacy-only` was the last suite to be converted (6f303e0f).

Real-Pi smoke belongs in external validation (e.g., `lrfs` in ChessBoardApp).

## Test segregation policy

- **Parser / unit**: deterministic, no Pi, no network. `--core-only`, `--tui-only`.
- **Integration**: exercises CLI surface, may use mocks. `--integration-only`.
- **Legacy / session**: full TUI sessions with mocked Pi. `--legacy-only`.
- **Long paste**: guards the paste-capture threshold. `--long-paste-only`.
- **External smoke**: real Pi, real repos. Not in this project.
