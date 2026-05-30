# RFS Legacy Deprecation Plan

## Purpose

This document governs future deprecations of legacy and experimental RFS command-surface elements.
It does *not* deprecate anything by itself.
It does not change runtime behavior.
It is a decision contract, not an execution plan.

## Current classification

| Element | Status | Current role | Replacement / forward path | Current coverage | Current decision |
| --- | --- | --- | --- | --- | --- |
| `bridge/rfs-ask.mjs` | legacy fallback | Escape hatch for `rfs ask` when `RFS_USE_LEGACY_ASK_BRIDGE=1` is accepted | `rfs ask` via Pi JSON Event Stream | `rfs ask` and `rfs ask --record` E2E coverage exists; bridge-specific external smoke is still pending | Keep |
| `bridge/rfs-agent.mjs` | legacy active | Current Node bridge for `rfs agent` / `rfs agent --record` | `rfs agent-json` after parity is proven | `rfs agent` and `rfs agent --record` dispatcher coverage exists; bridge-specific external smoke is still pending | Keep |
| `rfs ask-json` | experimental diagnostic | JSON transport / parser diagnostic path for ask | `rfs ask` and `rfs ask --record` | Ask-side CLI E2E coverage exists; diagnostic transport still serves validation value | Keep |
| `rfs agent-json` | experimental forward path | Native JSON Event Stream agent candidate | `rfs agent` / `rfs agent --record` | Agent-side CLI coverage exists; `--record` parity and real bridge smoke are still pending | Keep |
| `rfs ask` | active current path | Primary headless ask path | N/A | Ask CLI E2E coverage exists | Keep |
| `rfs ask --record` | active current path | Primary headless ask path plus controlled RCK recording | N/A | Ask record CLI E2E coverage exists | Keep |
| `rfs agent` | legacy active | Current agent execution path | `rfs agent-json` after parity is proven | Agent dispatcher coverage exists | Keep |
| `rfs agent --record` | legacy active | Current agent execution path plus controlled RCK recording | `rfs agent-json --record` only after parity exists | Agent record dispatcher coverage exists | Keep |
| TraceSlice deterministic commands | canonical baseline | Baseline selection / materialization path | N/A | Deterministic build and parser coverage is already in place | Keep |
| TraceSlice LLM commands | experimental diagnostic | Hardening-only proposal/validation path | Deterministic TraceSlice baseline plus validated materialization | Coverage exists for the experimental path; it remains non-canonical | Keep |
| `context-pack --trace-slice-validated` | experimental forward path | Validated-slice materialization path | Deterministic TraceSlice + validated materialization | Coverage exists as part of the validated flow | Keep |

## Definitions

- **active current path**: the command path intended for day-to-day use today.
- **legacy fallback**: a supported escape hatch with narrower guarantees.
- **legacy active**: a legacy path that is still in production use because no replacement has been authorized.
- **experimental diagnostic**: a read-only path used to validate transport, parsing, or hardening behavior.
- **experimental forward path**: a candidate replacement path that is not yet authoritative.
- **canonical baseline**: the deterministic reference surface that defines expected behavior.
- **removable**: eligible for deletion only after the removal gates below are satisfied and a replacement path is authoritative.

## Non-removal rules

- Do not remove `bridge/rfs-agent.mjs` until `agent-json` has functional parity, `--record`, tests, and real smoke coverage.
- Do not remove `bridge/rfs-ask.mjs` while `RFS_USE_LEGACY_ASK_BRIDGE` remains an accepted escape hatch.
- Do not remove TraceSlice deterministic commands; they are the canonical baseline.
- Do not remove TraceSlice LLM commands while they still serve hardening and diagnostic work.
- Do not touch `Rufus.RCK.Core` as part of bridge deprecation.
- Do not remove any command that writes RCK unless an equivalent covered path exists.

## Deprecation phases

- **Phase 3A**: document the plan.
- **Phase 3B**: optionally add warnings or status labels.
- CLI status labels are soft UX markers only; they do not imply deprecation or behavior change.
- **Phase 3C**: run external smoke against the real bridges.
- **Phase 3D**: finish parity work for `agent-json`.
- **Phase 3E**: deprecate or remove only after all gates pass.

## Gates for future removal

### `bridge/rfs-ask.mjs`
Removal/deprecation can proceed only when all of the following are true:

- `rfs ask` has complete functional parity without the bridge.
- `rfs ask --record` remains covered on the non-bridge path.
- Tests cover the non-bridge ask path and its record path.
- External real smoke confirms the bridge is no longer needed.
- `RFS_USE_LEGACY_ASK_BRIDGE` is no longer an accepted supported escape hatch.

### `bridge/rfs-agent.mjs`
Removal/deprecation can proceed only when all of the following are true:

- `rfs agent-json` has functional parity with the current bridge path.
- `rfs agent-json --record` exists and is covered.
- Tests cover the replacement path, including record flow.
- External real smoke confirms the bridge is redundant.
- No bridge-only behavior remains in use.

### `rfs ask-json`
Removal/deprecation can proceed only when all of the following are true:

- The primary ask path already covers the same user-facing behavior.
- The diagnostic transport is no longer needed for validation.
- Tests continue to cover ask behavior without the diagnostic command.
- External smoke proves the diagnostic path adds no unique coverage value.

### `rfs agent-json`
Removal/deprecation can proceed only when all of the following are true:

- `rfs agent-json` has parity with `rfs agent`.
- `rfs agent-json --record` exists and matches the current record semantics.
- Tests cover both the agent and record paths.
- External real smoke is green.
- The current bridge can be removed without losing coverage or behavior.

## Current decision

- No removals authorized.
- No runtime behavior changes authorized.
- Keep all current bridges, diagnostic commands, and baseline TraceSlice paths in place.
- Next recommended step: decide whether to add visible status labels/warnings or run external bridge smoke.
