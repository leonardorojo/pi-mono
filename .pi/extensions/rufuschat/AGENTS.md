# RufusChat extension notes

## Scope

This directory is for the RufusChat PI extension foundation.
Keep it small and explicit.

## Rules

- Do not couple UI code directly to `.pi/rck/` layout details.
- Do not expose raw evidence by default.
- Do not add real Hermes execution here.
- Do not add Codex execution here.
- Do not implement storage mutations without confirmation gates.
- Keep the current bridge/provider boundary replaceable.
- Prefer safe DTOs and adapter contracts over direct state shape reuse.
- Do not conflate chat memory with RCK trace.
- Do not treat context packs as raw transcripts.
- Do not silently inject context without a user decision.

## Intent

The extension should eventually sit between the PI product surface and an RckProvider implementation.
Until that contract is finalized, keep this folder documentation-first and placeholder-only.
