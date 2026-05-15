# Phase 15A — Product Shell Visual Audit

## Current state

RufusChat is already recognizably a local-first chat product with a project/chat split, a central conversation rail, slash commands, and safe placeholder boundaries for Context Packs and Checkpoints. The shell is functional and structurally coherent, but it still reads more like a dense internal workspace than a calm ChatGPT-like product surface. The main sources of tension are the sidebar density, the amount of always-visible status metadata in the header, and the visibility of ProductState tools.

## North Star fit

What is already aligned:
- The UI is conversational-first in layout, with a central chat rail and composer.
- Context Pack and Checkpoint are modeled as product abstractions, not as raw RCK surfaces.
- The UI does not read `.pi/rck` directly from the frontend.
- No raw evidence viewer is present.
- Mutating actions are guarded by confirmation.

What starts to drift:
- The shell shows a lot of operational metadata at once, especially around project/chat status and trace state.
- Sidebar and header both expose technical/product-state concepts, which creates redundancy.
- ProductState dev tools are visible enough to feel like primary app features.
- The current visual language is boxy and status-heavy, so it trends toward dashboard/admin console energy.

What to correct before real RCK integration:
- Reduce persistent technical chrome.
- Make Project → Chats hierarchy easier to scan.
- Demote ProductState tooling.
- Keep Context Pack and Checkpoint present, but quieter and more conversational.

## Findings

### Sidebar

### Finding 1 — Sidebar hierarchy is clear but too dense

Severity:
- medium

Area:
- sidebar

Observation:
- The sidebar communicates Project → Chats correctly, but it stacks many labels and controls into a narrow column: Projects, Workspace, Global actions, Project tree, Product State, and footer copy. Active/selected badges also add visual weight.

Why it matters:
- The North Star wants RufusChat to feel like a chat product first. When the sidebar looks like an object tree with a lot of operational labels, the product feels more administrative than conversational.

Suggested action:
- Collapse or soften secondary labels, keep one strong hierarchy label path, and reduce badge density on project/chat rows.

Files likely affected:
- apps/rufuschat-ui/public/index.html
- apps/rufuschat-ui/public/styles.css
- apps/rufuschat-ui/public/app.js

Do not implement yet:
- yes

### Finding 2 — Global actions and product-local actions are visually too close

Severity:
- medium

Area:
- sidebar

Observation:
- New project / New chat sit near Product State Export / Import / Reset, all within the same left rail. That makes local data operations feel as prominent as core creation actions.

Why it matters:
- ProductState is not RCK and should not feel like the main product feature. Mixing creation actions with data-management actions encourages a dashboard mental model.

Suggested action:
- Separate creation actions from local-data tools more clearly, likely by moving Product State into a lower-priority, collapsible “Local data” section.

Files likely affected:
- apps/rufuschat-ui/public/index.html
- apps/rufuschat-ui/public/styles.css
- apps/rufuschat-ui/public/app.js

Do not implement yet:
- yes

### Chat header

### Finding 3 — Header carries too much technical status at once

Severity:
- high

Area:
- header

Observation:
- The header shows active project, active chat, Memory status, Summary status, RCK trace status, and a trace chip. Even though the header status pill is hidden in CSS, the remaining visible status line still reads like operational telemetry.

Why it matters:
- ChatGPT-like UX wants the header to anchor the conversation, not to behave like a system console. Too many simultaneous status indicators dilute the conversational focus and make the shell feel technical.

Suggested action:
- Keep only the minimal conversation identity in the header and move deeper state into discreet chips or an overflow/details area.

Files likely affected:
- apps/rufuschat-ui/public/index.html
- apps/rufuschat-ui/public/styles.css
- apps/rufuschat-ui/public/app.js

Do not implement yet:
- yes

### Finding 4 — Header and sidebar duplicate the same state cues

Severity:
- medium

Area:
- header

Observation:
- Active project and active chat are shown in the header, while the sidebar also marks the active project and selected chat with badges. The trace chip and trace status also repeat a similar boundary story.

Why it matters:
- Redundant state consumes attention without adding clarity. The North Star prefers a calm product shell where status is legible but not repeated everywhere.

Suggested action:
- Decide which state belongs in the sidebar, which belongs in the header, and which should be hidden behind detail on demand.

Files likely affected:
- apps/rufuschat-ui/public/index.html
- apps/rufuschat-ui/public/styles.css
- apps/rufuschat-ui/public/app.js

Do not implement yet:
- yes

### Message rendering

### Finding 5 — Core messages are readable, but technical product messages can become noisy

