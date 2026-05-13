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

Implementation notes
- The server uses only Node built-ins
- Static assets live in `public/`
- The chat behavior is entirely local in the browser
- Slash commands return safe placeholder messages only

Next steps
- 10B: wire a controlled local bridge for product actions without exposing technical internals
- 10C: connect the real RCK/Hermes plumbing behind product actions while keeping the UI conversational
