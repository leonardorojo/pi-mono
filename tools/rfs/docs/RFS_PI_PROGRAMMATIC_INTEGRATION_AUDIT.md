# RFS / Pi programmatic integration audit

Status: analysis-only microphase.

Branch: `feature/rfs-pi-programmatic-integration-audit`, created from `feature/rufus-cli-design`.

Scope constraints:

- Do not implement RPC in this phase.
- Do not implement JSON Event Stream / JSONL in this phase.
- Do not change functional behavior.
- Do not modify `Rufus.RCK.Core`.
- Do not touch `packages/` or `.pi/` except for read-only inspection.
- Do not remove the current bridges.
- Do not break `rfs pi`.

Document location note: this audit lives under `tools/rfs/docs/` instead of `docs/rufus-cli/` because the current RFS documentation root in this repository is `tools/rfs/docs/` (`RCK_DAG_PRINCIPLES.md` already lives there), and `tools/rfs/README.md` / `tools/rfs/TODO.md` are local to the RFS proof of concept.

## Evidence inspected

Repository evidence:

- `tools/rfs/src/Rufus.Cli/Program.cs`
- `tools/rfs/bridge/rfs-ask.mjs`
- `tools/rfs/bridge/rfs-agent.mjs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckInteractionRecorder.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckInteractionRecord.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckInteractionTool.cs`
- `tools/rfs/README.md`
- `tools/rfs/TODO.md`
- `packages/coding-agent/docs/rpc.md` (read-only)
- `packages/coding-agent/docs/json.md` (read-only)
- `packages/coding-agent/docs/sdk.md` (read-only)
- `pi --help` on the local machine (read-only CLI discovery)

Branch caveat:

- The requested base branch, `feature/rufus-cli-design`, does not contain the newer `rfs model get|set|list` implementation. It contains `rfs pi`, `rfs ask`, and `rfs agent`.
- The caller-provided known state matches the sibling branch `feature/rfs-model-config`. That branch adds `RckWorkspaceModelConfigStore`, `rfs model get`, `rfs model set <model>`, deferred `rfs model list`, and `ApplyWorkspaceModelEnvironment(...)` for `rfs pi`, `rfs ask`, and `rfs agent`.
- This audit therefore separates: confirmed-on-this-base evidence versus known model-config behavior from the sibling branch and request context. No code from that sibling branch is merged here.

External docs considered conceptually:

- Pi SDK: https://pi.dev/docs/latest/sdk
- Pi RPC mode: https://pi.dev/docs/latest/rpc
- Pi JSON Event Stream mode: https://pi.dev/docs/latest/json

The local `packages/coding-agent/docs/*` files mirror the same relevant product documentation and provide concrete protocol details for this checkout.

## 1. Current RFS -> Pi inventory

### `rfs pi [message]`

- C# entrypoint: `tools/rfs/src/Rufus.Cli/Program.cs`, `if (args[0] == "pi")`.
- Helper / bridge: none.
- External command: `pi`, with optional single argument equal to `string.Join(" ", args.Skip(1))`.
- Working directory: inherited current process working directory.
- Environment variables:
  - On `feature/rufus-cli-design`: none set by RFS.
  - In the known `feature/rfs-model-config` path: `ApplyWorkspaceModelEnvironment(psi)` sets `RUFUSCHAT_LLM_MODEL` if `.rfs/config.json` contains `llm.defaultModel`.
- Interactive/headless: interactive passthrough. `UseShellExecute = false`; stdout/stderr are not redirected, so Pi owns the terminal.
- Uses configured model:
  - On this base: no.
  - With model-config branch behavior: yes, via `RUFUSCHAT_LLM_MODEL`.
- Registers in RCK: no.
- Output: whatever `pi` prints or renders directly; RFS returns Pi's exit code.
- Current evidence: no RCK state/delta/evidence. Only process exit code is propagated.
- Fragility / risks:
  - It intentionally enters Pi/TUI territory; it should not be mixed with RPC framing.
  - No structured output contract.
  - No timeout/cancellation wrapper in RFS.
  - No dedicated error detail beyond `Failed to start pi.` if process launch fails.

Recommendation: keep as passthrough/TUI for now. It is the right escape hatch for direct Pi use.

### `rfs ask <prompt>`

