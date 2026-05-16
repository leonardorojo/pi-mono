# RufusChat Lightweight Memory Boundary v1

## Purpose

This document captures the current RufusChat memory boundary as a verified snapshot for Fase 18A.

North Star:
- RufusChat should feel like chat.
- RufusChat should keep lightweight conversational continuity.
- RCK remains the system for deep, traceable, governable memory.
- RufusChat must not become a parallel memory system.
- No RAG, embeddings, or automatic summaries yet.

## Current RufusChat memory state

RufusChat today has a local-first product state with persisted chat transcript history.
The UI sends the LLM a recent, bounded conversation window rather than a full long-term memory system.

Current boundary, as verified for this snapshot:
- ProductState holds the user-facing workspace state.
- The UI sends only a recent transcript window to the LLM, currently 12 messages.
- 18B improves fluency via streaming only; it does not introduce deep memory.
- Memory, trace, and context are visible as placeholders in the product shell.
- Injections and checkpoints exist as part of the product state.
- There is no real semantic memory yet.
- There is no real RAG, embeddings, vector store, or Trace DAG inside RufusChat.
- Deep, auditable, governed memory stays in RCK territory.
- RufusChat should preserve conversational continuity without duplicating RCK.

## What RufusChat can do now

RufusChat can use lightweight conversational controls that help the UI feel like chat without turning it into a memory engine:
- recent transcript history
- a bounded context window
- streaming responses
- retry and cancel flows
- visibility into what context is being used
- a minimal system prompt
- manual checkpoints
- manual injections

These are product controls, not a semantic memory subsystem.

## What RufusChat should not do yet

RufusChat should not introduce:
- a vector database
- embeddings-based retrieval
- RAG of its own
- automatic hidden memory accumulation
- automatic project summaries
- internal agents that rewrite memory
- a parallel Trace DAG
- a replacement for RCK

## Boundary summary

The current product boundary is:

- RufusChat = conversation surface with lightweight continuity
- ProductState = local persisted UI state and transcript history
- LLM context = recent bounded transcript plus explicit prompt/context controls
- RCK = deep memory, auditability, governance, future traceable memory

This keeps RufusChat product-shaped and conversational while avoiding premature memory-system duplication.

## Fase 18A scope

Fase 18A documents the current memory boundary only.
It does not add semantic memory, RAG, embeddings, or a new trace system.
