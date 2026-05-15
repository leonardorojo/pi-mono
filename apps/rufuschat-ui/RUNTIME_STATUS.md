# Runtime Status v0

RuntimeStatus is the backend-facing contract that tells RufusChat UI which product-level capabilities are currently available in the local session.

It exists so the UI can render simple, product-friendly chips without learning backend internals.

## What it is

- A small JSON contract exposed by the RufusChat UI server
- A safe status surface for product capabilities such as Memory, Context, Trace, and LLM
- A placeholder boundary for future real providers

## What it is not

- It is not the real RCK Trace DAG
- It is not raw evidence
- It is not a semantic memory store
- It is not a provider API for LLM execution
- It is not a path to `.pi/rck`
- It is not a product-state replacement

## v0 shape

```json
{
  "version": 1,
  "runtime": {
    "mode": "local",
    "label": "Local session"
  },
  "memory": {
    "status": "off",
    "label": "Memory off"
  },
  "context": {
    "status": "off",
    "label": "Context off"
  },
  "trace": {
    "status": "not_linked",
    "label": "Trace not linked"
  },
  "llm": {
    "status": "off",
    "label": "LLM off"
  }
}
```

## Endpoint

- `GET /api/runtime-status`
- Returns JSON only
- Designed for UI hydration and simple capability display
- Safe to call even when no real providers are wired up yet

## Why it does not expose real RCK yet

Fase 16A keeps the boundary intentionally minimal.

We want the UI to know only the product story:

- what is available
- what is not available
- what is linked
- what is still local-only

The technical RCK implementation, evidence data, and future provider wiring stay behind the boundary.

## Relationship to ProductState

ProductState remains the local, persisted UI/project/chat data model.

RuntimeStatus is separate:

- ProductState stores product content and local workspace state
- RuntimeStatus advertises runtime capability/status
- Neither one replaces the other

## Relationship to Context Pack

Context Pack is the abstraction for future safe context injection.

RuntimeStatus can say whether Context is off or on, but it does not define the Context Pack format and it does not expose raw source data.

## Relationship to Checkpoint

Checkpoint is the product event for marking a chat/work milestone.

RuntimeStatus can report whether checkpoint-related context is available, but checkpoints remain their own product action and boundary.

## Future providers

Later phases may map these labels to real providers, for example:

- a real memory backend
- a real context-pack generator
- a real trace linker
- a real LLM provider

That work should happen behind the same contract shape, or behind a versioned successor.

## Non-goals

- No RCK technical dashboard in the UI
- No raw evidence exposure
- No internal path exposure
- No provider credentials in the browser
- No ProductState schema changes in this phase
- No real provider wiring in Fase 16A
