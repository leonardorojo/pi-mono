# RufusChat UI product shell

This app is the ChatGPT-like product shell for RufusChat.

It is intentionally local-first in the browser shell. Phase 17 adds a backend LLM baseline through `/api/chat/complete`, while Hermes execution and the RCK backend remain disconnected in the UI flow.

## LLM conversation baseline

- Phase 17 introduces a backend chat completion baseline for RufusChat.
- Endpoint: `POST /api/chat/complete`
- The endpoint talks to the existing Pi AI provider contract from `packages/ai`.
- v0 is non-streaming.
- The backend requires a configured model/provider and the matching API key or bearer token in the environment or Pi auth storage.
- The browser UI does **not** call the LLM directly.
- Normal chat messages are sent to `/api/chat/complete`.
- Real assistant replies use the Pi Agent configured provider/model by default (`github-copilot/gpt-5.4-mini` in this workspace).
- If you override the defaults, set `RUFUSCHAT_LLM_PROVIDER` and `RUFUSCHAT_LLM_MODEL`.
- Do **not** set `OPENAI_API_KEY` unless the selected provider is actually OpenAI.

## Streaming responses v0

- RufusChat now streams normal LLM replies through `POST /api/chat/stream`.
- The stream uses SSE with `start`, `delta`, `done`, and `error` events.
- The browser still uses the configured Pi provider/model; no direct browser-to-LLM calls are introduced.
- `/api/chat/complete` remains available as the non-streaming fallback path.
- The UI persists the final assistant message at the end of the stream, not on every token.
- While the assistant is still thinking, the transient placeholder stays compact and animated; it disappears as soon as the first streamed text arrives.
- Slash command results are rendered as compact product events so conversation stays visually primary.
- Cancel and Retry are lightweight chat controls for the streaming conversation path.
- Cancel aborts the active stream or ignores late deltas, and Retry replays the last LLM assistant reply without duplicating the user message.
- This phase does not add deep memory, Hermes, or RCK wiring.

## Context Pack boundary

- Boundary document: [`CONTEXT_PACK.md`](./CONTEXT_PACK.md)
- Checkpoint boundary: [`CHECKPOINTS.md`](./CHECKPOINTS.md)
- RCK ContextPack preview contract: [`RCK_CONTEXTPACK_PREVIEW.md`](./RCK_CONTEXTPACK_PREVIEW.md)
- RCK ContextPack generation contract: [`RCK_CONTEXTPACK_GENERATION_CONTRACT.md`](./RCK_CONTEXTPACK_GENERATION_CONTRACT.md)
- RCK Context Scope Suggestion contract: [`RCK_CONTEXT_SCOPE_SUGGESTION.md`](./RCK_CONTEXT_SCOPE_SUGGESTION.md)
- Manual / dev-safe ContextPack JSON load preview: [`RCK_LOAD_CONTEXTPACK_JSON_PREVIEW.md`](./RCK_LOAD_CONTEXTPACK_JSON_PREVIEW.md)
- The Attach RCK Context flow now starts with a placeholder scope suggestion, converts approved scope into a placeholder generation request, and can also load a real ContextPack JSON preview manually.
- Phase 25 adds the approved RCK context completion path: a confirmed injection can be included once in the next `/api/chat/complete` request, then it is consumed.
- Phase 26 defines the future write-back direction from a closed chat turn into RCK State / Delta traces; design doc: [`RCK_WRITEBACK_DESIGN.md`](./RCK_WRITEBACK_DESIGN.md)
- Phase 27 adds the first adapter contract/stub for chat-turn write-back payloads; contract doc: [`RCK_WRITEBACK_ADAPTER_CONTRACT.md`](./RCK_WRITEBACK_ADAPTER_CONTRACT.md)
- Phase 28 wires the stub into the closed-turn chat flow and renders a dev-only write-back preview; flow doc: [`RCK_CHAT_TURN_WRITEBACK_PLACEHOLDER.md`](./RCK_CHAT_TURN_WRITEBACK_PLACEHOLDER.md)
- Phase 29 stabilizes the in-memory write-back state model; state model doc: [`RCK_WRITEBACK_STATE_MODEL.md`](./RCK_WRITEBACK_STATE_MODEL.md)
- Fase 13B adds a placeholder `/inject` candidate UX in the browser
- Fase 13C adds minimal per-chat injection history metadata
- `/inject` runtime behavior remains safe / fake and does not read real sources
- Context Pack is a safe abstraction for future context injection, not a technical RCK dashboard

## Product path

- `apps/rufuschat-ui` is the official RufusChat UI v0 path.
- It follows a ChatGPT-like UX:
  - sidebar projects/chats
  - central chat
  - slash commands
  - backend RCK/Hermes invisible
- Future work should target `apps/rufuschat-ui` unless explicitly doing prototype maintenance.

## What is not connected in 10E

- No LLM calls
- No OpenAI calls
- No Hermes execution
- No RCK mutations
- No RCK Core integration
- No persistence or storage changes
- No project memory
- No technical RCK dashboard in the UI
- No automatic ContextPack generation
- No automatic context injection

## What 10E adds

- A chat-linked RCK trace placeholder in the session shell
- A visible but non-functional `Trace: not linked` chip in the header
- A placeholder `/trace` command that does not create or link any real trace
- No real trace management yet

## Relationship to existing prototype

- `scripts/rufuschat-ui-server.mjs` remains the validated Fase 8 prototype server
- `apps/rufuschat-ui/` is the official product UI path
- The prototype is frozen as a validation reference unless a critical bug or comparison is needed

## Product data persistence

Fase 11A defines the Product Data boundary only.

- Boundary document: [`PRODUCT_DATA_PERSISTENCE.md`](./PRODUCT_DATA_PERSISTENCE.md)
- Storage decision: backend-local JSON file
- Future runtime path: `apps/rufuschat-ui/.data/rufuschat-product-state.json`
- 11A does **not** implement runtime persistence
- 11B implements the backend-local JSON store; 11C hydrates/saves the UI from that store

## Runtime status

- Endpoint: `GET /api/runtime-status`
- Shape: `RuntimeStatus v0` with product-friendly labels for runtime, memory, context, trace, and LLM
- Current implementation: safe local placeholder composed from internal providers and normalized at the boundary
- /status reads the hydrated RuntimeStatus contract and falls back safely if the endpoint is unavailable
- The UI consumes the labels directly and falls back locally if the endpoint is unavailable
- The endpoint does not expose RCK internals, `.pi/rck`, raw evidence, or internal paths

Run it
From the repo root:

```bash
node apps/rufuschat-ui/server.mjs
```

Optional port override:

```bash
PORT=4173 node apps/rufuschat-ui/server.mjs
```

Then open:
- http://127.0.0.1:4173/

Backend endpoints in 10B
- `GET /api/status`
- `POST /api/checkpoint`
- `POST /api/inject`
- `POST /api/hermes/fake`

Implementation notes
- The server uses only Node built-ins
- Static assets live in `public/`
- The chat behavior is local in the browser
- Mutating actions require confirmation in the UI
- Slash commands return safe placeholder or controlled backend messages only
- `/help` lists the current command catalog
- The slash menu is only a discovery aid and does not run anything by itself
- No raw JSON, stdout/stderr, or evidence dumps are shown in the chat UI
- New project now prompts for project name and optional repository path
- Repository path is metadata only for now
- RufusChat does not read files automatically yet
- Chats are never created without a project context

Next steps
- Keep polishing the command experience while preserving the local-only browser model
- Any real backend wiring remains out of scope for this UI skeleton
