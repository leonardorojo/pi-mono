# RufusChat product path

Official product path: `apps/rufuschat-ui`

Legacy prototype path: `scripts/rufuschat-ui-server.mjs`

## Why the prototype is frozen

- It validated the primary RCK/Hermes interactions.
- It served as proof of operation during the prototype phases.
- It was never intended to be the final UX.

## What can still change in the prototype

- Critical bugfixes
- Regression comparison work
- Temporary diagnostics

## What must not be added to the prototype

- New product features
- Chat UX
- Project/chat memory
- RCK Core integration
- LLM integration

## Future development rule

- Product UX features go to `apps/rufuschat-ui`.
- RCK provider integration goes through extension/provider contracts.
