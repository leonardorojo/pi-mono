# Rufus CLI (`rfs`) POC

`rfs` is a small C#/.NET proof of concept for a Rufus CLI inside `pi-mono`.
It is intentionally still a POC, not a finished product.

## Current shape

Implemented commands:

- `rfs help`
- `rfs --version`
- `rfs init`
- `rfs status`
- `rfs log`
- `rfs pi [message]`
- `rfs ask <prompt>`
- `rfs ask --record <prompt>`
- `rfs agent <task>`
- `rfs agent --record <task>`

High-level behavior:

- `rfs init` initializes the local Rufus workspace and seeds the local RCK DAG with a genesis State and Anchor
- `rfs ask` is headless prompt execution through Pi's auth/provider/model stack
- `rfs ask --record` records the ask interaction into local RCK as State + Delta
- `rfs agent` is the headless streaming agent path
- `rfs agent --record` records the streamed agent interaction into local RCK as State + Delta
- `rfs status` is read-only and reports workspace, RCK, and Git context
- `rfs log` is read-only and walks the active RCK chain from `.rfs/rck/HEAD` backward through reachable Deltas

`rfs` is still a POC. The higher-level RCK workspace layer owns `.rfs/` layout, local persistence, Git context capture, and status reporting.

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
It uses the Node helper at `tools/rfs/bridge/rfs-ask.mjs` to talk to Pi's AI layer without opening the Pi TUI.

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

## Current limitations

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
