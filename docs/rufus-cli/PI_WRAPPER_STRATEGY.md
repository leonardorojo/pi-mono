# Pi Wrapper Strategy

Pi can serve as a tactical agentic backend behind Rufus CLI when the workflow benefits from it.

## What Pi contributes

- filesystem tools
- shell / bash execution
- read / edit capabilities
- sessions and persistence
- skills / extensions
- the agent loop

## What Rufus contributes

- operational judgment
- workflows
- gates
- contracts
- RCK relationship management
- coordination with Hermes and Codex

## Strategy

Rufus CLI should be able to invoke Pi as a tactical backend, but it should not become structurally dependent on Pi.

The wrapper must remain replaceable so that the underlying engine can change without rewriting Rufus CLI itself.

## Conceptual entry points

- `rfs inspect`
- `rfs plan`
- `rfs verify`
- `rfs pi run`
- `rfs rck export-trace`

These are design markers, not implementation commitments.