- C# entrypoint: `Program.cs`, `if (args[0] == "ask")`.
- Helper / bridge: `tools/rfs/bridge/rfs-ask.mjs`.
- External command: `node <helperPath> <prompt>`.
- Working directory:
  - C# does not set `WorkingDirectory` for ask, so the child inherits the caller's cwd.
  - The helper computes `repoRoot = path.resolve(helperDir, '..', '..', '..')`, i.e. the pi-mono checkout root for importing packages, not the external repo root.
- Environment variables:
  - Helper reads `RUFUSCHAT_LLM_PROVIDER` and `RUFUSCHAT_LLM_MODEL`.
  - Helper falls back to `~/.pi/agent/settings.json` (`defaultProvider`, `defaultModel`) and hardcoded defaults `github-copilot` / `gpt-5.4-mini`.
  - Helper reads provider API keys from environment via `getEnvApiKey(provider)` or from `~/.pi/agent/auth.json` including OAuth refresh support.
  - In known model-config branch behavior, C# sets `RUFUSCHAT_LLM_MODEL` from `.rfs/config.json` before starting the helper.
- Interactive/headless: headless one-shot prompt. No tools.
- Uses configured model:
  - Helper can use `RUFUSCHAT_LLM_MODEL`.
  - Current base does not populate it; model-config branch does.
- Registers in RCK: no for plain `rfs ask`.
- Output:
  - C# prints a human header (`Rufus Ask`, prompt, `Answer`) then writes helper stdout as free text.
  - Helper streams `text_delta` directly to stdout and writes a final newline.
  - Helper writes errors to stderr and returns exit code 1 on failure.
- Current evidence: none in RCK for plain ask.
- Fragility / risks:
  - Free-text stdout is both user UI and answer capture surface.
  - C# cannot distinguish final answer, provider metadata, usage, retry events, or partial failures.
  - stderr is passed through as text.
  - The helper bypasses the Pi CLI modes and calls Pi AI package APIs directly.

Recommendation: migrate to Pi JSON Event Stream mode before RPC. It is one-shot and wants structured streaming/final-answer extraction.

### `rfs ask --record <prompt>`

- C# entrypoint: same ask branch in `Program.cs`; `recordInteraction` is derived from `--record`.
- Helper / bridge: `rfs-ask.mjs`.
- External command: `node <helperPath> <prompt>`.
- Environment variables: same as `rfs ask`.
- Interactive/headless: headless one-shot prompt.
- Uses configured model: same as `rfs ask`.
- Registers in RCK: yes, only after helper exit code 0.
- RCK classes involved:
  - `RckInteractionRecorder.RecordAsk(prompt, finalAssistantAnswer)`.
  - `RckInteractionRecord.CreateAsk(...)` creates mode `ask`, answer, and a 240-character `AnswerSummary` with inline-code redaction.
  - `RckInteractionRecorder` writes a new state, delta, updates `.rfs/HEAD`, and optionally creates a git-commit anchor if the commit changed.
- Output:
  - Same human ask output as `rfs ask`.
  - Then record result summary lines from `RckInteractionRecordResult.FormatConsoleLines()`.
- Current evidence:
  - State payload `type = "rufus.interaction-state"` contains `interaction.mode`, `prompt`, `answerSummary`, current git branch/commit/dirty state, and changed artifacts.
  - Delta payload `type = "rufus.interaction-delta"` contains `change`, `cause` with prompt and answer summary, and `evidence.artifacts`.
  - `evidenceRefs` currently reference changed artifacts only, not the LLM event stream.
- Fragility / risks:
  - Records the answer by trimming the helper's full stdout text, so any non-answer stdout contaminates RCK.
  - Does not store provider/model/usage/stop reason/tool events for ask.
  - No structured link to an upstream Pi session/event id.

Recommendation: strong JSON Event Stream candidate. The first useful improvement is structured final answer plus optional provider/model/usage metadata, without changing RCK schema yet.

### `rfs agent <task>`

- C# entrypoint: `Program.cs`, `if (args[0] == "agent")`.
- Helper / bridge: `tools/rfs/bridge/rfs-agent.mjs`.
- External command: `node <helperPath> <task>`.
- Working directory:
  - C# sets `WorkingDirectory = Directory.GetCurrentDirectory()`.
  - C# sets `RFS_REPO_ROOT = Directory.GetCurrentDirectory()`.
  - Helper uses `RFS_REPO_ROOT` if present; otherwise `process.cwd()`.
