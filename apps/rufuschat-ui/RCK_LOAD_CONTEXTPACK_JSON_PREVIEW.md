# Load ContextPack JSON preview

This document covers the **manual / dev-safe** flow for loading a real ContextPack JSON payload into the RufusChat RCK side panel.

## What this phase does

- Accepts a ContextPack JSON object pasted by the user.
- Validates the shape minimally and safely.
- Normalizes the payload into the existing preview contract.
- Renders a **loaded preview only** view in the side panel.
- Keeps **Confirm injection disabled**.

## What this phase does not do

- No automatic ContextPack generation by RufusChat.
- No real RCK Core execution.
- No `.rck` reads.
- No filesystem path loading from arbitrary local paths.
- No chat context injection.
- No injection record persistence.
- No semantic ranking, embeddings, or LLM calls.
- No HTML rendering from the loaded JSON.
- No script execution.

## Supported load inputs

The endpoint accepts either:

- `{ "contextPackJson": "{...}" }`
- `{ "contextPack": { ... } }`

The UI uses pasted JSON text and sends it as `contextPackJson`.

## Minimal expected shape

The loader expects, at minimum:

- `schemaVersion` equal to `rck.context_pack.v0`
- `contextPack` metadata object
- `sourceTraceSlices`
- `structuralContext`
- `narrativeContext`
- `injectionInstructions`
- `injectionPolicy`

Optional fields are tolerated and normalized to safe defaults when possible.

## Output contract

A successful load returns:

- `ok: true`
- `preview`
- `warnings`
- `constraints`
- `source: "loaded-contextpack-json"`

The preview is safe for the UI and is rendered with text-only DOM updates.

## Expected errors

Common load failures include:

- empty JSON
- invalid JSON syntax
- missing `schemaVersion`
- unsupported `schemaVersion`
- missing `contextPack` metadata
- missing required structural buckets
- payload too large for the simple pasted-JSON path

Error responses return:

- `ok: false`
- `error.code`
- `error.message`
- `issues`

## UI behavior

When a loaded preview is available, the side panel shows:

- `Loaded ContextPack preview`
- schema version
- title if present
- source trace slice hashes if present
- visible sections
- warnings and constraints
- provenance summary
- `exactTextToInject` if present

The panel also keeps the manual-load disclaimer visible so it is clear that RufusChat did **not** generate the ContextPack automatically.

## Security notes

- Content is rendered with `textContent` / safe text assignment.
- No `innerHTML` is used for loaded ContextPack content.
- No markdown-to-HTML interpretation is applied.
- No scripts are executed.
- The pasted JSON size is capped in the loader, and the UI warns on large payloads.

## Next steps

1. Validate against the real schema.
2. Add safe import-from-file and export flows.
3. Add an explicit confirm-injection step.
4. Persist an injection record only after the real injection chain exists.
