# RFS Model Configuration and Provider Resolution

This document is the canonical reference for how RFS configures, resolves, and executes LLM models today.
It is based on the current code in `tools/rfs/src` and does not propose a new routing layer.

Note: the shell examples below use the local wrapper spelling from the task (`lrfs`). The repo source currently shows `rfs ...` in CLI help and command handlers; the behavior described here is the same.

## 1. Overview

RFS does not talk to providers directly.
RFS chooses a model id or a provider-qualified model id, then invokes Pi.
Pi resolves the effective model and executes it against a real provider.
The provider can be GitHub Copilot, DeepSeek/OpenRouter, OpenAI Codex, Azure OpenAI Responses, Ollama, or any other provider that Pi exposes through its model catalog and runtime resolution.

Conceptually:

```text
RFS config/profile/session
  -> model id / provider-qualified model
  -> Pi invocation
  -> Pi provider resolution
  -> provider auth/API
  -> LLM response
```

The important boundary is:

- RFS owns workspace/session intent and stage policy.
- Pi owns provider mapping and provider-specific execution.
- RFS should preserve provider when it knows it.
- RFS should not silently collapse a provider-qualified selection into a bare model id.

## 2. Configuration layers

RFS has three model configuration layers that matter today:

1. workspace config in `.rfs/config.json`
2. Complete model profiles in `RfsCompleteModelProfileStore`
3. session model state in the TUI session

These layers are related, but they are not the same thing.

### A) `.rfs/config.json`

Location:

```text
<repo>/.rfs/config.json
```

This is the persisted workspace model configuration.
The current code reads and writes it through `RckWorkspaceModelConfigStore` and `RckWorkspaceInitializer`.

Relevant fields:

- `schemaVersion`
  - workspace config schema version
  - current initializer writes `1`
- `type`
  - workspace type marker
  - current initializer writes `rufus.workspace`
- `createdBy`
  - provenance marker
  - current initializer writes `rfs init`
- `llm.defaultModel`
  - workspace default model
  - used as the persisted baseline when no stage-specific override exists
- `llm.stages.intent`
  - stage-specific model for Complete intent resolution
- `llm.stages.traceSliceProposal`
  - stage-specific model for Complete trace-slice proposal
- `llm.stages.conversationalMemory`
  - stage-specific model for Complete conversational-memory synthesis
- `llm.stages.principalAnswer`
  - stage-specific model for Complete principal-answer execution

Balanced example:

```json
{
  "schemaVersion": 1,
  "type": "rufus.workspace",
  "createdBy": "rfs init",
  "llm": {
    "defaultModel": "gpt-5.4-mini",
    "stages": {
      "intent": {
        "model": "claude-haiku-4.5"
      },
      "traceSliceProposal": {
        "model": "gpt-5.4-mini"
      },
      "conversationalMemory": {
        "model": "gpt-5.4-mini"
      },
      "principalAnswer": {
        "model": "gpt-5.4-mini"
      }
    }
  }
}
```

How it is used:

- `RckWorkspaceModelConfigStore.Read()` reads `llm.defaultModel`.
- `RckWorkspaceModelConfigStore.TryReadStageModel(stageName)` reads `llm.stages.<stageName>.model`.
- `RckWorkspaceInitializer.Initialize()` creates the file if it does not exist.
- `RckWorkspaceInitializer` only upgrades a legacy init-generated config in a controlled way.
- Explicit user configuration should not be overwritten by init.

### B) Complete model profiles

Source:

```text
tools/rfs/src/Rufus.RCK.Workspace/RfsCompleteModelProfileStore.cs
```

This file is the current source of truth for the built-in Complete profiles.
The profile matrix is duplicated into `.rfs/config.json` only when a profile is applied or init seeds the workspace with the balanced profile.

Current profiles:

#### `test`

- `defaultModel`: `deepseek-chat`
- `intent`: `deepseek-chat`
- `traceSliceProposal`: `deepseek-chat`
- `conversationalMemory`: `deepseek-chat`
- `principalAnswer`: `deepseek-chat`

Meaning:

- DeepSeek/API-oriented profile for testing Complete mode.
- This is the cheap / test-oriented profile.

#### `balanced`