- Environment variables:
  - `RFS_REPO_ROOT` from C#.
  - Helper reads `RUFUSCHAT_LLM_PROVIDER` / `RUFUSCHAT_LLM_MODEL` or falls back to Pi settings and defaults.
  - Helper reads provider keys from environment or `~/.pi/agent/auth.json`.
  - In known model-config branch behavior, C# also sets `RUFUSCHAT_LLM_MODEL` from `.rfs/config.json`.
- Interactive/headless: headless streaming agent with read-only tools.
- Uses configured model: via helper defaults/env; model-config branch passes workspace model.
- Registers in RCK: no for plain `rfs agent`.
- Output:
  - C# renders a human-friendly header (`Rufus Agent`, task, mode/scope, actions), action lines, then final answer.
  - Helper emits sentinel text lines on stdout:
    - `[agent:start] ...`
    - `[tool:start] id=... name=... ...`
    - `[tool:end] id=... name=... ...`
    - `[assistant] ...`
    - `[agent:end]`
  - C# parses those sentinel prefixes and formats them for the user.
- Helper implementation:
  - Uses `@earendil-works/pi-agent-core` `Agent` directly, not `pi --mode json` or `pi --mode rpc`.
  - Defines only two read-only tools: `list_directory` and `read_file`.
  - Restricts paths to stay under `repoRoot`.
  - Uses `toolExecution: 'sequential'`.
- Current evidence: none in RCK for plain agent.
- Fragility / risks:
  - Sentinel text protocol is ad hoc and can be broken by accidental stdout content.
  - C# records only completed tool names in record mode, not tool args/results/status details.
  - Helper catch block references `assistantLineOpen`, which is not defined in the file; an error path may throw a secondary `ReferenceError`.
  - The helper uses SDK/direct package imports and custom tools, so behavior can drift from Pi CLI JSON/RPC modes.

Recommendation: migrate to JSON Event Stream as the first programmatic replacement if RFS wants one-shot agent behavior. Consider RPC later when RFS needs multi-command sessions or steering/follow-up.

### `rfs agent --record <task>`

- C# entrypoint: same agent branch in `Program.cs`; `recordInteraction` is derived from `--record`.
- Helper / bridge: `rfs-agent.mjs`.
- External command: `node <helperPath> <task>`.
- Environment variables: same as `rfs agent`.
- Interactive/headless: headless streaming agent with read-only tools.
- Uses configured model: same as `rfs agent`.
- Registers in RCK: yes, only after helper exit code 0 and after a final assistant answer is captured.
- RCK classes involved:
  - `RckInteractionRecorder.RecordAgent(task, finalAssistantAnswer, recordedTools)`.
  - `RckInteractionRecord.CreateAgent(...)` creates mode `agent`, answer summary, and tool list.
  - `RckInteractionTool.Completed(name)` currently captures only name + status.
- Output:
  - Same human-friendly agent output as `rfs agent`.
  - Then record result summary lines.
- Current evidence:
  - Same state/delta structure as ask record.
  - Delta `evidence.tools` contains simple `{ name, status }` entries only if tool-end sentinel lines were parsed.
  - Delta `evidenceRefs` currently reference changed artifacts, not Pi tool event payloads.
- Fragility / risks:
  - Final answer depends on sentinel parsing and string buffering.
  - Tool evidence loses `toolCallId`, args, result, error flag, partial output, and timing.
  - RCK evidence cannot reconstruct the agent turn or distinguish provider error / tool error / no-output failure.

Recommendation: strongest initial JSON Event Stream candidate because structured `tool_execution_*`, `message_*`, and `turn_end` events map naturally to RCK evidence opportunities.

### `rfs model get`

- C# entrypoint:
  - Not present on requested base branch `feature/rufus-cli-design`.
  - Present in sibling branch `feature/rfs-model-config`, `Program.cs`, `if (args[0] == "model")`, `args[1] == "get"`.
- Helper / bridge: none.
- External command: none.
- Workspace class on model-config branch: `RckWorkspaceModelConfigStore.Read()`.
- Environment variables used: none.
- Interactive/headless: headless/local.
- Uses configured model: reads local configured model; does not call Pi.
- Registers in RCK: no.
- Output on model-config branch:
  - `rfs model get`
  - `source: workspace` or `source: default (Pi/RFS)`
  - `model: <model>` or `(inherited)`
- Current evidence: local `.rfs/config.json` value only.
- Fragility / risks:
  - No validation that the configured model exists in Pi's active model registry.
  - No provider disambiguation if a bare model id is ambiguous.

Recommendation: keep local. It does not need Pi RPC unless later UX wants to show resolved provider/model metadata.

