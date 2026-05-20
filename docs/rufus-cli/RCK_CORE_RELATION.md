# RCK Core Relation

RCK Core is a source of cognitive traceability, not an agentic runtime.

## Relationship

Rufus CLI can consume and produce RCK artifacts through CLI commands, contracts, and files, without premature direct coupling.

## Core concepts

- Trace
- TraceSlice
- Anchor
- ContextPack
- Evidence
- payloads

## Intended use

Rufus CLI can coordinate:

- context extraction
- trace generation
- anchor creation
- Evidence verification

## Separation model

- Pi executes.
- Rufus governs.
- RCK remembers.
- Hermes coordinates.
- Codex implements when it is the right tool.

This keeps RCK Core as an observable memory layer while Rufus CLI remains the operational control plane.