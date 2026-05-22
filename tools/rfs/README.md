# Rufus CLI (`rfs`) POC

`rfs` is a small C#/.NET proof of concept for a Rufus CLI inside `pi-mono`.
It is intentionally still a POC, not a finished product.

## Current shape

## Command catalog

The current CLI surface is grouped below by category.
Each command lists: what it does, whether it writes `.rfs/rck`, whether it is read-only, and whether it depends on Pi / JSONL / RPC / legacy behavior.

### General

- `rfs help`
  - Description: prints the current command surface.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: none.

- `rfs --version`
  - Description: prints the current `rfs` version string.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: none.

### Workspace / RCK

- `rfs init`
  - Description: initializes the local Rufus workspace and seeds the local RCK DAG with a genesis State and Anchor.
  - Writes RCK: yes.
  - Read-only: no.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: none.

- `rfs status`
  - Description: reports workspace, RCK, and Git context.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: none.

- `rfs log`
  - Description: walks the active RCK chain from `.rfs/rck/HEAD` backward through reachable Deltas.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: none.

- `rfs context-pack`
  - Description: exports the full active RCK DAG as JSON.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: none.

- `rfs context-pack --trace-slice "<prompt>"`
  - Description: materializes a focused JSON context-pack from deterministic TraceSlice v0 selection + materialization policy.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: none.

- `rfs trace-slice "<prompt>"`
  - Description: builds a deterministic read-only JSON slice from the active RCK chain.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: none.

- `rfs trace-slice-proposal "<prompt>"`
  - Description: emits a deterministic, anchor-aware, non-authoritative `TraceSliceProposal` JSON built from prompt + inferred intent + DAG quick index.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: yes.
  - Pi / JSONL / RPC / legacy: none.

- `rfs trace-slice-validate "<prompt>"`
  - Description: runs the same deterministic proposal pipeline internally, validates requested selection + materialization policy, and emits a validated `rufus.trace-slice` JSON document with a `validation` block.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: yes.
  - Pi / JSONL / RPC / legacy: none.

- `rfs context-pack --trace-slice-validated "<prompt>"`
  - Description: materializes a focused JSON context-pack from a validated TraceSlice produced by the deterministic intent -> proposal -> validation pipeline.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: yes.
  - Pi / JSONL / RPC / legacy: none.

### Models

- `rfs model get`
  - Description: reads the workspace default LLM model from `.rfs/config.json` when configured.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: none.

- `rfs model set <model>`
  - Description: stores the workspace default LLM model in `.rfs/config.json`.
  - Writes RCK: no.
  - Read-only: no.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: none.

- `rfs model list`
  - Description: queries the currently available models without opening the Pi TUI.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: Pi RPC.

### Ask

- `rfs ask <prompt>`
  - Description: executes a headless prompt through Pi auth/provider/model using Pi JSON Event Stream by default.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: Pi + JSONL by default; optional legacy fallback via `RFS_USE_LEGACY_ASK_BRIDGE=1`.

- `rfs ask --record <prompt>`
  - Description: executes the same headless ask flow and records the interaction into local RCK.
  - Writes RCK: yes.
  - Read-only: no.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: Pi + JSONL by default; optional legacy fallback via `RFS_USE_LEGACY_ASK_BRIDGE=1`.

- `rfs ask-json <prompt>`
  - Description: experimental one-shot prototype that runs `pi --mode json`, parses stdout as JSONL, and prints a human answer.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: yes / diagnostic.
  - Pi / JSONL / RPC / legacy: Pi + JSONL.

### Agenting

- `rfs intent <prompt>`
  - Description: runs `Rufus.Agenting.Intent.IntentInferenceAgent` with a deterministic `AgentTask` (`Kind = infer-intent`) and prints the result.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: none.

- `rfs intent --record <prompt>`
  - Description: runs the same deterministic intent inference path and records the controlled result into local RCK.
  - Writes RCK: yes.
  - Read-only: no.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: none.

