# RFS Long Pipeline / Model Strategy

## Purpose

This document turns the long-pipeline model audit into a canonical, lightweight strategy note.
It explains how RFS should think about model selection across the long pipeline without defining a routing implementation yet.

The central strategy is:

- RFS should not assume that the whole system always uses one single global model forever.
- Today, the workspace default model is the only persisted model choice.
- The long pipeline may eventually need stage-specific model configuration, but that is a future design space, not a current implementation.
- `Rufus.RCK.Core` must remain free of models, agents, providers, and model routing.

## Sources of truth

This strategy note should be read together with:

- [`RFS_PIPELINE_LONG_MODEL_AUDIT.md`](RFS_PIPELINE_LONG_MODEL_AUDIT.md)
- [`RFS_COMMAND_GOVERNANCE.md`](RFS_COMMAND_GOVERNANCE.md)
- [`RFS_TUI_UX_CONTRACT.md`](RFS_TUI_UX_CONTRACT.md)
- [`tools/rfs/docs/issues/001-rfs-long-pipeline-model-strategy.md`](issues/001-rfs-long-pipeline-model-strategy.md)

The audit remains the evidence base. This document is the strategic framing.
It does not repeat the full audit.

## Current state summary

The repo currently has one persisted workspace-level model setting: `.rfs/config.json -> llm.defaultModel`.
That value is reused by the headless Pi-backed paths and the TUI flows that call Pi.

There is no formal `ModelProfile`, no formal `AgentProfile`, and no formal `PipelineRole -> ModelProfile` mapping in the current codebase.
The long pipeline exists as a set of composed stages, not as a model-routing runtime.

In practice, the system currently splits into three kinds of work:

- deterministic local stages that shape intent, trace slices, and context packs;
- legacy bridge paths that still exist for `rfs agent`;
- LLM-backed execution that consumes the workspace default model as the current global default.

## Long pipeline state by stage

### 1. Intent inference

- **Classification:** implemented; deterministic path (`IntentInferenceAgent`) and LLM-backed path (`PiIntentInferenceAgent`, `claude-haiku-4.5`) both exist
- **Current shape:** Complete mode uses the LLM-backed agent in stage [1/5]; the deterministic path remains available via `rfs intent`
- **Evidence:** `tools/rfs/src/Rufus.Agenting/Intent/IntentInferenceAgent.cs`, `tools/rfs/src/Rufus.Cli/Intent/PiIntentInferenceAgent.cs`, `tools/rfs/src/Rufus.Cli/Tui/RfsCompleteModePipeline.cs`
- **Note:** the LLM-backed path is operational in Complete mode, not just experimental.

### 2. TraceSlice generation

- **Classification:** implemented; deterministic baseline plus LLM-backed proposal/selection path
- **Current shape:** `PiTraceSliceProposalAgent` (`claude-sonnet-4.5`) performs anchor selection in Complete mode stage [2/5]; deterministic `TraceSlicePlannerAgent` remains available for baseline paths
- **Evidence:** `tools/rfs/src/Rufus.Agenting/TraceSlice/TraceSlicePlannerAgent.cs`, `tools/rfs/src/Rufus.Cli/TraceSlice/PiTraceSliceProposalAgent.cs`, `tools/rfs/src/Rufus.RCK.Workspace/RckTraceSliceBuilder.cs`, `tools/rfs/src/Rufus.Cli/Tui/RfsCompleteModePipeline.cs`, `tools/rfs/src/Rufus.RCK.Workspace/RckTraceSliceProposalValidator.cs`
- **Note:** proposal output is never authoritative by itself; RFS validation remains the authority boundary.

### 3. ContextPack generation

- **Classification:** implemented, deterministic-first
- **Current shape:** read-only projection from the workspace/RCK state or from a validated TraceSlice
- **Evidence:** `tools/rfs/src/Rufus.RCK.Workspace/RckWorkspaceContextPackReader.cs`, `tools/rfs/src/Rufus.RCK.Workspace/RckTraceSliceContextPackBuilder.cs`, `tools/rfs/src/Rufus.RCK.Workspace/RckTraceSliceBuilder.cs`
- **Note:** ContextPack is a projection, not a storage model.

### 4. Main LLM execution

- **Classification:** implemented
- **Current shape:** the downstream consumer of the validated prompt/context package
- **Evidence:** `tools/rfs/src/Rufus.Cli/Tui/RfsCompleteModePipeline.cs`, `tools/rfs/src/Rufus.Cli/PiIntegration/PiJsonEventRunner.cs`, `tools/rfs/src/Rufus.Cli/Program.cs`, `tools/rfs/src/Rufus.Cli/Tui/RfsTuiSession.cs`
- **Note:** the main LLM is currently consumed through Pi-backed execution paths, using the workspace default model when a model needs to be selected.

### 5. Verification / validation