### `rfs model set <model>`

- C# entrypoint:
  - Not present on requested base branch.
  - Present in sibling branch `feature/rfs-model-config`, `args[1] == "set"`.
- Helper / bridge: none.
- External command: none.
- Workspace class on model-config branch: `RckWorkspaceModelConfigStore.SetDefaultModel(args[2])`.
- Config path: `.rfs/config.json` under `llm.defaultModel`; creates `.rfs` and preserves/sets `schemaVersion`.
- Environment variables used: none during set.
- Interactive/headless: headless/local mutation of `.rfs/config.json`.
- Uses configured model: writes it.
- Registers in RCK: no.
- Output on model-config branch:
  - `rfs model set`
  - `source: workspace`
  - `model: <model>`
  - `config: .rfs/config.json`
- Current evidence: `.rfs/config.json` contains the durable workspace value.
- Fragility / risks:
  - Does not validate model name against Pi.
  - Stores a single string, so provider/model/thinking parsing remains implicit.
  - If later validation uses RPC, it must not require starting a long-lived session for a simple local set unless the user asks for validation.

Recommendation: keep local for now. Later optionally validate/resolve through RPC `get_available_models` / `set_model` semantics or direct `pi --list-models` inspection.

### `rfs model list`

- C# entrypoint:
  - Not present on requested base branch.
  - Present as deferred behavior in sibling branch: writes `rfs model list is not implemented yet.` and returns 1.
- Helper / bridge: none today.
- External command: none today.
- Environment variables used: none today.
- Interactive/headless: intended headless/programmatic.
- Uses configured model: not applicable.
- Registers in RCK: no.
- Output: not implemented.
- Current evidence: none.
- Fragility / risks:
  - Without a programmatic Pi path, RFS would be tempted to scrape `/model` from TUI or duplicate package internals. Both should be avoided.

Recommendation: first clear RPC candidate. Pi RPC exposes `get_available_models`, which returns full Model objects.

## 2. Current bridges/helpers map

### `tools/rfs/bridge/rfs-ask.mjs`

- Invoked by: `rfs ask` and `rfs ask --record` in `Program.cs`.
- Arguments: prompt as CLI args joined by the helper (`process.argv.slice(2).join(' ').trim()`).
- Cwd/repo root:
  - Process cwd inherited from RFS caller.
  - Helper `repoRoot` is the pi-mono root derived from helper path for importing TypeScript source through `jiti`.
  - It does not set a target external repo root for tools because it has no tools.
- Environment variables:
  - Input: `RUFUSCHAT_LLM_PROVIDER`, `RUFUSCHAT_LLM_MODEL`.
  - Provider auth via standard Pi AI env vars (`getEnvApiKey`) and `~/.pi/agent/auth.json`.
- Pi usage type:
  - Does not invoke `pi` binary.
  - Uses Pi AI internals: `packages/ai/src/env-api-keys.ts`, `models.ts`, `stream.ts`, `oauth.ts` through `jiti`.
- Output type: free text assistant answer on stdout, streamed token deltas; errors on stderr.
- Structured output: no. It consumes structured internal events but discards them, preserving only text.
- Risk: direct internal package import and free-text stdout make it less stable as an integration contract than Pi JSON/RPC modes.

### `tools/rfs/bridge/rfs-agent.mjs`

- Invoked by: `rfs agent` and `rfs agent --record` in `Program.cs`.
- Arguments: task as CLI args joined by helper.
- Cwd/repo root:
  - C# sets cwd to caller cwd and `RFS_REPO_ROOT` to caller cwd.
  - Helper resolves `repoRoot` from `RFS_REPO_ROOT` or `process.cwd()`.
  - Tool paths are resolved under `repoRoot` and path escape is rejected.
- Environment variables:
  - Input: `RFS_REPO_ROOT`, `RUFUSCHAT_LLM_PROVIDER`, `RUFUSCHAT_LLM_MODEL`.
  - Provider auth via standard Pi AI env vars and `~/.pi/agent/auth.json`.
- Pi usage type:
  - Does not invoke `pi` binary, JSON mode, or RPC mode.
  - Uses `@earendil-works/pi-agent-core` `Agent` and `@earendil-works/pi-ai` directly.
  - Provides custom read-only `list_directory` and `read_file` tools.
- Output type:
  - Ad hoc sentinel-prefixed text lines on stdout.
  - Error messages on stderr.
