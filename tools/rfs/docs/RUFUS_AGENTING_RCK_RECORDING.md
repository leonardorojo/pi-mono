# Rufus.Agenting -> RCK recording design

## Status of this document

This document is design-only.
It does not implement recording.
It does not change runtime behavior.
It does not change `Rufus.RCK.Core`.
It does not change `Rufus.Agenting` execution semantics.

Current architectural split remains:

- `Rufus.Agenting` executes.
- `Rufus.RCK.Core` models, remembers, and persists.
- `RFS` orchestrates.

## 1. Problem

Executing an agent is not enough when RFS needs durable cognitive traceability.

A future RFS flow will need to record, in a controlled way:

- which agent executed;
- which task it received;
- what result it produced;
- which provider/model executed it;
- which evidence was used or produced;
- which warnings/errors occurred;
- how the result relates to the original prompt and to a future `TraceSlice` or interaction transition.

Without a recording design, `AgentTaskResult` stays operational only:

- useful during execution;
- visible in CLI output;
- lost as structured cognitive context once the command ends.

That is acceptable for the current phase, but insufficient for future flows such as:

- `rfs intent --record`;
- deterministic `TraceSlice` preparation;
- future agent proposals that RFS validates before persisting;
- engine-agnostic evidence capture for Pi-backed and non-Pi-backed agents.

The problem is not "how to let agents write memory directly".
The problem is "how to let RFS transform agent execution outcomes into controlled RCK memory without collapsing layer boundaries".

## 2. Principles

The future recording path should follow these principles explicitly:

1. `Rufus.Agenting` does not write RCK directly.
2. `Rufus.RCK.Core` does not know concrete agents.
3. `RFS` or `Rufus.RCK.Workspace` acts as the persistence adapter/orchestrator.
4. RCK should not store unnecessary raw logs.
5. RCK should store facts, refs, and summaries.
6. Full `AgentTaskResult` must not persist automatically without filtering.
7. Provider/model is execution evidence, not the primary cognitive identity.
8. Raw JSONL/RPC traffic must not enter the DAG unless persisted as an explicit artifact/ref with a clear reason.
9. The persisted shape must stay engine-agnostic: Pi is one producer, not the storage schema.
10. The persisted record must be selected and normalized by RFS before any write.

Implication:

The recording boundary belongs above `Rufus.Agenting` and outside `Rufus.RCK.Core`.
The adapter layer decides what becomes memory, what becomes evidence, and what stays external.

## 3. What should be stored

A future recording flow should conceptually persist a filtered projection of `AgentTaskResult`, not the raw object as-is.

### Minimum useful payload

Persist the minimum stable execution facts:

- `taskId`
- `taskKind`
- `agentId`
- execution model `provider` / `model`
- `status`
- `summary`
- controlled `output` summary or controlled output payload
- evidence summaries
- warning summaries
- error summaries
- `refs` / `evidenceRefs` when external artifacts exist

Also include the orchestration relationship when available:

- originating prompt id or interaction id;
- related transition/delta id when the result is attached to an interaction;
- future `TraceSlice` linkage when applicable.

### What should not be stored by default

Do not persist by default:

- full JSONL streams;
- full tool outputs;
- full `stdout` / `stderr`;
- long internal prompts for subagents;
- secrets;
- blobs;
- full diffs;
- provider-specific event dumps;
- arbitrary raw `Output` strings without review.

### Why filtering is required

`AgentTaskResult` is an execution contract.
RCK is a cognitive memory contract.

Those are related but not identical.
A future adapter must filter for:

- token efficiency;
- long-term usefulness;
- safety;
- reproducibility;
- engine independence.

This matches existing RCK DAG principles:

- store facts and references;
- prefer summaries over replay logs;
- keep reproducible/raw payloads outside the primary DAG unless specifically justified.

## 4. State vs Delta

The main design question is how a future `AgentTaskResult` should appear in RCK.

### Option A: `AgentTaskResult` creates a new State

Concept:
A successful agent execution becomes its own new `State`.

Advantages:

- simple mental model: each agent run becomes a visible state boundary;
- easy to inspect in timelines;
- gives the result strong identity.

Problems:

- too heavy for many agent runs;
- risks turning operational substeps into primary memory;
- encourages storing too many intermediate states;
- weak fit when the agent result only supports a parent interaction.