- `defaultModel`: `gpt-5.4-mini`
- `intent`: `claude-haiku-4.5`
- `traceSliceProposal`: `gpt-5.4-mini`
- `conversationalMemory`: `gpt-5.4-mini`
- `principalAnswer`: `gpt-5.4-mini`

Meaning:

- The main profile for normal Complete usage.
- The intent stage uses a lighter model.
- The remaining stages use the main balanced model.

How the commands behave today:

- `lrfs init` applies `balanced` by default.
- `lrfs model profile test` applies `test`.
- `lrfs model profile balanced` applies `balanced`.
- `lrfs model profile` shows the available profiles.
- `lrfs model profile <name>` writes the corresponding stage matrix into `.rfs/config.json`.

The TUI `/complete-profile` command is the same profile mechanism from the session UI.

### C) Session model

The TUI session keeps a mutable current-session model in `SessionState`.
This is the model used by the interactive `/model` flow and by session-level Pi invocations when no stage-specific override is being applied.

Current session state fields:

- `CurrentSessionModel`
- `CurrentSessionModelProvider`

Behavior:

- `/model` opens the interactive model picker.
- `/model <model>` changes the session model temporarily.
- `SessionState.SetSessionModel()` stores both the model id and provider when available.
- `SessionState.ResolveMainModel()` returns:
  - `model` if no provider is known
  - `provider/model` if a provider is known

Important distinction:

- workspace default model = persisted workspace baseline
- session current model = temporary in-memory session choice
- Complete stage model = stage-specific override from `.rfs/config.json`

These are separate layers.
Do not treat them as interchangeable.

## 3. Init

Current init behavior:

- `rfs init` / `lrfs init` creates `.rfs/config.json` if it does not exist.
- init seeds the workspace with the balanced profile by default.
- if `.rfs/config.json` already exists, init does not blindly overwrite explicit user configuration.
- init only performs a controlled upgrade when the file is a legacy init-generated config.
- init uses the same profile source of truth as `RfsCompleteModelProfileStore`; it does not duplicate the profile matrix independently.

Useful commands:

```bash
rm -rf .rfs
lrfs init
cat .rfs/config.json
lrfs model profile balanced
```

The equivalent repo-source CLI spelling is `rfs init` and `rfs model profile balanced`.

## 4. Complete mode

Complete mode is stage-driven.
It does not choose one global model and reuse it blindly.
It resolves a model per stage.

Observed stage sequence:

```text
[1/5] intent
[2/5] traceSliceProposal
[4/5] conversationalMemory
[5/5] principalAnswer
```

The missing [3/5] step is the deterministic validation/materialization part of the pipeline, not a separate model-selection stage.
The current code also has the rest of the governed pipeline around these stages, but the model-resolution rules matter most for these stages.

### Stage sources

#### [1/5] intent

Source:

- `llm.stages.intent`
- `RckWorkspaceModelConfigStore.TryReadStageModel("intent", ...)`

Fallback:

- if the stage is absent, the current agent constructor falls back to its built-in execution model (`claude-haiku-4.5`)
- the workspace default model still matters as the persisted baseline for session/default resolution, but it does not replace a configured stage silently

#### [2/5] traceSliceProposal

Source:

- `llm.stages.traceSliceProposal`
- `RckWorkspaceModelConfigStore.TryReadStageModel("traceSliceProposal", ...)`

Fallback:

- if the stage is absent, the current agent constructor falls back to its built-in execution model (`claude-sonnet-4.5`)
- provider preservation still applies when a model is resolved from RFS state

#### [4/5] conversationalMemory

Source:

- `llm.stages.conversationalMemory`
- `RckWorkspaceModelConfigStore.TryReadStageModel("conversationalMemory", ...)`

Fallback:

- if the stage is absent, the current agent constructor falls back to its built-in execution model (`claude-haiku-4.5`)
- the workspace default model can still seed session state, but it is not the stage policy itself

#### [5/5] principalAnswer

Source:

- `llm.stages.principalAnswer`
- `RckWorkspaceModelConfigStore.TryReadStageModel("principalAnswer", ...)`

Fallback:

- if the stage is absent, the final answer step falls back to the session model, which in turn may reflect the workspace default
- if the stage exists, it must not be silently replaced by `SessionState.ResolveMainModel()`

