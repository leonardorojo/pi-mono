# RufusChat UI Adapter Contract 7B

## 1. Goal

Define the minimum safe contract between a future RufusChat UI and the RCK Bridge.

This phase is documentation-only. It specifies *what* the UI should consume and *how* it should talk to the bridge, but it does not implement a visual surface or any runtime adapter code.

The intent is to keep PI/RufusChat as the local conversational orchestrator, RCK as the auditable operational truth, Hermes as the local executor, and the user as the final decision-maker.

## 2. Non-goals

- No UI real implementation
- No React, Vue, or TUI yet
- No Codex executor
- No RufusLab.RCK.Cli interop
- No raw evidence viewer
- No direct UI reads from `.pi/rck`
- No background jobs
- No autonomous execution
- No runtime changes that are required to make this contract exist
- No dependency additions
- No changes to tests

## 3. Adapter boundary

The contract boundary should be:

`RufusChat UI` → `RckBridgeClient` → `RCK Bridge / RPC / command adapter` → `safe typed DTOs`

Key rule:
- the UI must not depend on console text formatting as a stable contract
- textual command output may exist internally, but it is not the UI API
- the UI should consume typed DTOs that are safe, normalized, and stable across rendering layers

This keeps the presentation layer decoupled from bridge output format drift.

## 4. Proposed client interface

Conceptual TypeScript interface:

```ts
interface RckBridgeClient {
  getStatus(): Promise<RckStatusDto>;
  getInventory(): Promise<RckInventoryDto>;
  getSupervision(): Promise<RckSupervisionDto>;

  createState(request: CreateStateRequest): Promise<CreateStateResultDto>;
  createInjectContext(request: CreateInjectContextRequest): Promise<CreateInjectContextResultDto>;
  createAnchor(request: CreateAnchorRequest): Promise<CreateAnchorResultDto>;

  runHermesFake(request: RunHermesRequest): Promise<HermesRunResultDto>;
  runHermesRealGated(request: RunHermesRequest): Promise<HermesRunResultDto>;
}
```

The client should act as a typed adapter over the bridge, not as a text parser embedded in the UI.

## 5. DTOs

DTOs should be minimal, safe, and UI-ready.

### RckStatusDto

Fields:
- `traceId`
- `currentTrace`
- `latestState`
- `latestContextPack`
- `latestAnchor`
- `latestHermesRun`
- `generatedAt`

Purpose:
- provide the current operational snapshot for the UI

### RckInventoryDto

Fields:
- `traceId`
- `counts`
- `latestEvents`
- `latestHermesRun`
- `generatedAt`

Purpose:
- provide compact inventory-level visibility without exposing raw logs

### RckSupervisionDto

Fields:
- `traceId`
- `level`
- `reason`
- `recommendedAction`
- `needsAttention`
- `latestRunId`
- `latestEventId`
- `signals`
- `generatedAt`

Purpose:
- provide supervision and attention state in a compact form

### CreateStateResultDto

Fields:
- `traceId`
- `stateId`
- `eventId`
- `safeSummary`
- `generatedAt`

Purpose:
- summarize state creation safely

### CreateInjectContextResultDto

Fields:
- `traceId`
- `contextPackId`
- `eventId`
- `safeSummary`
- `generatedAt`

Purpose:
- summarize safe context injection

### CreateAnchorResultDto

Fields:
- `traceId`
- `anchorId`
- `eventId`
- `label`
- `safeSummary`
- `generatedAt`

Purpose:
- summarize anchor creation for continuity and auditability

### HermesRunResultDto

Fields:
- `traceId`
- `runId`
- `requestedEventId`
- `recordedEventId`
- `status`
- `exitCode`
- `durationMs`
- `safeSummary`
- `evidenceRefs`
- `generatedAt`

Purpose:
- provide safe execution results for Hermes fake and gated real runs

### EvidenceRefDto

Fields:
- `kind`
- `refId`
- `path`
- `isRaw`
- `displayPolicy`

Purpose:
- reference evidence without forcing the UI to show raw artifacts by default

Important:
- `evidenceRefs` may exist
- raw evidence must not be shown by default
- the default UI surface should render summaries and references, not payload dumps

### Request DTOs

The request DTOs are intentionally minimal in this phase.

Examples:

