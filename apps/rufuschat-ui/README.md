# RufusChat UI skeleton (Fase 10A)

This app is the ChatGPT-like UI skeleton for RufusChat.

It is intentionally local-only and does not connect to the LLM, OpenAI, Hermes execution, or the RCK backend yet. The goal of 10A is to lock the final product shape before wiring any real automation.

What is included
- Left sidebar with Projects / Chats structure
- Center chat rail with header, message history, and composer
- Local message handling for plain text input
- Placeholder slash commands: /checkpoint, /inject, /status, /hermes
- Dark theme with a ChatGPT-like layout

What is not connected in 10A
- No LLM calls
- No OpenAI calls
- No Hermes execution
- No RCK mutations
- No RCK Core integration
- No persistence or storage changes
- No project memory
- No technical RCK dashboard in the UI

Relationship to existing prototype
- `scripts/rufuschat-ui-server.mjs` remains the validated Fase 8 prototype server
- `apps/rufuschat-ui/` is the new skeleton for the final UI direction

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
- No raw JSON, stdout/stderr, or evidence dumps are shown in the chat UI

Next steps
- 10B: connect controlled product actions through the slash command endpoints
- 10C: wire real RCK/Hermes plumbing behind product actions while keeping the UI conversational
