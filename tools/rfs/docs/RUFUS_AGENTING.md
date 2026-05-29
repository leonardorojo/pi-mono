# Rufus.Agenting

## Purpose

`Rufus.Agenting` is the operational layer for executing tasks through agents.

Short version:

- `Rufus.Agenting` executes.
- `Rufus.RCK.Core` remembers and models.
- `RFS` orchestrates.

`Rufus.Agenting` owns agent execution, task input/output, evidence, and fixed provider/model selection for each agent.
`Rufus.RCK.Core` stays focused on persistent cognitive structures such as State, Delta, Anchor, DAG, and Trace.

## Boundary with Rufus.RCK.Core

Current separation:

- `Rufus.RCK.Core` contains no Agents.
- `Rufus.RCK.Core` does not execute tasks.
- `Rufus.RCK.Core` does not know provider/model details.
- `Rufus.RCK.Core` does not depend on `Rufus.Agenting`.
- `Rufus.Agenting` does not depend on `Rufus.RCK.Core`.
- `Rufus.Agenting` does not write `.rfs/rck`.

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

## CLI surfaces in this phase

### `rfs intent "<prompt>"`

`rfs intent` is the minimal CLI harness for `IntentInferenceAgent`.

Behavior:

- creates an `AgentTask` with:
  - `Kind = "infer-intent"`
  - `Goal = "inferir intent operativo del prompt"`
  - `Input = <prompt>`
- executes `Rufus.Agenting.Intent.IntentInferenceAgent`
- prints `Status`, `AgentId`, `ExecutionModel`, `Summary`, `Output`, `Evidence`, `Warnings`, and `Errors` when present
- does not call Pi
- does not use JSONL or RPC
- does not write `.rfs/rck`

### `rfs agent-json <task>`

`rfs agent-json` remains a prototype JSON Event Stream path.

Behavior in this phase:

- remains separate from `rfs agent`
- remains separate from `rfs agent --record`
- is explicitly experimental in runtime and docs
- prints this warning when executed:

`Experimental: relies on Pi --tools enforcement for read-only behavior.`

- does not write `.rfs/rck`
- still relies on Pi JSON Event Stream plus Pi `--tools` restriction for read-only execution

## Intent inference agents

`Rufus.Agenting` provides two intent inference agents:

### `IntentInferenceAgent` (deterministic)

- Lives in `Rufus.Agenting.Intent`
- Is mock/deterministic
- Accepts only `AgentTask` with `Kind = infer-intent`
- Returns a `PromptIntent` JSON payload
- Returns `Failed` when the task kind is unsupported

This agent is a contract example. Used by `rfs intent`.

### `PiIntentInferenceAgent` (LLM-backed)

- Lives in `Rufus.Cli.Intent`
- Uses Pi JSON Event Stream (`claude-haiku-4.5`)
- Accepts only `AgentTask` with `Kind = infer-intent`
- Parses the LLM answer into `PromptIntent` JSON via `PromptIntentJsonCodec`
- Used by Complete mode stage [1/5] and `rfs intent --llm`

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
- RCK writes from the agent itself
- TraceSlice execution
- migration of `rfs agent`
- migration of `rfs agent --record`

(Note: Pi-backed agents exist in `Rufus.Cli` — `PiIntentInferenceAgent`,
`PiTraceSliceProposalAgent`, `PiPrincipalAnswerAgent` — and are operational
in Complete mode. They are CLI-layer adapters, not Agenting-layer primitives.)

## Possible next steps

Future work could include:

- connecting `AgentTaskResult` to RCK `State` / `Delta` (done: `rfs intent --record`)
- adding agents for `TraceSlice` and `ContextPack` (done: `PiTraceSliceProposalAgent` in `Rufus.Cli`)
- swapping `mock` agents for provider-backed agents where needed (done for Complete mode)
