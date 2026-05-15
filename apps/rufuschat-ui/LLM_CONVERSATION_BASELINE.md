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
→ ProductState persistence

Adapter rules:

- keep the browser UI free of raw LLM credentials
- call the Pi AI package from the backend only
- persist the user message before the request and the assistant message after the response
- store only transcript data in ProductState for Phase 17
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