- Structured output: semi-structured text only; not JSONL. C# parses prefixes, not JSON.
- Risk:
  - Sentinel protocol can collide with text output and loses rich event data.
  - Error path contains an apparent `assistantLineOpen` reference that is not declared.
  - RFS records only completed tool names, not full evidence.

## 3. Pi RPC mode analysis for RFS

Pi RPC mode starts a Pi process with:

```bash
pi --mode rpc [options]
```

The protocol is JSONL over stdin/stdout:

- RFS sends one JSON command per LF-delimited line on stdin.
- Pi emits JSON responses and asynchronous events on stdout.
- Commands can include an `id` for response correlation.
- Clients must split records on LF only and strip optional CR; generic readers that split on Unicode separators are explicitly called out as unsafe.

Important commands for RFS:

- `get_available_models`: returns full configured model objects.
- `set_model`: switches to a specific provider/model and returns the model object.
- `get_state`: returns active model/session/streaming state.
- `prompt`, `steer`, `follow_up`, `abort`: support multi-command session control.
- `get_last_assistant_text`: useful for final-answer extraction after a prompt completes.
- `get_messages`, `get_session_stats`, `new_session`, `switch_session`, `fork`, `clone`: useful for controlled sessions/subagents later.

When RPC is a good fit:

- RFS needs request/response operations against Pi state or registry.
- RFS needs more than one command against the same agent/session.
- RFS needs model discovery, model resolution, `set_model`, session state, stats, or controlled branching.
- RFS needs to support future subagents with steering/follow-up/cancel rather than just one-shot prompt execution.
- RFS needs process isolation from .NET while still speaking a stable protocol.

How RPC could serve `rfs model list`:

- Start `pi --mode rpc --no-session` with the right cwd and model scope options if needed.
- Send `{"id":"rfs-model-list-1","type":"get_available_models"}`.
- Read until the matching `response` with `command: "get_available_models"` and `success: true`.
- Render a stable table from `data.models`.
- Exit the process cleanly after response.

How RPC could serve model validation / `set_model`:

- For validation only, use `get_available_models` and match the user-provided pattern/string in RFS.
- For Pi's own resolver semantics, start RPC with the candidate model option or send `set_model` with provider/model id if already split.
- Do not replace local `.rfs/config.json` storage just to validate; keep local persistence in RFS and use RPC as an optional resolver.

How RPC could serve sessions/subagents:

- A long-lived `pi --mode rpc` process can host an RFS-controlled session.
- RFS can send `prompt`, then `steer` / `follow_up`, inspect `get_state`, call `get_session_stats`, and abort if needed.
- RFS can correlate event streams and responses by ids and event order, then decide what becomes RCK evidence.

Lifecycle implications:

- RFS must own process start, stdin writer, stdout JSONL reader, stderr reader, cancellation, graceful shutdown, and forced kill fallback.
- RFS must keep stdout dedicated to protocol JSONL; stderr should be captured separately and summarized safely.
- RFS must define timeouts per command and overall process lifetime.
- RFS must decide whether each command uses a short-lived process (`model list`) or a long-lived session process (subagents/sessions).
- Exit code matters, but for accepted prompts many failures are emitted as events after the initial successful `prompt` response, so exit code and command response are not enough.

.NET compatibility:

- Straightforward with `System.Diagnostics.Process`, redirected stdin/stdout/stderr, async reads, cancellation tokens, and `System.Text.Json`.
- Avoid `ReadLineAsync` if strict LF-only semantics or Unicode separator safety matters; implement a byte/char buffer that splits only on `\n`, strips trailing `\r`, and parses each JSON object.
- Use request ids to correlate responses; events do not carry ids.

Advantages:

- Stable programmatic protocol, not TUI scraping.
- Full model objects for `rfs model list`.
- Model/session/state commands without duplicating Pi internals.
- Future multi-turn, subagent, steering, cancellation, stats, and session management.
- Process isolation from C#.

Risks:

- More lifecycle complexity than JSON mode.
- Bidirectional JSONL parsing can deadlock if stdin/stdout/stderr are not handled concurrently.
- Prompt acceptance response is not final answer; RFS must continue reading events until completion.
- Extension UI sub-protocol may require RFS responses or explicit avoidance/timeout policy.
- Long-lived workers must not become orphaned.
- Must never mix this with interactive TUI `rfs pi`.

## 4. Pi JSON Event Stream mode analysis for RFS

Pi JSON Event Stream mode runs a one-shot prompt:

```bash
pi --mode json "Your prompt"
```

