import {
  createContextScopeSuggestion,
  normalizeContextScopeSuggestion,
} from './rck-context-scope-contract.mjs';

const MOCK_CONTEXT_SCOPE_SUGGESTION = {
  suggestionId: 'rck-scope-suggestion-placeholder-v1',
  status: 'suggested',
  userIntentText: 'Qué decisiones tomamos sobre ContextPack y RufusChat?',
  suggestedTarget: {
    targetType: 'anchor',
    targetId: 'mock-anchor-rufuschat-contextpack-adapter',
    label: 'RufusChat ContextPack adapter boundary',
  },
  suggestedDepth: 4,
  includeAnchors: true,
  includeEvidenceRefs: true,
  includeDocs: true,
  selectedArtifacts: [
    {
      path: 'docs/CONTEXT_PACK_BOUNDARY.md',
      label: 'ContextPack boundary',
      reason: 'Defines the scope contract and the user approval boundary.',
    },
    {
      path: 'docs/RUFUSCHAT_ADAPTER_DESIGN.md',
      label: 'RufusChat adapter design',
      reason: 'Contains the integration boundary for the placeholder flow.',
    },
    {
      path: 'apps/rufuschat-ui/RCK_CONTEXTPACK_PREVIEW.md',
      label: 'ContextPack preview placeholder doc',
      reason: 'Documents the placeholder preview that appears after approval.',
    },
  ],
  candidateArtifacts: [
    {
      path: 'apps/rufuschat-ui/RCK_CONTEXT_SCOPE_SUGGESTION.md',
      label: 'Context scope suggestion doc',
      reason: 'Documents this dev-only suggestion layer.',
    },
    {
      path: 'apps/rufuschat-ui/server.mjs',
      label: 'RufusChat UI server',
      reason: 'Hosts the placeholder suggestion endpoint.',
    },
    {
      path: 'apps/rufuschat-ui/public/app.js',
      label: 'RufusChat UI client',
      reason: 'Renders the placeholder suggestion flow in memory only.',
    },
    {
      path: 'apps/rufuschat-ui/public/index.html',
      label: 'RufusChat UI shell',
      reason: 'Contains the visible Attach RCK Context panel.',
    },
  ],
  excludedArtifacts: [
    {
      path: 'storage/CLI docs',
      label: 'Storage / CLI docs',
      reason: 'Excluded because this phase is placeholder/dev-only and does not use storage data.',
    },
    {
      path: '.data product state',
      label: 'Product state files',
      reason: 'Excluded because suggestions stay in memory and do not persist to .data.',
    },
    {
      path: 'rck-core internals',
      label: 'RCK Core internals',
      reason: 'Excluded because the implementation does not read or execute RCK Core.',
    },
  ],
  rationale:
    'The intent mentions ContextPack and RufusChat integration, so the placeholder scope centers the adapter boundary, preview placeholder, and the UI/server files that host the dev-only flow.',
  confidence: 0.83,
  warnings: [
    'Placeholder / dev-only suggestion.',
    'No LLM call was made.',
    'No TraceSlice was generated.',
    'No ContextPack was generated.',
    'Confirm injection stays disabled.',
  ],
  preview: {
    previewId: 'rck-context-scope-preview-placeholder-v1',
    status: 'placeholder',
    placeholder: true,
    available: true,
    title: 'RufusChat ContextPack preview placeholder',
    summary: 'Demo/dev-only preview. The real TraceSlice and ContextPack chain is still pending.',
    derivedFromSuggestion: false,
    confirmationDisabled: true,
    nextSteps: [
      'Connect the scope to a real selector in a later phase.',
      'Generate a real TraceSlice from an approved scope.',
      'Build a real ContextPack from that TraceSlice.',
      'Show a real preview of the generated chain.',
      'Enable confirm injection only after the real chain exists.',
    ],
  },
  userDecision: {
    decision: 'pending',
    decidedAt: null,
    decidedBy: 'user',
    notes: ['User approval is required before the suggestion becomes approved locally.'],
  },
};

export function buildMockContextScopeSuggestion(overrides = {}) {
  return normalizeContextScopeSuggestion(
    createContextScopeSuggestion({
      ...MOCK_CONTEXT_SCOPE_SUGGESTION,
      ...overrides,
      suggestedTarget: {
        ...MOCK_CONTEXT_SCOPE_SUGGESTION.suggestedTarget,
        ...(overrides.suggestedTarget ?? {}),
      },
      preview: {
        ...MOCK_CONTEXT_SCOPE_SUGGESTION.preview,
        ...(overrides.preview ?? {}),
      },
      userDecision: {
        ...MOCK_CONTEXT_SCOPE_SUGGESTION.userDecision,
        ...(overrides.userDecision ?? {}),
      },
    }),
  );
}

export function getContextScopeSuggestionPlaceholder(intentText = '') {
  return buildMockContextScopeSuggestion({
    userIntentText: intentText,
  });
}
