const CONTEXT_SCOPE_SELECTED_ARTIFACTS = [
  'docs/CONTEXT_PACK_BOUNDARY.md',
  'docs/RUFUSCHAT_ADAPTER_DESIGN.md',
  'apps/rufuschat-ui/RCK_CONTEXTPACK_PREVIEW.md',
];

const CONTEXT_SCOPE_CANDIDATE_ARTIFACTS = [
  'apps/rufuschat-ui/RCK_CONTEXT_SCOPE_SUGGESTION.md',
  'apps/rufuschat-ui/server.mjs',
  'apps/rufuschat-ui/public/app.js',
  'apps/rufuschat-ui/public/index.html',
];

const CONTEXT_SCOPE_EXCLUDED_ARTIFACTS = [
  'storage/CLI docs',
  '.data product state files',
  '.pi/rck runtime files',
  'rck-core implementation internals',
];

function toTrimmedString(value, fallback = '') {
  if (typeof value !== 'string') {
    return fallback;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : fallback;
}

function toStringList(value, fallback = []) {
  if (!Array.isArray(value)) {
    return [...fallback];
  }

  return value.map((item) => toTrimmedString(item)).filter(Boolean);
}

function toBoolean(value, fallback = false) {
  return typeof value === 'boolean' ? value : fallback;
}

function toFiniteNumber(value, fallback = 0) {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function clampConfidence(value, fallback = 0.72) {
  const candidate = toFiniteNumber(value, fallback);
  if (candidate < 0) {
    return 0;
  }

  if (candidate > 1) {
    return 1;
  }

  return candidate;
}

function createContextScopeArtifact(input = {}) {
  return Object.freeze({
    path: toTrimmedString(input.path, 'unknown'),
    label: toTrimmedString(input.label, toTrimmedString(input.path, 'Unknown artifact')),
    reason: toTrimmedString(input.reason, 'Placeholder artifact note.'),
  });
}

function toArtifactList(value, fallback = []) {
  if (!Array.isArray(value)) {
    return fallback.map((item) => createContextScopeArtifact(typeof item === 'string' ? { path: item } : item));
  }

  return value.map((item, index) => {
    if (typeof item === 'string') {
      return createContextScopeArtifact({
        path: item,
        label: item,
        reason: 'Placeholder artifact reference.',
      });
    }

    return createContextScopeArtifact({
      ...item,
      path: toTrimmedString(item?.path, fallback[index] ?? 'unknown'),
    });
  });
}

function inferSuggestedTarget(userIntentText) {
  const lowered = toTrimmedString(userIntentText, '').toLowerCase();
  const mentionsRufusChat = lowered.includes('rufuschat');
  const mentionsContextPack = lowered.includes('contextpack') || lowered.includes('context pack');
  const mentionsTrace = lowered.includes('trace');

  if (mentionsRufusChat || mentionsContextPack || mentionsTrace) {
    return createContextScopeTarget({
      targetType: mentionsTrace ? 'trace' : 'anchor',
      targetId: mentionsTrace ? 'mock-trace-rufuschat-contextpack' : 'mock-anchor-rufuschat-contextpack-adapter',
      label: mentionsTrace
        ? 'RufusChat trace boundary'
        : 'RufusChat ContextPack adapter boundary',
    });
  }

  return createContextScopeTarget({
    targetType: 'unknown',
    targetId: 'mock-context-scope-target-unknown',
    label: 'Unresolved placeholder target',
  });
}

function createContextScopeTarget(input = {}) {
  return Object.freeze({
    targetType: toTrimmedString(input.targetType, 'unknown'),
    targetId: toTrimmedString(input.targetId, 'mock-context-scope-target-unknown'),
    label: toTrimmedString(input.label, 'Unresolved placeholder target'),
  });
}

function createContextScopePreview(input = {}) {
  return Object.freeze({
    previewId: toTrimmedString(input.previewId, 'rck-context-scope-preview-placeholder-v1'),
    status: toTrimmedString(input.status, 'placeholder'),
    placeholder: true,
    available: toBoolean(input.available, true),
    title: toTrimmedString(input.title, 'RufusChat ContextPack preview placeholder'),
    summary: toTrimmedString(
      input.summary,
      'Demo/dev-only preview. The real TraceSlice and ContextPack chain is still pending.',
    ),
    sourceSuggestionId: toTrimmedString(input.sourceSuggestionId, 'rck-scope-suggestion-placeholder-v1'),
    derivedFromSuggestion: toBoolean(input.derivedFromSuggestion, false),
    confirmationDisabled: toBoolean(input.confirmationDisabled, true),
    nextSteps: toStringList(input.nextSteps, [
      'Generate a real TraceSlice from an approved scope in a later phase.',
      'Build a real ContextPack from that TraceSlice.',
      'Preview the real ContextPack chain.',
      'Enable confirm injection only after the real chain exists.',
    ]),
  });
}

function createContextScopeUserDecision(input = {}) {
  return Object.freeze({
    decision: toTrimmedString(input.decision, 'pending'),
    decidedAt: input.decidedAt === null ? null : toTrimmedString(input.decidedAt, ''),
    decidedBy: toTrimmedString(input.decidedBy, 'user'),
    notes: toStringList(input.notes, ['User approval is required before anything is shown as approved.']),
  });
}

export function createContextScopeSuggestion(input = {}) {
  const userIntentText = toTrimmedString(
    input.userIntentText ?? input.intent ?? input.text,
    'Qué decisiones tomamos sobre ContextPack y RufusChat?',
  );
  const suggestedTarget = createContextScopeTarget(input.suggestedTarget ?? inferSuggestedTarget(userIntentText));
  const suggestionId = toTrimmedString(input.suggestionId, 'rck-scope-suggestion-placeholder-v1');
  const status = toTrimmedString(input.status, 'suggested');
  const preview = createContextScopePreview({
    ...(input.preview ?? {}),
    sourceSuggestionId: suggestionId,
  });
  const selectedArtifacts = toArtifactList(input.selectedArtifacts, CONTEXT_SCOPE_SELECTED_ARTIFACTS);
  const candidateArtifacts = toArtifactList(input.candidateArtifacts, CONTEXT_SCOPE_CANDIDATE_ARTIFACTS);
  const excludedArtifacts = toArtifactList(input.excludedArtifacts, CONTEXT_SCOPE_EXCLUDED_ARTIFACTS);

  return Object.freeze({
    suggestionId,
    status,
    userIntentText,
    suggestedTarget,
    suggestedDepth: toFiniteNumber(input.suggestedDepth, suggestedTarget.targetType === 'trace' ? 3 : 4),
    includeAnchors: toBoolean(input.includeAnchors, suggestedTarget.targetType !== 'unknown'),
    includeEvidenceRefs: toBoolean(input.includeEvidenceRefs, true),
    includeDocs: toBoolean(input.includeDocs, true),
    selectedArtifacts,
    candidateArtifacts,
    excludedArtifacts,
    rationale: toTrimmedString(
      input.rationale,
      'The intent mentions RufusChat and ContextPack, so the placeholder scope centers the adapter boundary and preview contract.',
    ),
    confidence: clampConfidence(input.confidence, suggestedTarget.targetType === 'unknown' ? 0.48 : 0.81),
    warnings: toStringList(input.warnings, [
      'Placeholder / dev-only suggestion.',
      'No LLM call was made.',
      'No TraceSlice was generated.',
      'No ContextPack was generated.',
      'Confirm injection stays disabled.',
    ]),
    preview,
    userDecision: createContextScopeUserDecision(input.userDecision ?? {}),
  });
}

export function normalizeContextScopeSuggestion(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return createContextScopeSuggestion();
  }

  return createContextScopeSuggestion(candidate);
}

export function isContextScopeSuggestion(candidate) {
  return Boolean(
    candidate &&
      typeof candidate === 'object' &&
      typeof candidate.suggestionId === 'string' &&
      typeof candidate.status === 'string' &&
      typeof candidate.userIntentText === 'string' &&
      candidate.suggestedTarget &&
      typeof candidate.suggestedTarget === 'object' &&
      Array.isArray(candidate.selectedArtifacts) &&
      Array.isArray(candidate.candidateArtifacts) &&
      Array.isArray(candidate.excludedArtifacts) &&
      typeof candidate.rationale === 'string' &&
      typeof candidate.preview === 'object' &&
      typeof candidate.userDecision === 'object',
  );
}

export function createContextScopeTargetPlaceholder(input = {}) {
  return createContextScopeTarget(input);
}

export function createContextScopeArtifactPlaceholder(input = {}) {
  return createContextScopeArtifact(input);
}

export function createContextScopePreviewPlaceholder(input = {}) {
  return createContextScopePreview(input);
}

export function createContextScopeUserDecisionPlaceholder(input = {}) {
  return createContextScopeUserDecision(input);
}