```ts
interface CreateStateRequest {
  promptSummary?: string;
  source?: string;
}

interface CreateInjectContextRequest {
  sourceStateId?: string;
  scope?: string;
}

interface CreateAnchorRequest {
  label: string;
  sourceStateId?: string;
}

interface RunHermesRequest {
  prompt: string;
  mode?: "fake" | "real";
}
```

These request shapes are conceptual. The final implementation may refine them, but the UI contract should remain safe and typed.

## 6. Command mapping

The client should map to current bridge commands as follows.

### `getStatus()`
→ `/rck status`

### `getInventory()`
→ `/rck list`

### `getSupervision()`
→ `/rck supervise`

### `createState()`
→ `/state`

### `createInjectContext()`
→ `/rck inject`

### `createAnchor()`
→ `/rck anchor`

### `runHermesFake()`
→ `/hermes fake` or `/hermes --mode fake`

### `runHermesRealGated()`
→ `/hermes --mode real`
→ requires `RCK_BRIDGE_ALLOW_REAL_HERMES=1`

The adapter contract should keep the command mapping explicit so the UI does not invent its own semantics.

## 7. Safety policy

The UI must consume only safe summaries and safe DTOs.

Rules:
- stdout/stderr raw remain evidence refs, not primary UI content
- raw evidence display requires an explicit future action, outside 7B
- Hermes real execution must stay gated
- no autonomous action without a user decision
- the UI must not depend on `.pi/rck` internals as a direct data source
- console text is not the stable API boundary

Default display policy:
- show summaries first
- show IDs when needed
- show evidence references only as references
- keep raw artifacts out of the normal surface

## 8. Error model

Use a minimal adapter error DTO.

```ts
interface RckAdapterErrorDto {
  code:
    | "BRIDGE_UNAVAILABLE"
    | "COMMAND_FAILED"
    | "INVALID_RESPONSE"
    | "SUPERVISION_ATTENTION"
    | "HERMES_REAL_NOT_ALLOWED"
    | "STORAGE_UNAVAILABLE"
    | "UNKNOWN";
  message: string;
  source: string;
  command: string;
  recoverable: boolean;
  recommendedAction: string;
  generatedAt: string;
}
```

Conceptual meaning of the codes:
- `BRIDGE_UNAVAILABLE`: the adapter cannot reach the bridge
- `COMMAND_FAILED`: a command was issued but did not complete successfully
- `INVALID_RESPONSE`: the bridge returned data that does not match the expected DTO shape
- `SUPERVISION_ATTENTION`: the result indicates supervision or user attention is required
- `HERMES_REAL_NOT_ALLOWED`: real Hermes execution was blocked by policy or missing gate
- `STORAGE_UNAVAILABLE`: RCK storage or persistence is not available
- `UNKNOWN`: fallback for unclassified errors

The error model should help the UI decide whether to retry, surface attention, or ask the user for action.

## 9. UI consumption examples

### Example A: load the initial control room

Sequence:
- `getStatus()`
- `getSupervision()`

Expected UI outcome:
- header state is populated
- supervision area shows whether attention exists
- the conversation area remains primary

### Example B: after Hermes execution

Sequence:
- `runHermesFake()` or `runHermesRealGated()`
- `getSupervision()`
- suggest `createState()` if a new stable snapshot is needed

Expected UI outcome:
- the run result is summarized safely
- the supervision panel updates
- the UI can recommend state capture if the run changed the session materially

### Example C: after state creation

Sequence:
- `createState()`
- `getStatus()`
- suggest `createInjectContext()`

Expected UI outcome:
- the new state is visible as a safe summary
- the UI can propose context injection without automatically performing it

## 10. Validation expectations

Because this is a design document only:

- `git diff --name-only` must show only:
  - `docs/rufuschat-ui-adapter-contract-7B.md`
- run:
  - `git diff --check`

No runtime code, tests, or bridge files should be modified for this phase.

## 11. Phase 7C recommendation

Phase 7C should be one of these two options:

**A) Typed DTO normalization over the existing RPC smoke/client**
- preferred option
- converts bridge output into stable DTOs before any UI work
- avoids building a UI on top of free-form text

**B) Minimal shell/prototype only after DTO contract is stable**
- acceptable only if the typed contract is already proven
- still must avoid relying on raw console text as the long-term API

Preferred recommendation:
- **Phase 7C — Adapter DTO normalization**

This keeps the next step focused on a stable typed boundary instead of hardening text parsing into an accidental contract.