Rule:

- Complete always resolves by stage.
- `llm.defaultModel` is a legacy workspace baseline, not the stage policy.
- Stage-specific models win over session state when they are present.
- Session state only participates in fallback paths.

## 5. `/pi run`

`/pi run` is not a Complete stage.
It is a separate session-level command.

What it does:

- builds an operational prompt from the prior interaction
- uses the effective session/workspace model
- preserves provider when invoking Pi
- can offer to record the response into RCK if recording is enabled in the current flow
- if recorded, the response creates State + Delta
- if not recorded, the answer stays only on screen and Complete later cannot recover it from RCK

Current flow in code:

- `RfsTuiPiPromptBuilder.TryBuild(...)` builds the prompt
- `RfsTuiModelPicker.ResolveExecutionModelAsync(...)` resolves the effective model string
- `RfsTuiPiRunCommand.ExecuteAsync(...)` calls Pi
- the result may then be recorded through `RckInteractionRecorder.RecordTui(...)`

Provider preservation rule:

- if RFS knows the provider, it should pass `provider/model`
- if the selection is ambiguous, RFS should not collapse it to a bare model id

Example:

```text
github-copilot/gpt-5.4-mini
```

Not this:

```text
gpt-5.4-mini
```

That matters because a bare id can be resolved by Pi to a different backend provider than the one the user selected.

## 6. `/hermes run`

`/hermes run` exists in the TUI command catalog and is implemented in `RfsTuiHermesRunCommand`.

Current state, confirmed in code:

- it does not select an LLM model through RFS
- it builds a handoff prompt from the last interaction
- it executes the `hermes` CLI one-shot with `hermes -z <prompt>`
- it is a transport / handoff path, not a model-routing path
- it does not use `defaultModel`, `CurrentSessionModel`, or a provider-qualified model for its own execution

So the answer to “what model mechanism does `/hermes run` use?” is:

- not a model mechanism at all
- it uses the Hermes CLI directly

If you are looking for model selection, `/hermes run` is the wrong command.
Use `/model`, `/pi run`, or Complete mode instead.

## 7. Provider resolution

RFS can display provider-aware models in the picker.
The picker stores both the model id and provider when available.

Conceptual picker example:

```text
Model id       Display name      Provider
------------------------------------------------
deepseek-chat   DeepSeek Chat     deepseek
claude-haiku-4.5  Claude Haiku   github-copilot
gpt-5.4-mini    GPT-5.4 Mini     github-copilot
qwen3:1.7b      Qwen3 1.7B        ollama
```

The exact catalog contents depend on what Pi exposes at runtime.

Historical failure mode:

- RFS displayed a provider but passed only the bare model id.
- `gpt-5.4-mini` was ambiguous.
- Pi could resolve it to `azure-openai-responses`.
- That could produce `No API key found for azure-openai-responses`.

Current rule:

- when RFS knows the provider, preserve it
- for ambiguous ids, invoke Pi as `provider/model`
- do not change Pi’s provider mapping from RFS
- do not assume a bare model id has a unique provider

## 8. Catalog of models

Places to inspect the current catalog:

- RFS model picker in the TUI
- `packages/ai/src/models.generated.ts` as the generated catalog source in this repo, when relevant
- Pi model listing via the CLI / RPC path used by the picker

Example table:

| Model id | Provider expected in RFS | Usage |
| --- | --- | --- |
| `deepseek-chat` | `deepseek` / `openrouter` | `test` profile |
| `claude-haiku-4.5` | `github-copilot` | `intent` in `balanced` |
| `gpt-5.4-mini` | `github-copilot` when selected from RFS picker/config | balanced main LLM |
| `qwen3:1.7b` | `ollama` | local / Pi-default style use if exposed |

Important distinction:

- Pi may have its own default model.
- Pi’s default only matters if RFS does not pass an explicit model.
- If RFS passes an explicit model, RFS wins.

## 9. Precedence rules

### Complete stage

1. `llm.stages.<stage>.model`
2. provider preservation for that stage model
3. `llm.defaultModel` as legacy fallback
4. session/default model only if there is no stage config
5. warning/error if the provider cannot be resolved

### `/pi run`

