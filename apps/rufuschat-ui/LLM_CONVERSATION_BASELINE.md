# Phase 17 — LLM conversation baseline

## Goal

Baseline conversacional simple para RufusChat usando el LLM provider existente de Pi Agent.

## Existing Pi LLM provider

### Contract entrypoint

The reusable Pi Agent LLM contract is the package-level dispatcher in:

- `packages/ai/src/stream.ts`

It exposes:

- `stream(model, context, options)`
- `complete(model, context, options)`
- `streamSimple(model, context, options)`
- `completeSimple(model, context, options)`

These functions route through the registered provider for `model.api`, so RufusChat should consume this adapter instead of creating a parallel OpenAI client.

### Concrete provider modules inspected

The concrete provider implementations that define the chat-completion contract are:

- `packages/ai/src/providers/openai-responses.ts`
- `packages/ai/src/providers/openai-codex-responses.ts`
- `packages/ai/src/providers/openai-responses-shared.ts`

Relevant exports:

- `streamOpenAIResponses`
- `streamSimpleOpenAIResponses`
- `streamOpenAICodexResponses`
- `streamSimpleOpenAICodexResponses`
- `convertResponsesMessages`
- `convertResponsesTools`
- `processResponsesStream`

### How it is instantiated

The provider registry lives in:

- `packages/ai/src/providers/register-builtins.ts`

It lazy-loads the provider modules and registers them through `registerApiProvider()`.

The provider package also exposes subpath exports in:

- `packages/ai/package.json`

For example:

- `./openai-responses`
- `./openai-codex-responses`

### Request shape

The Pi AI contract takes:

- `model`: a `Model<Api>` object with `provider`, `api`, `id`, `baseUrl`, headers, and capability flags
- `context`: `{ systemPrompt?: string; messages: Message[]; tools?: Tool[] }`
- `options`: provider options plus generic stream options

Important request fields observed in the provider modules:

- `apiKey`
- `temperature`
- `maxTokens`
- `signal`
- `transport`
- `cacheRetention`
- `sessionId`
- `onPayload`
- `onResponse`
- `headers`
- `timeoutMs`
- `maxRetries`
- `metadata`

Provider-specific options inspected:

- `OpenAIResponsesOptions`
- `OpenAICodexResponsesOptions`

### Response shape

The public result is an `AssistantMessage` with:

- `role: "assistant"`
- `content: (TextContent | ThinkingContent | ToolCall)[]`
- `api`
- `provider`
- `model`
- `responseModel?`
- `responseId?`
- `usage`
- `stopReason`
- `errorMessage?`
- `timestamp`

The streaming protocol emits:

- `start`
- `text_start` / `text_delta` / `text_end`
- `thinking_start` / `thinking_delta` / `thinking_end`
- `toolcall_start` / `toolcall_delta` / `toolcall_end`
- `done`
- `error`

### Configuration needed

Observed configuration paths:

- Standard OpenAI / Responses: `OPENAI_API_KEY`
- Codex / ChatGPT subscription flow: explicit OAuth token passed as `apiKey` or provider-specific auth flow

The package-level env helper is:

- `packages/ai/src/env-api-keys.ts`

It exposes:

- `findEnvKeys(provider)`
- `getEnvApiKey(provider)`

For the standard OpenAI path, `getEnvApiKey("openai")` resolves `OPENAI_API_KEY`.

For Codex, the provider expects a bearer token, extracts the account id from the JWT payload, and sends Codex-specific headers.

### Streaming support

Yes.

- `openai-responses` streams through the OpenAI Responses API.
- `openai-codex-responses` supports both SSE and WebSocket transports.

Codex transport details:

- `transport: "sse" | "websocket" | "auto"`
- `sessionId` enables session reuse and prompt caching
- WebSocket connections are reused per session and expire after 5 minutes of inactivity

### Errors and failure handling

Observed error behavior:

- Missing auth throws immediately in `streamSimple()` and is surfaced as an error message
- HTTP/network retries are built in for transient failures
- 429 / 5xx responses are retried with exponential backoff
- provider errors are normalized into `AssistantMessage.stopReason = "error"` or `"aborted"`
- Codex has explicit `CodexApiError` and `CodexProtocolError` paths
- Codex WebSocket failures can fall back to SSE for a session
- Friendly usage-limit messaging is parsed from error payloads where possible

### Limitations

- This is a chat transcript provider contract, not a Trace DAG contract
- It does not include RufusChat ProductState persistence by itself
- It does not provide semantic memory
- It does not expose RCK or Hermes tools for this baseline
- Codex auth is subscription/OAuth driven and not the same as an ordinary API-key flow
- RufusChat should not call the provider directly from the browser

## Proposed RufusChat integration

RufusChat UI
→ POST `/api/chat/complete`
→ RufusChat backend adapter
→ Pi Agent LLM provider (`packages/ai` `stream` / `complete`)
→ assistant message
→ ProductState persistence (17C+)

Adapter rules:

- keep the browser UI free of raw LLM credentials
- call the Pi AI package from the backend only
- keep the backend adapter thin and deterministic
- leave ProductState persistence for the follow-up integration step
- keep Trace / RCK / Hermes out of the first baseline

## Non-goals

