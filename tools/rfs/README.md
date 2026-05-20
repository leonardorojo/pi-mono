# Rufus CLI (`rfs`) POC

`rfs` is a small C#/.NET proof of concept for a Rufus CLI inside `pi-mono`.
It is intentionally still a POC, not a finished product.

Current shape:

- `rfs pi` opens Pi interactively without an initial prompt and passes through to the Pi TUI
- `rfs ask` asks the LLM headlessly through Pi's auth/provider/model stack
- `rfs agent` runs a read-only headless agent with tools and streamed events

Rufus no ES Pi.
Rufus USA Pi cuando conviene.

## What exists today

Implemented commands:

- `rfs --version`
- `rfs pi [message]`
- `rfs ask <prompt>`
- `rfs agent <task>`
- `rfs agent --raw <task>`

The implementation lives under `tools/rfs/`:

- `tools/rfs/Rufus.Cli.sln`
- `tools/rfs/src/Rufus.Cli/Program.cs`
- `tools/rfs/bridge/rfs-ask.mjs`
- `tools/rfs/bridge/rfs-agent.mjs`
- `tools/rfs/src/Rufus.Cli/Rufus.Cli.csproj`

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

## Test `pi`

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- pi
```

`rfs pi` is an interactive passthrough to Pi.
Pi owns the terminal once it starts, so this mode should be validated in a foreground terminal or PTY.

If you pass a message, it is forwarded to Pi:

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- pi "hello from rfs"
```

Validation note:

- do not try to prove this mode by background-capturing stdout/stderr
- the expected behavior is interactive terminal ownership, not captured text output

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

That means `rfs` is not inventing its own agent stack here; it is leaning on Pi's configured runtime.

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
- `rfs agent --raw` forwards the helper output unchanged

Current tools:

- `list_directory`
- `read_file`

Streaming markers you should see:

- `[agent:start]`
- `[tool:start]`
- `[tool:end]`
- `[assistant]`
- `[agent:end]`

Security and confinement:

- read-only
- repo-root confined
- no file edits
- no access outside the checkout

## Difference between `pi`, `ask`, and `agent`

```text
rfs pi
  opens Pi interactively

rfs ask
  asks the LLM headlessly through Pi's auth/provider stack

rfs agent
  runs a headless read-only agent with tools and streaming
```

- `pi` is for interactive use.
- `ask` is for one-shot text answers.
- `agent` is for repository inspection and evidence gathering.

## Current limitations

This is still a minimal POC.

Not present yet:

- `.rufus/` workspace
- persisted sessions
- multi-turn history
- RCK integration
- formal Hermes/Codex integration
- packaging as a `dotnet tool`
- no writes / no editing from `rfs agent`
- no JSONL event format yet
- no explicit model selection from `rfs agent` yet
- production-grade command routing or lifecycle management

`ask` is only as good as the Pi configuration underneath it.
If Pi is not configured or authenticated, `ask` will fail the same way Pi would.

`agent` is also only as good as the Pi configuration underneath it.
If Pi is not configured or authenticated, `agent` will fail the same way Pi would.

## Conceptual boundary

```text
Rufus does not have to be Pi.
Rufus can use Pi when useful.
```

That is the current design center for this POC.
