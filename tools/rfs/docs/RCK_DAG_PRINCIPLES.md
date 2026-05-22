# RCK DAG Principles

## Purpose

RCK is not a replacement for Git.
RCK is not a mirror of the filesystem.
RCK must not store everything.
RCK preserves a minimal, verifiable cognitive memory that helps an LLM reconstruct context.

The RCK DAG / Store is the persistent source of truth for facts and references.
The RCK Context Pack is an exportable projection for LLM consumption and may include derived views.

## Core principles

### 1. Reference or reproduce, do not duplicate

Anything that can be referenced or reproduced should not be stored in full inside the DAG.

Do not persist complete copies of:
- file contents
- git diffs
- binary blobs
- full directory snapshots
- long tool outputs
- other reproducible data

These should live as references or reproducible evidence instead.

Examples:
- Git diff -> reference the commit and path(s) involved.
- File content -> reference the path plus commit/worktree status.
- Tool output -> store the tool name, target, status, and a short summary when useful.
- LLM answer -> may be stored as cognitive evidence because it is not deterministically reproducible.

### 2. Consistent, robust, non-redundant DAG

The DAG must have a single source of truth for primary objects such as states, deltas, anchors, refs, and evidenceRefs.

Redundancy is harmful when it repeats the same primary truth in multiple places.
Derived views are acceptable only when they are clearly derived and not treated as source data.

Source objects:
- states
- deltas
- anchors
- refs
- evidenceRefs
- HEAD

Derived views:
- quickIndex
- activeChain
- counts
- orphan ids
- anchorsByStateId
- deltasByToStateId
- deltasByFromStateId

### 3. Complete but optimized

Complete does not mean large.
Complete means sufficient to reconstruct:
- where I am
- what happened
- why it happened
- what changed
- what evidence exists
- what is still missing before a claim can be made

Optimized means maximizing cognitive value per token.

Conceptual formula:

`cognitive value / tokens`

## Store vs Export

The RCK Store / DAG is the minimal persistent model.
The Context Pack is the interpretation-ready projection that an LLM can consume.

The Context Pack may include:
- schema
- interpretationRules
- quickIndex
- derived relationships
- other convenience views for reasoning

These are export-time projections, not storage commitments.
Do not confuse what is persisted with what is exported.

## What belongs in the DAG

Should live in the DAG:
- state id
- delta from/to
- anchor to state
- prompt
- answer or answer summary, depending on role
- minimal git context
- artifact refs
- minimal tool evidence
- minimal metadata

Should not live in the DAG:
- file contents
- git diffs
- binary blobs
- full directory snapshots
- huge tool outputs
- generated artifacts when they can be referenced
- reproducible data

## Context Pack policy

`rfs context-pack` exports pure JSON.
It is a full DAG export plus schema and interpretation rules.
It is a projection, not the storage model.
It may contain derived redundancy when that redundancy improves LLM consumption.

Requirements:
- parseable JSON
- structurally valid
- stable enough for tooling
- comfortable for interpretation

The current correct nested format for `activeChain[*].gitContext` is:

```json
{
  "gitContext": {
    "branch": "master",
    "commit": "...",
    "dirty": true
  }
}
```

`.rfs/` is internal metadata and is not a user artifact.

## Decision checklist

Before adding new data to the RCK DAG, ask:

1. Is this cognitive information that cannot be reproduced deterministically?
2. Can this be referenced by path, commit, state id, delta id, anchor id, or tool invocation?
3. Is this already stored in Git or the filesystem?
4. Is this source data or a derived view?
5. Will this improve LLM reasoning enough to justify token/storage cost?
6. Can this be recomputed during context-pack export?

If it can be referenced or recomputed, do not persist it as primary DAG data.

## Future implications

Planned and likely follow-ups:
- improve tool evidence to keep targets instead of long outputs
- implement `rfs show` for point inspection
- implement DAG validation
- keep full context-pack JSON as the canonical export
- add a compact context-pack mode later if the LLM needs a smaller projection