Severity:
- medium

Area:
- messages

Observation:
- User/assistant messages are clearly differentiated. The current conversation feels readable. But the code already supports checkpoint and context-pack message variants with badges, summaries, and multi-line metadata, which is useful yet can become noisy if too much of it is surfaced simultaneously.

Why it matters:
- The product should still feel like a conversation even when actions like /inject and /checkpoint appear. If every action result becomes a metadata block, the feed starts to resemble a log.

Suggested action:
- Keep action result messages short, conversational, and visually subordinate to the main chat flow.

Files likely affected:
- apps/rufuschat-ui/public/app.js
- apps/rufuschat-ui/public/styles.css

Do not implement yet:
- yes

### Finding 6 — Message history currently reads as chat, not a technical log, but it is close to the edge

Severity:
- low

Area:
- messages

Observation:
- The current visible messages are simple and readable. However, the surrounding shell metadata and badge patterns make it easy for the feed to drift toward “system log with comments” instead of “conversation with light status.”

Why it matters:
- Once more checkpoints and context packs appear, the conversation needs to remain emotionally and visually primary.

Suggested action:
- Preserve the conversational rhythm in turn rendering and avoid adding extra IDs or raw metadata to the visible body unless explicitly requested.

Files likely affected:
- apps/rufuschat-ui/public/app.js
- apps/rufuschat-ui/public/styles.css

Do not implement yet:
- yes

### Empty states

### Finding 7 — Empty states are understandable, but the copy is functional rather than welcoming

Severity:
- low

Area:
- empty-state

Observation:
- The app has sensible empty states such as “No projects yet.”, “No chats in this project yet.”, and “Start a conversation in this chat.” They are clear, but they still feel quite utilitarian.

Why it matters:
- Empty states are the first impression for fresh workspaces and newly reset ProductState. If they feel too technical, the product loses the ChatGPT-like warmth early.

Suggested action:
- Keep the meaning, but consider slightly more welcoming onboarding copy and stronger primary actions in the empty states.

Files likely affected:
- apps/rufuschat-ui/public/app.js
- apps/rufuschat-ui/public/styles.css
- apps/rufuschat-ui/public/index.html

Do not implement yet:
- yes

### Dev tools

### Finding 8 — ProductState export/import/reset is too visible for a product shell

Severity:
- high

Area:
- dev-tools

Observation:
- Export / Import / Reset are always present in the sidebar and sit close to normal navigation. Reset is protected by confirmation, which is good, but the whole cluster still feels like a primary feature rather than an advanced local-data tool.

Why it matters:
- ProductState is local product data, not RCK. Its controls should be available without becoming part of the main product identity.

Suggested action:
- Move these controls into a compact, clearly labeled local-data section with lower visual emphasis and stronger caution on Reset.

Files likely affected:
- apps/rufuschat-ui/public/index.html
- apps/rufuschat-ui/public/styles.css
- apps/rufuschat-ui/public/app.js

Do not implement yet:
- yes

### Context Pack UX

### Finding 9 — Context Pack state is conceptually good, but the visible language is still technical

Severity:
- medium

Area:
- context-pack

Observation:
- The Context Pack boundary is well framed as candidate / injected / cancelled, with explicit no-raw-evidence language. That is aligned with the North Star. The weak spot is that the visible copy and chip language still leans toward implementation vocabulary rather than a conversational product surface.

Why it matters:
- Context Pack should feel like a user-governed product action, not like an internal data pipeline. If the user sees too much technical phrasing, the abstraction loses its value.

Suggested action:
- Keep the safe boundary, but soften presentation copy and ensure status appears as discreet product state, not as debugging metadata.

Files likely affected:
- apps/rufuschat-ui/public/app.js
- apps/rufuschat-ui/public/styles.css
- apps/rufuschat-ui/CONTEXT_PACK.md

Do not implement yet:
- yes

### Checkpoint UX

### Finding 10 — Checkpoints are understandable as product milestones, but they still risk sounding like RCK

Severity:
- medium

Area:
- checkpoint

Observation:
- The checkpoint flow clearly signals that it is a product-side milestone and not an anchor or Trace DAG node. That is good. But the surrounding messaging still uses RCK terminology in places, which can blur the line for users.

Why it matters:
- The North Star says Checkpoint marks decisions/hits of product. It should not feel like a real RCK anchor unless and until that mapping is explicitly introduced later.

Suggested action:
- Keep the result concise and product-oriented; minimize RCK terms in the visible result surface.

