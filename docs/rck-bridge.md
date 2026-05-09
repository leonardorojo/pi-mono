# RCK Bridge

## Overview

RCK Bridge is the local PI extension that connects PI/RufusChat session actions to a minimal RCK storage layer.

It sits between the Pi session UX layer and `.pi/rck/` so that:
- Pi can keep writing session-facing custom entries/messages for UX and traceability
- RCK can persist operational artifacts locally as the source of truth for the bridge
- future RCK tooling can consume those artifacts without depending on Hermes real execution yet

What it does *not* do yet:
- it does not run Hermes for real
- it does not call an external RCK CLI
- it does not write anchors, handoffs, or retention policy data yet
- it does not implement full locking or concurrency control
- it does not make `.pi/rck/` durable beyond the current local bridge v0.1 rules

## Commands

### `/state`

Creates a new operational state snapshot.

What it does:
- builds a state payload from the current Pi/RCK context
- writes a local RCK state artifact
- writes a matching RCK event artifact
- updates the latest-state index
- appends a Pi custom entry for UX/session traceability

What it writes in Pi:
- a custom entry describing that a state snapshot was created
- a short status-style message for the session

What it writes in `.pi/rck/`:
- `states/<timestamp>_state_<id>.json`
- `events/<timestamp>_evt_<id>.json`
- `indexes/latest-state.json`

Still mock:
- the payload is still a bridge-level mock state snapshot
- no real Hermes execution happens

### `/rck inject`

Reads the latest RCK state and creates a safe context pack.

What it does:
- reads the latest state index
- synthesizes a safe context pack from that state
- writes a context-pack artifact
- writes a matching RCK event artifact
- updates the latest-context-pack index
- appends a Pi custom entry and a safe message for UX/session traceability

What it writes in Pi:
- a custom entry describing that context was injected
- a safe summary message for the session

What it writes in `.pi/rck/`:
- `context-packs/<timestamp>_pack_<id>.json`
- `events/<timestamp>_evt_<id>.json`
- `indexes/latest-context-pack.json`

Still mock:
- the injected context is a safe summary, not raw operational evidence
- no real Hermes execution happens

### `/rck anchor <name>`

Registers a formal semantic anchor for the current bridge context.

What it does:
- creates a formal anchor artifact in `.pi/rck/anchors/`
- writes an `AnchorRegistered` event in `.pi/rck/events/`
- updates `indexes/latest-anchor.json`
- appends a Pi custom entry for UX/session traceability
- emits a visible safe message for the session

What it writes in Pi:
- a custom entry describing that the anchor was registered
- a safe status message that references the anchor name

What it writes in `.pi/rck/`:
- `anchors/<timestamp>_anchor_<id>.json`
- `events/<timestamp>_evt_<id>.json`
- `indexes/latest-anchor.json`

Behavior with latest state:
- if `indexes/latest-state.json` exists, the anchor links to the current state
- the anchor payload includes `stateId` and `statePath`
- the event correlation references the latest state event

Behavior without latest state:
- the anchor still registers successfully
- `stateId` and `statePath` stay absent
- no context-pack is created
- the anchor remains a semantic reference only

Still mock:
- `/rck anchor` is a bridge-level RCK operation, not a real Hermes run
- no real Hermes execution happens

### `/hermes <prompt>`

Records a fake-first Hermes request and a fake Hermes result.

What it does:
- captures the prompt as a Hermes request event
- routes execution through the pure `rck-hermes.ts` contract
- records a Hermes completion event
- appends Pi custom entries for traceability
- persists fake `stdout`/`stderr` as local evidence refs

What it writes in Pi:
- custom entries describing the Hermes request/result
- safe summary text only; no raw execution output

What it writes in `.pi/rck/`:
- `events/<timestamp>_evt_<id>.json`
- `evidence/hermes/stdout/`
- `evidence/hermes/stderr/`
- evidence refs for the stored fake output

Still mock / fake-first:
- Hermes is not executed for real yet
- `--mode real` is blocked by default
- real mode requires `RCK_BRIDGE_ALLOW_REAL_HERMES=1`
- this command remains a bridge placeholder until a later explicit phase enables real execution

## Storage layout

```text
.pi/rck/
  events/
  states/
  context-packs/
  anchors/
  evidence/
    hermes/
      stdout/
      stderr/
  indexes/
    latest-state.json
    latest-context-pack.json
    latest-anchor.json
```

Notes:
- `events/` stores RCK event records
- `states/` stores state snapshots
- `context-packs/` stores safe injection payloads
- `evidence/hermes/stdout/` stores fake Hermes stdout artifacts as refs
- `evidence/hermes/stderr/` stores fake Hermes stderr artifacts as refs
- `indexes/` stores the latest pointers for fast lookup

## Validation: test harness

Canonical test commands:

```bash
npm exec vitest run .pi/extensions/rck-bridge/rck-hermes.test.ts
```

```bash
cd packages/coding-agent
npm exec vitest run test/suite/rck-bridge-commands.test.ts
cd ../..
```

These validate the pure Hermes contract and the bridge command contract in the test harness.

## Validation: RPC JSONL

Validated RPC command sequence:

```bash
printf '%s\n' \
  '{"id":"1","type":"get_state"}' \
  '{"id":"2","type":"get_commands"}' \
  '{"id":"3","type":"prompt","message":"/state"}' \
  '{"id":"4","type":"prompt","message":"/rck inject"}' \
  '{"id":"5","type":"prompt","message":"/hermes inspect mock bridge"}' \
  '{"id":"6","type":"prompt","message":"/rck anchor fase-3b-anchor-test"}' \
| timeout 20 ./pi-test.sh --offline --mode rpc --no-tools --no-extensions --extension .pi/extensions/rck-bridge/index.ts
```

This confirms the bridge works in a minimal RPC runtime with explicit extension loading.

## Important RPC note

Use `--no-extensions` and load `rck-bridge` explicitly.

Do *not* rely on auto-discovery of `.pi/extensions/*` for this validation.

Reason:
- other local extensions can emit `extension_ui_request`
- `prompt-url-widget.ts` is especially relevant because it can emit `setWidget`
- that can pollute or block RPC bootstrap before the bridge is exercised

Recommended shape:
- `--no-extensions`
- `--extension .pi/extensions/rck-bridge/index.ts`

## Safety / context rules

- raw `stdout`, `stderr`, diffs, and logs never enter the LLM directly
- for `/hermes`, raw `stdout`/`stderr` are persisted only as evidence refs
- only `safeSummary` plus refs are eligible for `custom_message`
- `allowedToInject = true` is required for `/rck inject`
- raw operational evidence stays out of the injected prompt by design
- anchors are semantic references, not context packs
- anchors do not inject raw content into the LLM

## Current limits

- `/hermes` is fake-first and still blocked from real execution by default
- Hermes real is not connected yet
- `ctx.exec` is not used yet
- no external RCK CLI yet
- no Codex usage
- real mode requires a later explicit phase and the `RCK_BRIDGE_ALLOW_REAL_HERMES=1` env gate
- storage v0.1 has no locks or retention policy
- `piEntryId` is optional and may be incomplete during early correlation
- Pi visual labels are not integrated yet
- `anchor-index.json` is not implemented yet; `latest-anchor.json` is the only anchor index

## Cleanup

Remove generated local RCK artifacts when you are done validating:

```bash
rm -rf .pi/rck
```

## Next steps

Potential follow-ups for later phases:
- minimal RPC client that can drive this bridge automatically
- RCK anchors for stronger lineage and evidence references
- Hermes real integration when the mock bridge is ready to graduate