1. session model if it was selected with `/model`
2. workspace `llm.defaultModel`
3. Pi default only if RFS intentionally does not pass a model
4. provider preservation before invoking Pi

### `/hermes run`

- no LLM model precedence applies
- the command is a Hermes CLI handoff path, not a Pi model selection path

## 10. Useful commands

Shell / workspace commands:

```bash
lrfs init
lrfs model get
lrfs model profile test
lrfs model profile balanced
```

TUI commands:

```text
/model
/model <model>
/complete-profile test
/complete-profile balanced
/pi run
/hermes run
```

Repo-source spellings are `rfs ...` where the CLI help text uses `rfs`.

## 11. Troubleshooting

### A) `No API key found for azure-openai-responses`

Probable cause:

- RFS passed a bare ambiguous model id
- Pi resolved it to Azure

Diagnosis:

- check the effective model log line
- confirm whether RFS sent `provider/model`
- inspect `/model` picker output
- inspect `.rfs/config.json`

Expected fix:

- preserve provider
- select the correct model/provider pairing
- do not hide the problem by changing profiles to something else without understanding the resolution path

### B) Complete uses the wrong model

Diagnosis:

- inspect `.rfs/config.json`
- inspect the stage-specific models
- inspect the current session model from `/model`
- inspect the stage log lines, especially `[1/5]`, `[2/5]`, `[4/5]`, `[5/5]`

### C) `/pi run` uses a different model than Complete

This is expected when stage config differs from session config.

- Complete uses stage-specific models.
- `/pi run` uses session/workspace model resolution.
- The two paths are not required to match.

### D) Pi default can differ from RFS

This is expected if Pi has its own default model and RFS passes an explicit model.

For example, if Pi defaults to `qwen3:1.7b` in your environment:

- Pi default only applies when RFS does not pass a model.
- The balanced profile explicitly uses `gpt-5.4-mini`.

## 12. State known today and debts

Known current debts / watch items:

- ConversationalMemory can still fail if JSON parsing encounters fenced or malformed output and that path has not been normalized.
- `/pi run` can record State + Delta, but the current recorded ContextPack / response payload should be validated end-to-end whenever that feature is touched.
- Provider preservation must cover every call site that sends a model into Pi.
- If `azure-openai-responses` appears again, there is probably still one unresolved path that passed a bare model id.

## 13. Validation / inspection notes

This document reflects the current code paths in:

- `tools/rfs/src/Rufus.RCK.Workspace/RckWorkspaceInitializer.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckWorkspaceModelConfigStore.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RfsCompleteModelProfileStore.cs`
- `tools/rfs/src/Rufus.Cli/Tui/RfsTuiSessionState.cs`
- `tools/rfs/src/Rufus.Cli/Tui/RfsTuiModelPicker.cs`
- `tools/rfs/src/Rufus.Cli/Tui/RfsTuiPiRunCommand.cs`
- `tools/rfs/src/Rufus.Cli/Tui/RfsTuiSession.cs`
- `tools/rfs/src/Rufus.Cli/Tui/RfsCompleteModePipeline.cs`
- `tools/rfs/src/Rufus.Cli/Tui/RfsTuiHermesRunCommand.cs`
- `tools/rfs/src/Rufus.Cli/Tui/RfsTuiHermesRunner.cs`
- `tools/rfs/src/Rufus.Cli/Answering/PiPrincipalAnswerAgent.cs`
- `tools/rfs/src/Rufus.Cli/Intent/PiIntentInferenceAgent.cs`
- `tools/rfs/src/Rufus.Cli/TraceSlice/PiTraceSliceProposalAgent.cs`
- `tools/rfs/src/Rufus.Cli/ConversationalMemory/PiConversationalMemoryAgent.cs`

## 14. Cross references

- `tools/rfs/docs/RFS_COMMAND_GOVERNANCE.md`
- `tools/rfs/docs/RFS_TUI_UX_CONTRACT.md`
- `tools/rfs/docs/RFS_PIPELINE_LONG_MODEL_STRATEGY.md`
- `tools/rfs/docs/RFS_PI_PROGRAMMATIC_INTEGRATION_AUDIT.md`
- `tools/rfs/docs/RFS_LEGACY_DEPRECATION_PLAN.md`