Files likely affected:
- apps/rufuschat-ui/public/app.js
- apps/rufuschat-ui/public/styles.css
- apps/rufuschat-ui/CHECKPOINTS.md

Do not implement yet:
- yes

### CSS/layout cleanup

### Finding 11 — The shell has too many repeated pill/badge patterns

Severity:
- medium

Area:
- css

Observation:
- The codebase uses several similar visual treatments: header chips, trace chips, project/chat badges, context-pack badges, checkpoint badges, and sidebar dev-tool buttons. They are consistent individually, but the overall effect is visually noisy.

Why it matters:
- Repeated pill patterns make the product feel like it is constantly reporting status. A calmer interface needs fewer simultaneous badge styles.

Suggested action:
- Consolidate shared chip/badge tokens and reduce the number of badge instances visible at once.

Files likely affected:
- apps/rufuschat-ui/public/styles.css
- apps/rufuschat-ui/public/app.js

Do not implement yet:
- yes

### Finding 12 — Layout spacing is workable, but the shell still feels boxy and stiff

Severity:
- medium

Area:
- css

Observation:
- The sidebar width, sticky header, boxed sections, and dashed empty states create a rigid, segmented frame. The chat rail itself is readable, but the surrounding chrome gives the UI a desktop-tool feel rather than a warm web chat feel.

Why it matters:
- The North Star explicitly wants a ChatGPT web resemblance. That usually depends on softer hierarchy, fewer borders, and a more dominant conversation rail.

Suggested action:
- Reduce box stacking, simplify borders, and relax the sidebar/header treatment so the conversation area feels primary.

Files likely affected:
- apps/rufuschat-ui/public/styles.css
- apps/rufuschat-ui/public/index.html

Do not implement yet:
- yes

### Documentation

### Finding 13 — Documentation still speaks in older phase names and should be re-centered on Product Shell

Severity:
- medium

Area:
- docs

Observation:
- README still opens with “RufusChat UI skeleton (Fase 10E)” and several docs reference earlier phases like 13A/14A. The boundary docs are directionally correct, but they do not yet describe the current Product Shell focus with enough clarity.

Why it matters:
- Documentation should help new contributors understand the current product shape quickly. Stale phase references make the current state look more provisional than it is.

Suggested action:
- Update docs to explain the current Product Shell layer, distinguish existing product surface from future RCK wiring, and remove outdated phase framing where appropriate.

Files likely affected:
- apps/rufuschat-ui/README.md
- apps/rufuschat-ui/CONTEXT_PACK.md
- apps/rufuschat-ui/CHECKPOINTS.md
- apps/rufuschat-ui/PRODUCT_DATA_PERSISTENCE.md

Do not implement yet:
- yes

## Recommended subphase plan

15B — Sidebar hierarchy polish
15C — Chat header and status polish
15D — Message rendering polish
15E — Empty states and onboarding copy
15F — Dev tools containment
15G — CSS/layout cleanup
15H — Docs update

## Explicit non-goals

- No RCK real
- No Trace DAG real
- No raw evidence viewer
- No .pi/rck browser reads
- No LLM integration
- No semantic memory
- No dashboard técnico
- No legacy prototype feature work

## Recommended next step

Start with 15B — Sidebar hierarchy polish. The sidebar is currently the strongest signal that the shell feels like an internal workspace rather than a chat-first product, so reducing that noise should improve the overall first impression fastest.

## Phase 15B follow-up

Addressed in 15B:
- Sidebar hierarchy is clearer: the shell now separates app identity, create actions, Projects, and local data more explicitly.
- Project and chat rows are lighter: the project row no longer repeats chat-count metadata, and chat rows no longer surface extra injection/checkpoint badges in the sidebar.
- Sidebar copy is more product-oriented: ProductState phrasing was reduced in the visible shell.
- Local data tools are still available, but they read as a lower-priority section.

Still pending for 15C / 15D / 15F:
- 15C: simplify chat header status so it feels less like telemetry.
- 15D: keep message rendering calm when /inject and /checkpoint results accumulate.
- 15F: contain dev tools even further if needed, especially if local data actions still feel too prominent.

## Phase 15B.2 follow-up

Addressed in 15B.2:
- Sidebar density was reduced further by removing the explicit ACTIVE / SELECTED badges from project and chat rows.
- Project and chat rows were softened again so they read more like lightweight list items and less like cards.
- The local data footer was made more discreet and shorter so it does not compete with the project/chat hierarchy.

Still pending for 15C:
- chat header and status polish, including Memory, Summary, and RCK trace presentation.
- any further simplification of the trace chip and header identity treatment.