- `rfs agent-json <task>`
  - Description: experimental JSON Event Stream agent path that runs `pi --mode json`, captures observed events, and prints a human summary.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: yes.
  - Pi / JSONL / RPC / legacy: Pi + JSONL; relies on Pi `--tools` enforcement.

- `rfs agent <task>`
  - Description: headless streaming agent path for a task.
  - Writes RCK: no.
  - Read-only: yes.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: legacy Node helper.

- `rfs agent --record <task>`
  - Description: runs the same headless streaming agent path and records the interaction into local RCK as State + Delta.
  - Writes RCK: yes.
  - Read-only: no.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: legacy Node helper.

### Pi

- `rfs pi [message]`
  - Description: interactive passthrough to the Pi TUI.
  - Writes RCK: no.
  - Read-only: no.
  - Experimental: no.
  - Pi / JSONL / RPC / legacy: Pi interactive passthrough.

High-level behavior:

- `rfs ask` and `rfs agent` use the workspace default model when one is configured; otherwise they keep using the current Pi/RFS default
- `rfs ask-json` also reads `.rfs/config.json` and prefers `--model provider/id` when the configured model includes a provider prefix; otherwise it falls back to `RUFUSCHAT_LLM_MODEL` for bare model ids
- `rfs ask` can temporarily fall back to the legacy bridge with `RFS_USE_LEGACY_ASK_BRIDGE=1`
- `rfs trace-slice` stays the deterministic authoritative baseline selection path
- `rfs trace-slice-proposal` is intentionally non-authoritative: the agent proposes, RFS validates later, and the command does not materialize a ContextPack or write `.rfs/rck`
- `rfs trace-slice-validate` runs the deterministic proposal pipeline plus runtime validation and emits the authoritative validated TraceSlice without writing `.rfs/rck`
- `rfs context-pack --trace-slice-validated` materializes a scoped ContextPack from that validated TraceSlice without writing `.rfs/rck`

`rfs` is still a POC. The higher-level RCK workspace layer owns `.rfs/` layout, local persistence, Git context capture, and status reporting.

## Agent / Task abstraction

`Rufus.Agenting` defines the operational agent/task layer used by RFS.
`Rufus.RCK.Core` stays focused on the persistent cognitive model: State, Delta, Anchor, DAG, Trace, and other traceable RCK models.
Detailed reference: [`docs/RUFUS_AGENTING.md`](docs/RUFUS_AGENTING.md).

- An `IAgent` executes a single `AgentTask`.
- Each `Agent` has a fixed `AgentExecutionModel` with provider + model baked into the descriptor.
- There is no `ModelRouter` and no runtime model selection.
- `AgentTaskResult` records `AgentId`, `ExecutionModel`, `Output`, `Summary`, `Evidence`, `Warnings`, and `Errors`.
- `IntentInferenceAgent` is the first mock example.
- It accepts `Kind = infer-intent` and returns a deterministic `PromptIntent` JSON payload.
- RFS can run agents and later persist or project their results into RCK, but RCK Core does not execute agents.
- This is the base for future task kinds such as TraceSlice, ContextPack, diff inspection, and evidence summaries.

## Model config

`.rfs/config.json` can persist the workspace default model under `llm.defaultModel`.

Example:

```json
{
  "schemaVersion": 1,
  "llm": {
    "defaultModel": "gpt-5.4-mini"
  }
}
```

Commands:

```bash
rfs model get
rfs model set gpt-5.4-mini
rfs model list
```

If you are using the local wrapper from another repository, the same commands work there too:

```bash
cd /home/rufus/DEV/leonardorojo/ChessBoardApp
lrfs model get
lrfs model list
lrfs model set gpt-5.4-mini
lrfs model list
lrfs ask-json "Respond with one short sentence confirming JSON mode works."
lrfs ask "Respond with a short sentence confirming RFS model config is being used."
```

`rfs model list` uses `pi --mode rpc --no-session` with a single `get_available_models` request.
RFS disables extensions and context files for this RPC call so stdout stays dedicated to JSONL protocol traffic.
The command prints provider + model id, includes the display name when Pi returns one, and marks the current workspace model with `*` when it matches `.rfs/config.json`.