It emits JSON lines to stdout. The first line is a session header, followed by agent/session events such as:

- `agent_start`
- `turn_start`
- `message_start`
- `message_update`
- `message_end`
- `turn_end`
- `tool_execution_start`
- `tool_execution_update`
- `tool_execution_end`
- `agent_end`
- retry/compaction/queue events when applicable

When JSON mode is a good fit:

- RFS wants a one-shot ask/agent command.
- RFS wants streaming user output but does not need to send additional commands after start.
- RFS wants structured final-answer extraction without maintaining a bidirectional RPC client.
- RFS wants structured tool evidence for RCK record mode.
- RFS wants a direct Pi CLI integration instead of custom Node helpers.

Fit for `rfs ask`:

- Use `pi --mode json --no-tools` or equivalent options if the command must remain no-tools.
- Pass the configured model using Pi's `--model` option or current environment convention, preserving `.rfs/config.json` semantics.
- Render `message_update.assistantMessageEvent.text_delta` to the user.
- Extract final answer from `message_end` or `agent_end.messages` assistant content.

Fit for `rfs agent`:

- Use `pi --mode json` with an explicit read-only tool set if the current semantics must remain read-only (`read`, `grep`, `find`, `ls`) or a narrower equivalent.
- Avoid edit/write tools unless a later phase intentionally changes behavior.
- Render structured `tool_execution_*` events to action lines.
- Record toolCallId/name/args/result/isError in future evidence without scraping sentinel text.

How it improves RCK evidence:

- `message_end` can provide final assistant message, provider, model, usage, stop reason, and timestamp.
- `tool_execution_start/end` can provide tool name, args, results, and error flag.
- `turn_end` can group assistant message and tool results.
- `agent_end` can serve as the completion boundary.
- RFS can keep the current state/delta schemas initially while storing richer evidence only after a later schema decision.

Streaming considerations:

- Parse JSONL line by line from stdout.
- Render selected events to the terminal while retaining structured records in memory for `--record`.
- Treat stderr separately; do not merge it into JSON parsing.
- Define final-answer precedence: prefer the completed assistant message from `message_end` / `turn_end`; fallback to accumulated text deltas only if needed.

Risks:

- JSONL framing must be strict; partial lines are normal during streaming.
- Logs on stdout would break parsing if Pi is misconfigured; stderr must remain separate.
- Tool event payloads may be large; RFS should summarize safely for human output and preserve only approved evidence in RCK.
- Exit code alone is insufficient; partial events may exist before failure.
- `lrfs` wrappers must preserve cwd and env so Pi runs in the external repo, not the pi-mono source root.

## 5. Pi SDK analysis

The SDK is Node/TypeScript-oriented and exposes direct agent/session/runtime APIs. The local docs state that SDK use is preferred when:

- the caller is in the same Node.js process;
- type safety is desired;
- direct access to agent state is required;
- tools/extensions are customized programmatically.

For this repo:

- RFS is a C#/.NET CLI. Making the SDK the central integration layer would move core orchestration logic into TypeScript helpers and increase cross-language ownership complexity.
- The current helpers already demonstrate both the benefit and the risk: `rfs-agent.mjs` can define custom read-only tools, but RFS then depends on direct package internals and an ad hoc text protocol.
- SDK can still be useful for narrow helper prototypes or custom tool experiments, but the durable RFS/Pi boundary should prefer Pi's documented CLI protocols.

SDK vs RPC:

- SDK is best inside Node and when RFS intentionally delegates an entire behavior to a Node helper.
- RPC is better for C# because it is language-agnostic, process-isolated, and documented as the subprocess integration path.
- JSON mode is best for one-shot C# commands that need structured events but no bidirectional control.

Recommendation: do not make SDK the primary RFS integration. Keep it as reference material and possibly for temporary prototypes only.

## 6. Recommended classification by command

