# Project / Chat / RCK boundary model

This document defines the conceptual boundary for RufusChat so that chat memory, project memory, context packs, and RCK traces are not conflated.

It is a design contract only. It does not describe a persistence implementation or a runtime data model.

## Definitions

### Project
The Project is the PI workspace boundary RufusChat operates inside.
It identifies the repo path, project settings, stable project memory, chat session collection, and references to current or historical RCK traces.

### Chat Session
A Chat Session is the conversational boundary for a single user-facing dialogue thread.
It contains transcript, decisions, semantic summaries, and links to RCK artifacts, but it is not the operational source of truth.

### Conversation Memory
Conversation Memory is the semantic summary layer retained for later recall.
It may capture user-approved notes, decisions, and compact context, but it must not store raw evidence as the default form.

### RCK Trace DAG
The RCK Trace DAG is the auditable operational graph owned by RCK Core.
It is built from states, deltas, anchors, and related references, and it is not the same thing as a transcript or chat memory.

### RCK State
An RCK State is a recorded operational snapshot in the trace system.
It represents a stable point that can be referenced, summarized, or compared with other states.

### RCK Delta
An RCK Delta is a change record between states or between trace points.
It describes what changed, not the entire conversation around the change.

### RCK Anchor
An RCK Anchor is a user-approved reference point in the trace DAG.
It can be used to mark a decision, a notable state, or a context boundary worth preserving.

### Context Pack
A Context Pack is a safe, user-approved abstraction generated from RCK inputs.
It is intended for injection into a chat session or another agent when the user explicitly decides to do so.
It is not a raw trace dump.

### Activity Log
The Activity Log is the UI/runtime projection of recent actions and events.
It is useful for visibility, but it is not durable truth unless a later phase explicitly persists it.

## Ownership

### Project owns
- repo path
- project settings
- stable project memory
- list of chat sessions
- references to current and historical RCK traces

### Chat Session owns
- conversational transcript
- semantic summary
- decisions made in the chat
- links to RCK traces, anchors, and context packs
- the active UI conversation state, not the operational source of truth

### Conversation Memory owns
- semantic summaries
- user-approved notes
- decisions worth recalling later
- project-level or chat-level remembered facts

Conversation Memory does not own raw evidence.

### RCK Trace DAG owns
- state records
- deltas
- anchors
- operational/auditable register data

RCK Trace DAG is owned by RCK Core Kernel, not by the chat UI.

### Context Pack owns
- generated safe abstraction
- trace-derived context selected for a specific use
- references that can be injected when the user decides

Context Pack is generated from RCK data, but it is not the RCK trace itself.

### Activity Log owns
- recent runtime/UI events
- visible action history
- transient projection state

Activity Log is a view, not the durable system of record.

## Relationship model

```text
Project
  -> many Chat Sessions
  -> many/current RCK Trace refs

Chat Session
  -> may link to one or more RCK traces
  -> may use one current Context Pack

RCK Trace
  -> belongs to RCK Core
  -> references states / deltas / anchors / evidence / context packs

Context Pack
  -> generated from RCK state/trace inputs
  -> injected into Chat Session when the user decides
```

## Rules

- RCK is a parallel governed register, not the chat itself.
- Chat memory may summarize conversation, but it must link to RCK artifacts when claiming operational facts.
- RufusChat must not silently inject context.
- The user decides when to inject context.
- The user decides when to create state or anchor records.
- Chat can suggest actions, but mutating actions require confirmation.
- Context Packs can feed the current chat, future chats, or other agents.
- Project memory must not be used as raw evidence.
- Raw evidence remains gated/reference-only by default.

## Product implications

This boundary model implies the future RufusChat UI should have separate surfaces for:

- Project selector
- Chat session list
- Current RCK trace panel
- Safe Context panel
- Decision guidance
- Activity timeline
- Explicit inject action/button

It also implies that the UI should make the distinction visible between transcript, safe context, and auditable RCK trace artifacts.

## Non-goals

- no persistence implementation in 9C
- no chat LLM implementation
- no RCK Core integration in 9C
- no project manager implementation
- no UI rewrite
- no storage migration
