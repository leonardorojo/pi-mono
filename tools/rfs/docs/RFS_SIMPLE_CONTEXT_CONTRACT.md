# RFS Simple Context Contract

## Status

PT6.
Documentation-only.
This document defines the future *Simple Context v0* contract for the TUI Simple mode.
PT7 implements the first runtime use of this contract in the TUI Simple path.

This phase does not implement runtime behavior.
It does not call Pi or any LLM.
It does not write RCK.
It does not touch `Rufus.RCK.Core`.

## 1. Purpose

Simple Context v0 is an ephemeral, compact, and safe materialization for the TUI Simple mode.

It is not a source of truth.
The source of truth remains RCK.

North Star:

- Simple mode must be the daily mode: lightweight and fast.
- Simple Context should help the main LLM respond without trying to reconstruct the full repository history.
- Simple Context must stay intentionally smaller than a full ContextPack.

## 2. Future pipeline

Canonical future flow:

```text
Prompt
  → Simple Context
  → LLM principal
  → respuesta
  → State + Delta
```

Simple Context is an input materialization for the main LLM.
It is not the answer, not the record, and not the persisted state.

## 3. Inclusion criteria

### A. Current prompt

Current prompt has highest priority.

Rules:

- include the full prompt by default
- only trim it in an extreme budget emergency
- do not truncate the current prompt before trimming other sections

Fields:

- `text`
- `isExcerpt`

### B. Recent interactions

Default policy:

- `defaultRecentInteractions = 5`
- `maxRecentInteractions = 8`
- `minRecentInteractions = 1`

Each interaction enters as a summary, not as the full payload.

Each recent interaction may include:

- short or full `stateId`, depending on fit
- `mode`
- prompt excerpt
- `answerSummary`
- `createdAtUtc` when available
- associated git commit when available
- summarized artifact refs when available

Do not include:

- long full answer text
- full `payloadCanonicalJson`
- the entire raw JSON internals

### C. Git context

Git context must always be included.

Always include:

- branch
- commit
- dirty
- changed artifacts count

If dirty:

- include changed artifact paths as metadata only

Do not include:

- git diff
- file contents

### D. Artifact refs

Artifact refs are metadata-only.

May include:

- `path`
- `status`
- `kind` if known
- `size` if known

Defaults:

- `maxArtifactRefs = 20`

If there are more:

- truncate
- add an omission / truncation note

### E. Anchors

Anchors are controlled cognitive milestones.

Defaults:

- `maxAnchors = 3`

Anchor selection criteria:

- include the last anchor if one exists
- include explicitly recent anchors
- include recent commit-boundary anchors
- do not expand anchor contents

## 4. Context budget

Budget targets:

- `targetChars = 16000`
- `maxChars = 24000`
- `hardMaxChars = 32000`

Approximation rule:

- `approxTokens = ceil(chars / 4)`

Rule:

- Simple Context must not try to fill the whole model window
- it must remain compact by design

## 5. Truncation policy

Priority order for inclusion:

1. current prompt
2. git context
3. last relevant anchor
4. recent interactions
5. artifact metadata
6. additional anchors

If the budget is exceeded, reduce in this order:

1. shorten long `answerSummary` fields
2. reduce artifact refs
3. reduce recent interactions from 5 to 3
4. reduce anchors to 1
5. add omissions / truncation notes
6. only in an extreme case trim the current prompt

## 6. Guardrails

Explicitly false:

- `includeFileContents = false`
- `includeGitDiffs = false`
- `includeJsonl = false`
- `includeStdoutStderr = false`
- `includeToolOutputs = false`
- `includeFullContextPack = false`
- `includeFullTraceSlice = false`
- `includePayloadCanonicalJson = false`

Additional safety rules:

- no secrets
- no blobs
- no raw tool output payloads
- no full validation detail dumps
- no full trace / proposal materialization

## 7. Shape conceptual

Proposed JSON shape:

```json
{
  "type": "rufus.simple-context",
  "schemaVersion": 1,
  "prompt": {
    "text": "...",
    "isExcerpt": false
  },
  "budget": {
    "targetChars": 16000,
    "maxChars": 24000,
    "hardMaxChars": 32000,
    "estimatedChars": 0,
    "estimatedTokens": 0,
    "truncated": false
  },
  "git": {},
  "recentInteractions": [],
  "anchors": [],
  "artifacts": [],
  "omissions": [],
  "guardrails": {}
}
```

Suggested conceptual sub-shapes:

- `git.branch`
- `git.commit`
- `git.dirty`
- `git.changedArtifactsCount`
- `git.changedArtifactPaths[]`
- `recentInteractions[]`
- `anchors[]`
- `artifacts[]`
- `omissions[]`

## 8. Relation to TraceSlice / ContextPack

Simple Context does not use `TraceSliceProposal`.
Simple Context does not use the RFS validation runtime.
Simple Context does not use the full ContextPack.

Complete mode will use TraceSlice / Validation / ContextPack.
Simple mode is intentionally less precise but faster.

## 9. Relation to recording

Simple Context is ephemeral input to the main LLM.

When the main LLM responds, the TUI records:

- State + Delta
- `interaction.mode = tui-simple`
- `pipelineSummary.kind = simple`

PT9 reuses the same Simple Context v0 as the default planning context and records it as `interaction.mode = tui-plan` with `pipelineSummary.kind = plan`.

`pipelineSummary` must capture a controlled context summary, including:

- `recentInteractionCount`
- `selectedStateIds` if applicable
- `selectedDeltaIds` if applicable
- `selectedAnchorIds` if applicable
- `artifactRefCount`
- `estimatedChars`
- `estimatedTokens`
- `truncated`

Do not save the full Simple Context when it is too large.
Save a controlled summary instead.

## 10. Future implementation guidance for PT7

PT7 should implement:

- Simple Context builder
- minimal optional render / preview
- main LLM call
- final recording via the TUI recording contract

PT7 should not:

- read file contents
- include diffs
- use TraceSlice
- use Complete mode machinery

## 11. Non-goals

This phase does not:

- implement runtime Simple mode
- call Pi or any LLM
- write RCK
- touch `Rufus.RCK.Core`
- change schema core
- replace Complete mode
- implement autonomous code editing
- materialize a full ContextPack
- materialize a full TraceSlice
