# Rufus CLI (`rfs`) POC

`rfs` is a minimal C#/.NET proof of concept for a Rufus CLI inside `pi-mono`.

At this stage, `rfs` is not a full agent and does not implement Rufus governance yet. It only proves that a C# CLI can act as an entry point to Pi.

## Current scope

Implemented:

- `rfs --version`
- `rfs pi "message"`

Not implemented yet:

- `.rufus/` workspace
- configuration files
- engine adapters
- RCK integration
- Codex/Hermes integration
- packaging as a `dotnet tool`
- non-interactive execution mode

## Build

From the repository root:

```bash
dotnet build tools/rfs/Rufus.Cli.sln
```

## Version

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- --version
```

Expected output:

```text
rfs 0.0.1-poc
```

## Run Pi through rfs

```bash
dotnet run --project tools/rfs/src/Rufus.Cli -- pi "hello from rfs"
```

This launches Pi as an external process and passes the initial message to it.

## Interactive passthrough behavior

`rfs pi` is currently an interactive passthrough command.

Conceptually:

```text
shell -> dotnet run -> Rufus.Cli -> pi
```

instead of:

```text
shell -> pi
```

Once Pi starts, Pi owns the terminal UI.

This means:

- stdin/stdout/stderr are not captured by `rfs`
- Pi runs in the foreground
- the user can continue interacting with Pi normally
- when Pi exits, control returns to `rfs` and then to the shell
- `rfs` returns Pi's exit code

## Validation note

Because Pi is a TUI/interactive application, `rfs pi` should be validated in a foreground terminal/PTY.

Do not validate this mode by capturing stdout/stderr in background, because that changes the terminal behavior and may make Pi appear stuck.

## Current limitation

There is no separate non-interactive execution mode yet.

A future command could distinguish between:

```bash
rfs pi "message"
```

for interactive passthrough, and something like:

```bash
rfs pi exec "message"
```

for capturable/automation-friendly execution.

This is intentionally not implemented in this POC.

## Design principle

```text
Rufus does not have to be Pi.
Rufus can use Pi when useful.
```