- **Classification:** implemented
- **Current shape:** RFS-side validation of proposals, IDs, policy, and safe materialization boundaries
- **Evidence:** `tools/rfs/src/Rufus.RCK.Workspace/RckTraceSliceProposalValidator.cs`, `tools/rfs/src/Rufus.Cli/Tui/RfsCompleteModePipeline.cs`, `tools/rfs/src/Rufus.Cli/TraceSlice/TraceSliceProposalLlmRunner.cs`
- **Note:** validation belongs to RFS, not to an autonomous model-routing layer.

### 6. State + Delta recording

- **Classification:** implemented
- **Current shape:** local recording after a finalized interaction or controlled agent task
- **Evidence:** `tools/rfs/src/Rufus.RCK.Workspace/RckInteractionRecorder.cs`, `tools/rfs/src/Rufus.RCK.Workspace/RckAgentTaskRecorder.cs`, `tools/rfs/src/Rufus.RCK.Workspace/RckInteractionRecord.cs`, `tools/rfs/src/Rufus.RCK.Core/Model/RckState.cs`, `tools/rfs/src/Rufus.RCK.Core/Model/RckDelta.cs`
- **Note:** model/provider metadata can appear in recorder envelopes, but not in the core state/delta schema.

## Stage classification summary

### Implemented

- Intent inference
- TraceSlice generation baseline
- ContextPack generation
- Main LLM execution
- Verification / validation
- State + Delta recording

### Deterministic

- Intent inference
- TraceSlice planning baseline
- ContextPack generation
- validation boundary logic
- recording persistence logic in the workspace layer

### Legacy

- `rfs agent`
- `rfs agent --record`
- the Node bridge path used by those commands

See [`RFS_COMMAND_GOVERNANCE.md`](RFS_COMMAND_GOVERNANCE.md) for the current legacy/no-migrate-yet framing.

### Experimental

- `rfs ask-json`
- `rfs agent-json`
- `rfs trace-slice-proposal-llm`
- `rfs trace-slice-validate-llm`
- `rfs context-pack --trace-slice-validated`

### Missing / undefined

- `ModelProfile`
- `AgentProfile`
- formal per-stage model configuration
- formal `PipelineRole` contract or class
- a model-routing runtime
- a stage-specific model selection policy

## Model strategy: current default, main LLM, future per-stage configuration

### Workspace default model

Today, the workspace default model is the persisted starting point for model choice.
It lives in `.rfs/config.json` under `llm.defaultModel` and is read by the workspace model config store.

This is the current stable default, not a promise that every stage must forever share one identical model.
It is the fallback used by the current Pi-backed flows when a model must be selected.

### Main LLM model

The main LLM model is the model used by the downstream answer-generation step.
Today, that is still resolved from the workspace model setting in the current Pi-backed paths.

The important conceptual distinction is that the *main LLM model* is a consumer-facing execution choice, while the *workspace default model* is the persisted workspace-level default from which current paths derive that choice.

### Possible future per-stage configuration

The long pipeline may later need per-stage model configuration if the repo grows beyond the current default-model pattern.
That is a strategic possibility, not a current requirement.

If that ever happens, it should be treated as a stage-specific policy layer above the current workspace default, not as a rewrite of RCK storage or Core types.

For now, the repo should keep the conceptual distinction clear:

- workspace default model = current persisted default
- main LLM model = the active downstream answer model
- future per-stage config = a possible future overlay, not an existing contract

## Boundary: `Rufus.RCK.Core` stays model-free

`Rufus.RCK.Core` must remain independent from models, agents, providers, and model routing.
That boundary is already visible in the current codebase:

- `Rufus.RCK.Core` defines canonical state, delta, and anchor structures.
- `Rufus.RCK.Core` does not define `model`, `provider`, `agent`, or `profile` fields.
- model/provider metadata lives in workspace-layer recording envelopes, not in the core schema.
- agent execution lives in the sibling operational layer, not in Core.

This boundary is strategic, not accidental.
The long-pipeline model discussion must not pull model-routing concepts into Core.

## Cross references

- Audit and evidence: [`RFS_PIPELINE_LONG_MODEL_AUDIT.md`](RFS_PIPELINE_LONG_MODEL_AUDIT.md)
- Command surface and legacy/experimental classification: [`RFS_COMMAND_GOVERNANCE.md`](RFS_COMMAND_GOVERNANCE.md)
- TUI mode and recording contract: [`RFS_TUI_UX_CONTRACT.md`](RFS_TUI_UX_CONTRACT.md)

## Open questions

Keep these controlled and narrow:

1. Should future per-stage model configuration remain a pure strategy concept until a concrete need appears?
2. If stage-specific selection is ever added, should it be expressed as stage policy above the workspace default, rather than as a new Core concept?
3. Should `ModelProfile` / `AgentProfile` remain absent until the repo has a concrete routing contract to bind them to?
4. Should the strategy note stay stable and canonical, or be revised if a later phase introduces a real model-routing layer?

## Non-goals

This document does not:

- define model routing;
- define per-stage model config;
- introduce `PipelineRole` as a contract;
- define `ModelProfile` or `AgentProfile` in detail;
- deprecate bridges;
- change runtime behavior;
- change RCK schema;
- change `Rufus.RCK.Core`.