`rfs ask-json` is an experimental validation path for Pi JSON Event Stream mode.
It runs `pi --mode json --no-session --no-tools --no-extensions --no-context-files <prompt>` from the caller cwd, keeps stderr separate, parses stdout line-by-line as JSONL, accumulates `message_update.assistantMessageEvent.type == "text_delta"`, and prefers the structured final assistant text from `message_end`, `turn_end`, or `agent_end` when present.
It does not modify `.rfs/rck`, does not replace `rfs ask`, and does not touch the legacy Node bridges.

New experimental command (P7 prototype):

- `rfs agent-json <task>`: prototype JSON Event Stream agent. This runs `pi --mode json` with a restricted read-only `--tools` list (`read,grep,find,ls`), parses JSONL events, captures observed tool_execution_* events, and prints a concise human-friendly summary. This command is explicitly experimental at runtime and in documentation. Each execution prints: `Experimental: relies on Pi --tools enforcement for read-only behavior.` See tools/rfs/docs/RFS_PI_PROGRAMMATIC_INTEGRATION_AUDIT.md for audit notes.
- `rfs intent <prompt>`: minimal CLI harness for `Rufus.Agenting.Intent.IntentInferenceAgent`. It creates an `AgentTask` with `Kind = infer-intent`, `Goal = inferir intent operativo del prompt`, and `Input = <prompt>`, then prints `Status`, `AgentId`, `ExecutionModel`, `Summary`, `Output`, `Evidence`, `Warnings`, and `Errors` when present. It does not call Pi and does not write `.rfs/rck`.

Example:

```text
Available models:

* gpt-5.4-mini - GPT-5.4 Mini  github-copilot
  claude-haiku-4.5 - Claude Haiku 4.5  github-copilot

Current workspace model:
  gpt-5.4-mini
```

This workspace default is the base for future subagent-specific routing, but that routing is not implemented yet.

Rufus no ES Pi.
Rufus USA Pi cuando conviene.

## RCK workspace

`.rfs/` is the local Rufus workspace.
`.rfs/rck/` contains the local cognitive DAG workspace.

Workspace layout:

```text
.rfs/
  config.json
  rck/
    HEAD
    states/
    deltas/
    anchors/
```

Meaning:

- `.rfs/` is workspace-local Rufus state and is ignored by Git
- `.rfs/config.json` stores local workspace configuration
- `.rfs/rck/HEAD` points to the current State id
- `.rfs/rck/HEAD` defines the active cognitive chain
- `rfs log` starts from `HEAD`, follows incoming Deltas backward, and ignores orphan objects not reachable from `HEAD`
- future log/navigation commands should start from `HEAD`
- extra State/Delta JSON files do not necessarily belong to the active chain
- orphan objects may exist during development/testing and should be handled by future validation/log tooling
- `.rfs/rck/states/` stores State JSON files
- `.rfs/rck/deltas/` stores Delta JSON files
- `.rfs/rck/anchors/` stores Anchor JSON files

## Build

From the repository root:

```bash
dotnet build tools/rfs/Rufus.Cli.sln
```

## Test `--version`

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- --version
```

This should print the current `rfs` version string.

## Test `help`

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- help
dotnet run --project tools/rfs/src/Rufus.Cli -- --help
dotnet run --project tools/rfs/src/Rufus.Cli -- -h
```

`rfs help` shows the available rfs commands.
The `--help` and `-h` aliases should show the same command surface.

## Test `init`

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- init
dotnet run --project tools/rfs/src/Rufus.Cli -- init
```

`rfs init` initializes `.rfs/` in the current repository.
It looks upward from the current directory for the repo root by finding `.git`, then creates the workspace there.

What it creates:

- `.rfs/config.json`
- `.rfs/rck/HEAD`
- `.rfs/rck/states/`
- `.rfs/rck/deltas/`
- `.rfs/rck/anchors/`
- a genesis State
- a genesis Anchor

Behavior:

- idempotent
- if `.rfs/` already exists, it does not fail
- if `.rfs/config.json` already exists, it does not overwrite it
- safe to run multiple times

## Test `ask`

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- ask "Respond in one short sentence: what is Rufus CLI?"
```

