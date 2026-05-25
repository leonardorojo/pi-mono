# RFS TraceSliceProposal LLM mode

`rfs trace-slice-proposal-llm "<prompt>"` is an experimental Pi-backed proposal-only path.
It does not write RCK, does not emit a final TraceSlice, and does not materialize a ContextPack.

## Boundary

The command asks Pi for a single JSON object shaped as `rufus.trace-slice-proposal`.
RFS performs strict structural checks on the returned JSON and does not attempt to repair malformed output.
If the output is not valid JSON, if markdown fences or extra prose are present, or if `type != "rufus.trace-slice-proposal"`, the command fails.
The proposal output is also checked for basic contamination patterns such as raw diffs, stdout/stderr dumps, JSONL event fragments, and obvious leaked internals.

## Input contract

The LLM receives a compact request containing:

- the user prompt;
- the deterministic intent projection;
- a quick DAG index with recent state/delta IDs and anchor metadata;
- metadata-only artifact summaries;
- a locked materialization policy;
- anti-black-box rules.

It does not receive file contents, diffs, stdout/stderr, raw JSONL, or `.rfs` internals.

## Output contract

The LLM must return only JSON with the proposal shape:

```json
{
  "type": "rufus.trace-slice-proposal",
  "schemaVersion": 1,
  "prompt": {
    "text": "...",
    "isExcerpt": false
  },
  "intent": {
    "kind": "...",
    "summary": "...",
    "source": "..."
  },
  "requestedSelection": {
    "stateIds": [],
    "deltaIds": [],
    "anchorIds": [],
    "artifactRefs": []
  },
  "requestedMaterializationPolicy": {
    "includeStatePayloads": true,
    "includeDeltaDecodedOps": true,
    "includeArtifactContents": false,
    "includeGitDiffs": false,
    "includeStdoutStderr": false,
    "includeJsonl": false
  },
  "rationale": [],
  "confidence": 0.0,
  "warnings": []
}
```

## Validation rule

The proposal command is proposal-only.
RFS remains the authority that validates a proposal into a final TraceSlice.
The Complete-mode pipeline reuses the same LLM-backed proposal stage at [2/5] with `PiTraceSliceProposalAgent` on `claude-sonnet-4.5`, but validation still remains mandatory and authoritative.
If `rfs trace-slice-validate-llm` is enabled in the CLI, it must still pass the LLM proposal through `RckTraceSliceProposalValidator` before emitting any final TraceSlice JSON.

## Non-goals

- No TraceSlice finalization in the proposal command.
- No ContextPack materialization in the proposal command.
- No RCK writes.
- No ModelRouter.
- No file contents, diffs, stdout/stderr, or JSONL in the prompt payload.
