# PI/RufusChat + RCK Bridge POC Status

## Related ADR
- `docs/adr/ADR-0001-pi-rufuschat-rck-orchestrator.md`

## Mock extension
- `.pi/extensions/rck-bridge/index.ts`

## Mock commands
- `/state`
- `/rck inject`
- `/hermes`

## Validation status
- RPC mode validated
- `/state` validated
- `/rck inject` validated
- `/hermes` validated
- `HermesRunRequested` visible in RPC stdout
- `HermesRunRecorded` visible in RPC stdout

## Scope and constraints
- No Hermes real
- No RCK real
- No Codex
- No core changes
- `web-ui` was out of scope for this POC

## Decision
- Do not depend on the TUI for handler validation
- Use RPC for controlled, non-interactive validation

## Next phase
- Formalize a TypeScript RCK v0.1 contract
- Refactor mock events to use stronger types
- Then design the real Hermes integration