| Command RFS | Current state | Current pain | Best future path | Reason | Priority | Risk | Notes |
|---|---|---|---|---|---|---|---|
| `rfs pi [message]` | Direct `pi` passthrough; interactive/TUI; optional configured model only on model-config branch | No structured output, no RCK evidence | keep TUI/passthrough | This command is the intentional manual Pi escape hatch | Low | Low if left alone; high if mixed with RPC stdout | Do not scrape TUI |
| `rfs ask <prompt>` | C# -> `node rfs-ask.mjs` -> Pi AI internals; free-text stdout | Answer capture depends on text stream; no model/usage metadata | migrate to JSONL | One-shot prompt maps well to `pi --mode json`; no bidirectional control needed | High | Medium | Preserve no-tools semantics |
| `rfs ask --record <prompt>` | Same as ask plus `RckInteractionRecorder.RecordAsk` | RCK records answer summary from free stdout only | migrate to JSONL | Structured final answer and metadata improve record quality | High | Medium | Do not change schema in first migration |
| `rfs agent <task>` | C# -> `node rfs-agent.mjs`; custom read-only tools; sentinel stdout | Ad hoc sentinel protocol; limited event detail | migrate to JSONL first; RPC later if sessions needed | One-shot agent stream maps to JSON mode events | Medium | Medium/High | Must preserve read-only tool boundary |
| `rfs agent --record <task>` | Same as agent plus simple tool-name evidence in RCK | Tool evidence loses args/results/error/timing | migrate to JSONL first; possible RPC later | JSON events directly expose tool execution evidence | High | Medium/High | Strongest evidence-improvement candidate |
| `rfs model get` | Local `.rfs/config.json` read on model-config branch | No Pi validation/resolution | no action / local | Reading local configured model does not require Pi | Low | Low | Optionally display resolved info later |
| `rfs model set <model>` | Local `.rfs/config.json` write on model-config branch | No validation; ambiguous model strings | hybrid later | Keep local persistence; optionally validate via RPC model list | Medium | Medium | Do not require Pi online for simple local set unless explicit |
| `rfs model list` | Deferred/not implemented on model-config branch | No programmatic model inventory | migrate to RPC | RPC `get_available_models` is designed for this | Highest | Medium | First RPC microphase |
| Future Intent / TraceSlice / Principal subagents | Not implemented | Need controlled sessions, steering, branch/fork, stats, evidence | migrate to RPC or SDK bridge after experiments | Requires multi-command control and lifecycle ownership | Later | High | Decide after JSONL evidence prototype |

## 7. RCK implications

Current `RckInteractionRecorder` creates:

- a new state with interaction summary and git context;
- a delta with `/interaction`, `/git`, and optional `/artifacts` change summaries;
- `Delta.evidence.tools` for agent mode, currently only `{ name, status }`;
- `Delta.evidence.artifacts` from git status;
- `Delta.evidenceRefs` for changed artifacts;
- optional git-commit anchor when commit changed.

Potential improvements from JSONL/RPC, without committing to schema changes yet:

- `RckInteractionRecord` could eventually include provider, model id, stop reason, usage, and Pi session/event ids.
- `Delta.evidence.tools` could become grounded in `tool_execution_start`, `tool_execution_update`, and `tool_execution_end` events instead of sentinel text.
- `Delta.evidenceRefs` could reference persisted tool output artifacts or event logs when approved, not only changed files.
- `agent --record` could distinguish:
  - assistant answer;
  - tool calls;
  - tool results;
  - tool errors;
  - provider/retry/compaction events;
  - final success versus partial failure.
- Artifact refs could point to files changed during an interaction while structured tool evidence explains how those artifacts were observed or produced.
- Future TraceSlice agents could use RPC session ids and event ranges as audit handles while keeping the persistent DAG separate from the ephemeral Context Pack.
- Context Pack should remain an export projection over the DAG, not the storage source of truth. JSONL/RPC event capture should not silently inject context into future prompts.

Risks for RCK:

- Capturing full tool outputs can leak raw stdout/stderr or repository secrets if not filtered.
- Storing full event logs may bloat `.rfs` quickly.
- Schema changes made too early could lock RFS into Pi-specific event shapes.
- Evidence refs need stable artifact identity; raw JSONL line offsets alone may not be enough.
- `Rufus.RCK.Core` should remain pure; protocol-specific parsing belongs in CLI/workspace layers or a future adapter, not Core.

## 8. Recommended incremental order

A. Land this audit document only.

B. Implement `rfs model list` using a small RPC client and `get_available_models`.

- Short-lived `pi --mode rpc --no-session` process.
- No RCK writes.
- Strict stdout JSONL parsing and stderr separation.
- Keep output human-friendly but sourced from structured Model objects.

C. Prototype an internal JSONL reader for `pi --mode json` with `ask` in a non-default/dev path.

- Prove final-answer extraction.
- Prove stderr/stdout separation.
- Prove configured model propagation.
- Prove external repo cwd through `lrfs`.

D. Migrate `rfs ask` to JSON Event Stream mode.