Use when:

- the agent produced a durable user-visible conclusion that truly changes the remembered cognitive state;
- the result is itself the main outcome, not supporting evidence.

Assessment:
Not ideal as the default first implementation.

### Option B: `AgentTaskResult` creates a Delta between states

Concept:
The agent result becomes the primary cause/change payload of a new `Delta`.

Advantages:

- better fit for "something changed because this result happened";
- preserves transition semantics;
- keeps the result attached to an explicit before/after boundary.

Problems:

- still too strong as a default for every agent run;
- requires deciding how much of the result becomes primary change vs supporting evidence;
- may over-model intermediate machine operations as cognitive change.

Use when:

- the agent result directly explains a state transition that RFS already decided to persist.

Assessment:
Potentially valid later, but too aggressive as the first default.

### Option C: `AgentTaskResult` becomes controlled evidence within a primary interaction Delta

Concept:
The primary persisted object remains the user/system interaction transition.
The agent result is attached as controlled evidence inside that transition.

Advantages:

- best preserves current architecture;
- lets RFS remain the decision point;
- avoids promoting every agent execution to autonomous memory;
- fits "agent proposes, RFS validates, RCK persists selected facts";
- easier to keep compact and filtered.

Problems:

- requires careful evidence summarization rules;
- some agent runs may later deserve stronger standalone identity;
- inspection may require following evidence links rather than reading one dedicated state.

Use when:

- the agent supports a parent interaction;
- the result informs interpretation, classification, or proposal;
- the result should be remembered, but not as primary autonomous memory.

Assessment:
This is the safest first implementation path.

### Option D: `AgentTaskResult` stays external and RCK stores only an artifact/ref

Concept:
The full result or event stream is stored outside the DAG, and RCK only stores a reference.

Advantages:

- keeps the DAG small;
- preserves detailed diagnostics when really needed;
- works for large Pi JSONL/RPC outputs or audit artifacts.

Problems:

- weaker immediate cognitive value if the DAG only points outward;
- requires external artifact lifecycle and location rules;
- can become an excuse to avoid summarization.

Use when:

- the raw material is large, provider-specific, or audit-oriented;
- the detailed result must remain retrievable but should not live in primary memory.

Assessment:
Useful as a secondary mechanism, not the default cognitive representation.

### Recommendation

For the first future implementation, prefer Option C:

- persist `AgentTaskResult` as controlled evidence associated with a primary interaction/transition;
- do not treat it as autonomous memory by default;
- allow special cases later where RFS intentionally promotes a result into a State or a Delta-focused cause.

This keeps the first bridge conservative and aligned with the current layer split.

## 5. First safe case: `IntentInferenceAgent`

The safest first recording case is the existing deterministic intent flow.

Current flow:

```text
PromptN
  -> rfs intent
  -> IntentInferenceAgent
  -> AgentTaskResult
  -> CLI output only
```

Future minimal recording candidate:

```text
PromptN
  -> rfs intent --record
  -> IntentInferenceAgent
  -> AgentTaskResult
  -> RFS filtering / evidence preparation
  -> controlled RCK write
```

Or internally:

```text
PromptN
  -> IntentInferenceAgent
  -> AgentTaskResult
  -> RCK evidence/slice preparation
```

Important:

- this document does not implement `rfs intent --record`;
- this document does not define final CLI syntax;
- this document only defines the future boundary.

Why this is the safest first case:

- `IntentInferenceAgent` already exists;
- it is deterministic/mock;
- it has fixed provider/model metadata;
- it already emits summary, output, and evidence;
- it does not depend on Pi JSONL/RPC.

That makes it the lowest-risk place to validate the recording adapter contract before touching richer providers.

## 6. Relationship with `TraceSlice`

A future agent result can feed `TraceSlice`, but should not bypass RFS selection.

Near-term conceptual flow:

```text
PromptN
  -> Intent Agent
  -> intent result
  -> TraceSlice deterministic v0
  -> ContextPack
```

Later conceptual flow:

```text
PromptN
  -> Intent Agent
  -> TraceSliceAgent
  -> TraceSliceProposal
  -> RFS validation
  -> ContextPack
```

Key boundary:

- the agent may propose;
- RFS validates;
- RCK persists only the selected/controlled result.