`rfs ask` is headless.
It uses Pi JSON Event Stream by default and keeps the output human-readable.
Set `RFS_USE_LEGACY_ASK_BRIDGE=1` to force the legacy Node bridge temporarily.

`ask` reuses Pi's existing auth/provider/model setup:

- `~/.pi/agent/settings.json`
- `~/.pi/agent/auth.json`
- Pi's configured provider/model
- Pi's streaming AI layer

## Test `ask --record`

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- ask --record "Respond in one short sentence: what is RCK?"
```

`rfs ask --record` executes the headless ask flow and records the interaction into local RCK.
It now uses Pi JSON Event Stream by default and keeps the legacy Node bridge only when `RFS_USE_LEGACY_ASK_BRIDGE=1` is set.
When the worktree has changed files, the recording captures `artifacts` for `ask --record` and `agent --record`.

Recording shape:

- previous State + Delta -> next State
- updates `.rfs/rck/HEAD`
- captures the Git context in the State payload as material context
- State payload stores `answerSummary`
- Delta cause stores the full answer
- does not record anything unless `--record` is present

Minimum payload shape includes:

- mode
- prompt
- answerSummary in State
- answer in Delta cause
- git context
- artifacts []

## Changed artifact paths

RCK records a minimal artifact footprint when the worktree has changes.

- RCK does not store diffs.
- RCK does not store file contents.
- RCK does not store artifact hashes yet.
- Git keeps the full content and the diffs.
- RCK stores only a minimal trace of changed artifacts.
- The source of truth is `git status --porcelain`.
- `.rfs/` is excluded and must not contaminate the recording.

Each artifact entry includes, at minimum:

- `kind = file`
- `path`
- `changeType`
- `gitStatus`
- `source = git-status`

State payload:

- `artifacts` can include real changed paths.
- `answerSummary` stays compact; the full answer lives in Delta cause.

Delta payload:

- `evidence.artifacts` can include real changed paths.
- `change.changes` includes `/artifacts` when artifacts are detected.

Conceptual example:

```json
{
  "kind": "file",
  "path": "tools/rfs/README.md",
  "changeType": "modified",
  "gitStatus": " M",
  "source": "git-status"
}
```

## Test `agent`

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- agent "inspect tools/rfs"
```

`rfs agent <task>` is the headless agent POC.
It uses the Node helper at `tools/rfs/bridge/rfs-agent.mjs`.

Behavior:

- read-only tools only
- confined to the repository root
- no writes
- no Pi TUI
- streamed events during execution
- `rfs agent` renders a human-friendly summary by default
- the human render shows a cabecera with `Rufus Agent`, `Task`, `Mode`, `Scope`, `Actions`, and `Answer`
- `Answer` is lightly formatted for human readability

Current tools:

- `list_directory`
- `read_file`

Implementation note:

- `toolExecution` is currently sequential to keep the POC output legible

## Test `agent --record`

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- agent --record "inspect tools/rfs"
```

`rfs agent --record` executes the headless streaming agent and records the interaction into local RCK.
When the worktree has changed files, the recording captures `artifacts` for `agent --record` and `ask --record`.

Recording behavior:

- records a full interaction as State + Delta
- captures basic tools from the streamed events
- Agent tool calls are recorded in Delta `evidence.tools`.
- Tools are not currently stored in the State payload.
- State only stores `answerSummary`, not the full answer or tool list.
- captures changed artifacts when the worktree has changes
- updates `.rfs/rck/HEAD`
- does not record anything unless `--record` is present

## Anchor `git-commit:<hash>`

When a recorded interaction detects that the current Git commit differs from the commit stored in the previous State, `rfs` creates an Anchor:

- `git-commit:<short-hash>`

Important boundary:

- `rfs` does not assume it created the commit
- it only detects that Git HEAD changed between recorded States
- the Git commit is stored in the State payload as material context

## Test `status`

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- status
```