- Preserve current console shape as much as possible.
- Preserve no-tools behavior.

E. Migrate `rfs ask --record` to use structured final answer.

- Keep current RCK schema initially.
- Optionally attach approved metadata only after a separate schema discussion.

F. Migrate `rfs agent` to JSON Event Stream mode.

- Preserve read-only behavior.
- Render tool events from JSON events, not sentinel text.

G. Improve `rfs agent --record` evidence from structured tool events.

- Still avoid definitive schema redesign until evidence shape stabilizes.

H. Evaluate RPC for controlled subagents.

- Long-lived process lifecycle.
- Steering/follow-up/abort.
- Session/fork/clone/stat commands.
- Extension UI policy.

I. Design model routing by role.

- Principal / Intent / TraceSlice model roles.
- Local config shape.
- Validation/resolution against Pi model registry.

J. Only then advance TraceSlice agents.

- Keep TraceSlice design dependent on proven JSONL/RPC behavior and RCK evidence boundaries.

## 9. Technical risks and guardrails

- Do not mix TUI and RPC. `rfs pi` should remain a passthrough and not share stdout parsing code with RPC.
- Do not scrape Pi TUI or `/model` interactive output.
- Do not depend on free-text helper output as the durable integration contract.
- JSONL must be parsed line by line with strict LF framing; partial lines are normal.
- stdout and stderr must remain separate; logs on stdout can corrupt JSONL.
- RFS must drain stdout and stderr concurrently to avoid deadlocks.
- Process lifecycle must include startup failure, readiness assumptions, command timeout, cancellation, graceful shutdown, and kill fallback.
- Exit codes must be combined with structured error events/responses; neither is sufficient alone.
- Partial failures can occur after initial RPC `prompt` acceptance.
- RPC extension UI requests need a policy: reject, auto-timeout, or explicitly support selected methods.
- Linux Mint and WSL path/cwd behavior must be validated separately if `lrfs` is used across repositories.
- Preserve `lrfs`: cwd must remain the external repository root, not the pi-mono checkout root.
- Preserve configured model behavior: `.rfs/config.json` `llm.defaultModel` should continue to influence `ask`, `agent`, and `pi` once that branch is present.
- Avoid touching `.pi/` runtime state during tests except normal Pi auth/session reads caused by Pi itself; prefer `--no-session` for model-list RPC.
- Avoid touching `packages/` during RFS integration phases except read-only inspection.
- Do not contaminate `Rufus.RCK.Core` with Pi protocol concerns.
- Do not break `rfs pi`; keep it as the interactive fallback.
- Bound tool evidence size and redact sensitive raw outputs before storing anything durable.
- Keep documentation explicit about Project / Chat Session / Context Pack / RCK Trace boundaries; no silent context injection.

## 10. Decision recommendation

Implement first:

1. `rfs model list` via Pi RPC `get_available_models`.
2. A narrow JSONL reader prototype for `rfs ask` using `pi --mode json`.
3. `rfs ask` and `rfs ask --record` migration to JSON Event Stream after the prototype proves final-answer extraction and configured-model propagation.
4. `rfs agent` / `rfs agent --record` migration to JSON Event Stream after read-only tool parity is proven.

Do not implement yet:

- Full RPC session/subagent orchestration.
- TraceSlice agents.
- Definitive RCK schema changes for Pi events.
- SDK-centered C# integration.
- TUI scraping or `/model` scraping.

Validate experimentally before migration:

- `pi --mode rpc --no-session` model discovery in the external repo cwd.
- `pi --mode json` final-answer extraction for text-only ask.
- JSONL parsing with stderr noise, provider errors, timeout, cancellation, and non-zero exit.
- Model propagation from `.rfs/config.json` to Pi's `--model` or environment path.
- `lrfs` wrapper cwd/env preservation.
- Read-only agent tool parity and no accidental edit/write tools.

Maintain as-is:

- `rfs pi [message]` as interactive passthrough.
- `rfs model get` as local config read.
- `rfs model set <model>` as local config write until validation/resolution is explicitly added.
- Existing bridges until JSONL replacements are implemented and verified in later microphases.

Final classification:

- RPC: best for `rfs model list`, future model resolution, and later controlled sessions/subagents.
- JSON Event Stream: best for one-shot `rfs ask` / `rfs agent` flows and their `--record` variants.
- SDK: reference or temporary Node-helper tool, not the primary durable RFS integration boundary.
- TUI passthrough: keep for `rfs pi` only.