- No RCK real
- No Hermes real
- No tools
- No semantic memory
- No Trace DAG
- No raw evidence
- No direct UI-to-LLM calls
- No new parallel LLM client
- No ProductState schema redesign in this subphase

## Proposed subphases

- 17B — backend chat completion endpoint
- 17C — frontend send-message integration
- 17D — conversation context window
- 17E — UX hardening
- 17F — validation and merge

## Notes for implementation

For the first baseline, prefer the existing Pi AI package API over a bespoke SDK call. The cleanest path is:

1. Resolve the configured model/provider in the backend.
2. Build a `Context` from the current RufusChat transcript.
3. Call `stream()` or `complete()` from `packages/ai`.
4. Persist the returned assistant message together with the user message.
5. Keep the transport adapter small so provider changes stay isolated.

## 17B Backend chat completion endpoint

### Endpoint

- `POST /api/chat/complete`

### Request v0

```json
{
  "projectId": "string",
  "chatId": "string",
  "messages": [
    { "role": "user", "content": "..." },
    { "role": "assistant", "content": "..." }
  ],
  "options": {
    "model": "optional string"
  }
}
```

### Response v0

```json
{
  "message": {
    "role": "assistant",
    "content": "..."
  },
  "metadata": {
    "provider": "pi-ai",
    "model": "...",
    "createdAt": "..."
  }
}
```

### Provider used

- Backend adapter: `apps/rufuschat-ui/chat-completion-provider.mjs`
- Contract entrypoint: `packages/ai/src/stream.ts`
- Call path: `completeSimple(model, context, options)`
- Non-persistent in 17B: the endpoint returns the assistant message, but ProductState writes are deferred to 17C
- Runtime import note: the backend uses `packages/ai/dist/stream.js`, `packages/ai/dist/env-api-keys.js`, and `packages/ai/dist/models.js` directly because the package entrypoint currently pulls `@sinclair/typebox` in this checkout under plain `node`

### Configuration required

- `OPENAI_API_KEY` for the default OpenAI provider path
- `RUFUSCHAT_LLM_PROVIDER` and `RUFUSCHAT_LLM_MODEL` can override the provider/model selection
- Default model baseline: `openai/gpt-4.1-mini`
- No API key is accepted from the browser request body

### Fallback / error behavior

- Missing or invalid config returns a product-friendly JSON error
- Invalid request bodies return `invalid_request`
- Empty assistant text returns `llm_empty_response`
- Provider failures return `llm_unavailable`
- The server does not crash on LLM errors
- This is a non-streaming baseline

### Non-goals for 17C

- No streaming
- No RCK
- No Hermes
- No tools
- No semantic memory
- No trace DAG
- No raw evidence exposure
- No direct browser-to-LLM calls

## 18B Streaming response v0

### Goal

Make normal RufusChat replies feel progressively streamed while keeping the same lightweight memory boundary.

### Endpoint

- `POST /api/chat/stream`
- SSE response with `Content-Type: text/event-stream`

### SSE events v0

- `start` — metadata about the chosen provider/model
- `delta` — incremental assistant text
- `done` — final assistant message plus metadata
- `error` — product-friendly error payload

### Fallback behavior

- `/api/chat/complete` remains available as the non-streaming fallback
- Streaming failures surface product-friendly errors instead of provider internals
- The browser does not call the LLM directly

### Persistence

- The UI keeps a transient assistant placeholder during streaming
- The final assistant message is persisted once the stream finishes
- The ProductState schema is unchanged

### Non-goals for 18B

- No cancel yet
- No RCK
- No Hermes
- No tools
- No semantic memory
- No embeddings
- No RAG
- No Trace DAG
- No automatic summaries
- No memory rewrite agents

## 17D Configured Pi LLM provider

### Goal

Use the Pi Agent configured provider/model instead of assuming OpenAI.

### What RufusChat resolves

- provider: `github-copilot`
- model: `gpt-5.4-mini`
- fallback source: `~/.pi/agent/settings.json`
- auth source: `~/.pi/agent/auth.json`

### Resolution rules

- `RUFUSCHAT_LLM_PROVIDER` and `RUFUSCHAT_LLM_MODEL` override the Pi defaults when set
- otherwise RufusChat reads the Pi Agent settings file and uses the configured default provider/model
- the adapter then resolves the matching API key/token from the existing Pi auth storage
- if the provider is `github-copilot`, the stored GitHub Copilot OAuth credential is reused through the Pi AI OAuth provider contract

### Difference from the old OpenAI-only path

- RufusChat no longer tells the user to set `OPENAI_API_KEY` when the configured Pi provider is GitHub Copilot
- OpenAI-specific wording is only relevant if the selected provider is actually OpenAI
- the endpoint stays behind the same `POST /api/chat/complete` contract; only provider resolution changes

### Error behavior

- missing Copilot auth becomes: `LLM provider is not configured. Check the Pi Agent GitHub Copilot authentication.`
- missing or invalid provider/model combinations remain product-friendly and do not expose tokens
- secrets are never logged or persisted in ProductState

### Non-goals

- No secrets in the UI
- No new LLM client
- No RCK
- No Hermes
- No streaming

### Non-goals for 17C

- No streaming
- No RCK
- No Hermes
- No tools
- No semantic memory
- No trace DAG
- No raw evidence exposure
- No direct browser-to-LLM calls