`rfs status` is read-only.
It reports:

- initialized
- root
- HEAD
- states count
- deltas count
- anchors count
- Git branch
- Git commit
- dirty

It does not create, modify, or delete any files.

## Test `log`

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- log
```

`rfs log` is read-only.
It shows the active cognitive history starting at `.rfs/rck/HEAD` and walking backward through reachable Deltas.
It ignores orphan State/Delta JSON files that are not reachable from `HEAD`.
It prints a compact summary of each entry, including the interaction mode, prompt excerpt, answer summary, Git commit/dirty state, changed artifacts, Delta id, and `createdAt` / `CreatedBy` when present.

## Test `context-pack`

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- context-pack
```

`rfs context-pack` is read-only.
It exports the active workspace RCK DAG as pure JSON.
JSON is the canonical portable format for this command.
It is intended to be pasted into another LLM or consumed by tools.
Markdown is not emitted by this command.

`rfs context-pack --trace-slice "<prompt>"` is also read-only.
It keeps the full export mode intact, but materializes a focused context-pack using deterministic TraceSlice v0 as the selection plan.
Conceptually, TraceSlice planning is intent-first and should become anchor-aware over time: anchors are cognitive milestones and may be stronger relevance signals than recency alone, even though the current v0 runtime remains a simpler deterministic baseline.
That scoped mode emits pure JSON with `scope = "trace-slice"`, includes the TraceSlice used to produce the pack, filters `states` / `deltas` / `anchors` to the TraceSlice selection, keeps artifacts metadata-only, and preserves the no file contents / no diffs / no stdout-stderr / no JSONL / no RCK writes boundary.

Top-level fields:

- `schemaVersion`
- `type`
- `generatedAtUtc`
- `schema`
- `interpretationRules`
- `quickIndex`
- `workspace`
- `headStateId`
- `headShortId`
- `counts`
- `activeChain`
- `states`
- `deltas`
- `anchors`
- `derivedRelationships`
- `notes`

Content notes:

- `schema` contains a formal JSON Schema document embedded in the payload
- `interpretationRules` defines the DAG reading rules
- `quickIndex` is a compact navigation summary
- `workspace` captures the repository root and git context
- `headStateId` / `headShortId` identify the current HEAD State
- `counts` summarizes DAG sizes and reachability counts
- `activeChain` walks backward from HEAD toward genesis
- `states` includes `payloadDecoded`
- `deltas` includes `decodedValueJson` when the op payload can be parsed
- `anchors` is always present
- `derivedRelationships` is always present
- `notes` explains output limits and interpretation boundaries

Active chain shape:

- ordered from HEAD backward to genesis
- each entry includes:
  - `stateId`
  - `incomingDeltaId`
  - `anchors`
  - `mode`
  - `prompt`
  - `answerSummary`
  - `gitContext`
  - `artifacts`

The `gitContext` shape is explicitly nested:

```json
{
  "gitContext": {
    "branch": "master",
    "commit": "...",
    "dirty": true
  }
}
```

Output notes:

- no Markdown report
- no fenced code blocks in the command output
- no narrative text outside the JSON object
- no file contents
- no git diffs
- no artifact hashes yet
- it can be large
- it does not modify `.rfs/`
- `.rfs/` is internal metadata and is excluded from changed artifact tracking

This v1 is a full DAG export. A future compact context pack can be added later as a separate command or mode.

RCK DAG design principles are documented in `tools/rfs/docs/RCK_DAG_PRINCIPLES.md`.
Main rule: reference or reproduce, do not duplicate.
The context-pack is an export projection, not the storage model.

Still not present:

- `rfs pi` recording
- sessions
- `TraceSlice`
- cognitive branch/merge workflows
- complex artifact hashing
- DAG navigation commands
- automatic recording without `--record`
- production-grade command routing or lifecycle management
- `RckRef` and `EvidenceRef` population in the current recorder
- changed artifacts represented as `RckRef` / `EvidenceRef`
- artifact hashes
- file diffs
- file contents in RCK artifacts

