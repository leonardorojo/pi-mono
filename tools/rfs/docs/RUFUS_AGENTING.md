# Rufus.Agenting

## Purpose

`Rufus.Agenting` is the operational layer for executing tasks through agents.

Short version:

- `Rufus.Agenting` executes.
- `Rufus.RCK.Core` remembers and models.

`Rufus.Agenting` owns agent execution, task input/output, evidence, and fixed provider/model selection for each agent.
`Rufus.RCK.Core` stays focused on persistent cognitive structures such as State, Delta, Anchor, DAG, and Trace.

## Boundary with Rufus.RCK.Core

Current separation:

- `Rufus.RCK.Core` contains no Agents.
- `Rufus.RCK.Core` does not execute tasks.
- `Rufus.RCK.Core` does not know provider/model details.
- `Rufus.RCK.Core` does not depend on `Rufus.Agenting`.
- `Rufus.Agenting` does not depend on `Rufus.RCK.Core`.

Operational flow:

1. RFS creates or chooses an `IAgent`.
2. The agent executes one `AgentTask`.
3. The agent returns an `AgentTaskResult` with evidence.
4. RFS may later project or persist that result into RCK if needed.

RCK Core is not the execution layer.

## Agent / Task model

### `IAgent`

An agent has:

- `Id`
- `Descriptor`
- `ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)`

### `AgentDescriptor`

Describes the agent:

- `Id`
- `Name`
- `Role`
- `ExecutionModel`
- `Capabilities`

### `AgentExecutionModel`

Defines the fixed runtime identity of the agent implementation:

- `Provider`
- `Model`

This is intentionally fixed per agent.

### `AgentTask`

Describes the work to do:

- `Id`
- `Kind`
- `Goal`
- `Input` optional
- `ExpectedOutput` optional

### `AgentTaskResult`

Captures the outcome:

- `TaskId`
- `Status`
- `AgentId`
- `ExecutionModel`
- `Output` optional
- `Summary` optional
- `Evidence`
- `Warnings`
- `Errors`

### `AgentTaskStatus`

Current values:

- `Succeeded`
- `Failed`
- `Partial`

### `AgentEvidence`

Minimal evidence record:

- `Kind`
- `Source`
- `Detail` optional

## Provider/model is fixed, not routed dynamically

There is no `ModelRouter`.
There is no runtime model selection.

The agent definition owns its provider/model via `AgentExecutionModel`.
If a different depth, model, or provider is needed, define another agent.

Conceptual examples:

- `IntentInferenceAgent`
  - provider: `mock`
  - model: `deterministic-v1`

- future `DeepIntentInferenceAgent`
  - provider: `pi` or `openai`
  - model: a stronger model

## `IntentInferenceAgent`

`IntentInferenceAgent` is the first example agent.

- Lives in `Rufus.Agenting.Intent`
- Is mock/deterministic
- Accepts only `AgentTask` with `Kind = infer-intent`
- Returns a `PromptIntent` JSON payload
- Returns `Failed` when the task kind is unsupported

This agent is a contract example, not a real LLM-backed inference path yet.

## Non-goals for this phase

Not part of this layer:

- `ModelRouter`
- runtime model selection
- planner
- supervisor
- workflow engine
- tools
- memory
- handoffs
- real Pi/OpenAI/Codex integration
- RCK writes from the agent itself
- TraceSlice execution

## Possible next steps

Future work could include:

- using `IntentInferenceAgent` from the CLI
- connecting `AgentTaskResult` to RCK `State` / `Delta`
- adding agents for `TraceSlice` and `ContextPack`
- swapping `mock` agents for provider-backed agents where needed