This matters because `TraceSlice` is not just an execution artifact.
It is a cognitive projection that may affect downstream context selection.
So:

- agents should produce candidate interpretations;
- RFS should validate structure, relevance, and allowed fields;
- RCK should store only the accepted compact result.

That preserves deterministic control and avoids persisting unreviewed agent reasoning as canonical memory.

## 7. Relationship with Pi JSONL/RPC

Pi-backed agents may later produce richer runtime outputs:

- `PiJsonAgent` result;
- `PiRpcAgent` result;
- tool events;
- provider/model info;
- event counts;
- tool evidence summaries.

These can still map into the same recording design if RFS adapts them before persistence.

Preferred mapping direction:

- Pi event stream -> adapter normalization
- adapter normalization -> `AgentEvidence` summaries and/or external refs
- RFS selection -> RCK persistence

Important constraints:

- RCK schema must not depend on Pi-specific event names;
- Pi events should adapt into `AgentEvidence`, summaries, counts, or explicit external refs;
- RCK must stay engine-agnostic.

Examples of safe normalized fields:

- provider/model used;
- tool invocation count;
- count of warning/error events;
- summarized tool evidence such as "read 3 files" or "inspected git diff";
- explicit artifact ref when raw JSONL must be retained for audit/debug.

Examples of unsafe direct persistence:

- dumping Pi JSONL records into a Delta payload;
- storing full RPC envelopes inside the DAG;
- coupling RCK interpretation logic to Pi event type taxonomy.

## 8. Initial conceptual shape

The following payload is conceptual only.
It is not a final schema.
It is not an implementation contract.

```json
{
  "type": "rufus.agent-task-result",
  "schemaVersion": 1,
  "task": {
    "id": "...",
    "kind": "...",
    "goal": "..."
  },
  "agent": {
    "id": "...",
    "provider": "...",
    "model": "..."
  },
  "result": {
    "status": "Succeeded|Failed|Partial",
    "summary": "...",
    "output": {}
  },
  "evidence": [],
  "warnings": [],
  "errors": []
}
```

If RFS ever persists a shape like this, it should be a filtered and controlled projection.

Likely future refinements:

- link to parent interaction/prompt/transition;
- explicit `refs` / `evidenceRefs`;
- summarized output vs structured output selection policy;
- classification of evidence kinds;
- artifact boundary for large external payloads.

## 9. Non-goals

This phase does not implement:

- `rfs intent --record`;
- RCK writes for `AgentTaskResult`;
- changes in `RckState` or `RckDelta`;
- changes in `Rufus.RCK.Core`;
- `TraceSlice`;
- `TraceSliceAgent`;
- `ModelRouter`;
- agent migration;
- real subagents;
- new Pi integration.

Also not included:

- changing `.rfs/rck` layout;
- changing command behavior;
- changing provider/model routing;
- changing current `rfs intent` output semantics.

## 10. Recommended next microphase

Two logical next options are:

- P12A: implement minimal `rfs intent --record` with strict filtering and controlled evidence persistence;
- P12B: implement deterministic `TraceSlice` v0 without recording `AgentTaskResult` yet.

Recommendation: prefer P12A first.

Why P12A is safer:

1. It validates the adapter boundary directly.
2. It reuses the simplest existing agent: `IntentInferenceAgent`.
3. It can remain deterministic and tightly scoped.
4. It proves the filtering policy before a more semantic `TraceSlice` layer exists.
5. It avoids introducing `TraceSlice` concepts before the recording contract is tested.

Suggested scope for P12A:

- add a minimal `rfs intent --record` path;
- keep persistence controlled and summary-first;
- attach the result as evidence to a primary interaction transition rather than autonomous memory;
- keep `Rufus.RCK.Core` unchanged;
- keep Pi-specific concerns out of the first implementation.

Suggested follow-up after P12A:

- P12B: deterministic `TraceSlice` v0 derived from prompt + validated intent result;
- only after that, consider richer provider-backed agent adapters or external artifact refs.

## Final design position

The future connection between `Rufus.Agenting` and RCK should be:

- adapter-based, not direct;
- filtered, not raw;
- evidence-first, not autonomous-memory-first;
- engine-agnostic, not Pi-shaped;
- validated by RFS before persistence.

In short:

- agents execute;
- agents may propose;
- RFS selects and normalizes;
- RCK persists only the controlled cognitive result.