Next candidate command:

- `rfs show <state-id|delta-id|anchor-id>`

`ask` is only as good as the Pi configuration underneath it.
If Pi is not configured or authenticated, `ask` will fail the same way Pi would.

`agent` is also only as good as the Pi configuration underneath it.
If Pi is not configured or authenticated, `agent` will fail the same way Pi would.

## Branch hygiene

- `main` stays clean and aligned with upstream.
- `feature/rufus-cli-design` is the rfs integration branch.
- Short-lived feature branches should be merged back into `feature/rufus-cli-design` and deleted.
- Historical branches such as `rufuschat/dev-history` may be kept as preserved history.
- Local backup branches may be deleted once their commits are confirmed to be contained in a preserved branch.

Safe checks:

```bash
git merge-base --is-ancestor backup/main-before-rufuschat-isolation rufuschat/dev-history && echo "backup contained"
git branch -d backup/main-before-rufuschat-isolation
```

If `git branch -d` fails but you have already verified that the branch is contained in a preserved branch, you may use `git branch -D`.
`git branch -D` should be used only after that verification.

## RCK layering rule

`Rufus.RCK.Core` is the cognitive Git plumbing.
It should stay small and focused on base DAG concepts:

- State
- Delta
- Anchor
- Ref / EvidenceRef
- Hash / canonical JSON
- basic storage interfaces
- DAG validation and navigation

`Rufus.RCK.Core` must not contain product- or workspace-level concerns:

- `rfs init` logic
- Git detection
- LLM interactions
- agent event parsing
- tool-call parsing
- artifact discovery
- sessions
- `TraceSlice`
- recording workflows
- `.rfs/` layout decisions
- CLI formatting
- dependencies on Pi, Node, agents, or `Rufus.Cli`

If higher-level behavior is needed, it should live in a separate layer, preferably `tools/rfs/src/Rufus.RCK.Workspace/`.
That layer owns `.rfs/` layout, workspace persistence, Git context capture, interactivity mapping, and future commit-aware anchors such as `git-commit:<hash>`.

Preferred dependency rule:

```text
Rufus.Cli
  -> Rufus.RCK.Workspace
      -> Rufus.RCK.Core
```

In this design, `Rufus.Cli` should use `Rufus.RCK.Workspace` for workspace logic, `.rfs`, Git context, recording, and local persistence.
`Rufus.RCK.Workspace` should use `Rufus.RCK.Core`.
`Rufus.RCK.Core` must not depend on `Rufus.Cli` or `Rufus.RCK.Workspace`.
Direct `Rufus.Cli -> Rufus.RCK.Core` references should be avoided unless there is an explicit justification.

Prohibited dependencies:

- `Rufus.RCK.Core -> Rufus.Cli`
- `Rufus.RCK.Core -> Rufus.RCK.Workspace`
- `Rufus.RCK.Core -> Pi`
- `Rufus.RCK.Core -> Node`
- `Rufus.RCK.Core -> .rfs layout`
- `Rufus.RCK.Core -> agent runtime`

## State and Delta shape

State = how the cognitive state ends up.
Delta = what changed, what caused it, and what evidence supports it.

State:

- represents the snapshot cognitive result
- in interaction states stores:
  - `interaction.mode`
  - `interaction.prompt`
  - `interaction.answerSummary`
  - `git.branch`
  - `git.commit`
  - `git.dirty`
  - artifacts snapshot
- does not store the full answer
- does not store tools

Delta:

- represents the transition between previous State and next State
- stores:
  - `fromStateId`
  - `toStateId`
  - `PatchOp` over `/interaction`
  - `valueJson` with:
    - `change`
    - `cause`
    - `evidence`
- `cause` contains:
  - `mode`
  - `prompt`
  - full `answer`
- `evidence` contains:
  - tools for agent
  - artifacts when there are changed artifact paths

## Conceptual boundary

```text
Rufus does not have to be Pi.
Rufus can use Pi when useful.
```

That is the current design center for this POC.
