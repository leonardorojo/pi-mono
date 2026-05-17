const CONTEXT_PACK_SCHEMA_VERSION = 'rck.context_pack.v0';
const CONTEXT_PACK_GENERATION_STATUS_VALUES = new Set(['placeholder', 'not_connected', 'ready', 'failed']);

const DEFAULT_GENERATION_WARNINGS = [
  'Placeholder / dev-only generation request.',
  'No RCK Core execution happened.',
  'No TraceSlice was generated.',
  'No ContextPack was generated.',
  'Confirm injection stays disabled.',
];

const DEFAULT_GENERATION_CONSTRAINTS = [
  'Do not read .rck directly.',
  'Do not execute RCK Core.',
  'Do not generate a real TraceSlice yet.',
  'Do not generate a real ContextPack yet.',
  'Do not persist generation requests.',
  'Do not persist injection records.',
  'Do not inject context into the chat runtime.',
  'Do not call an LLM.',
];

function toTrimmedString(value, fallback = '') {
  if (typeof value !== 'string') {
    return fallback;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : fallback;
}

function toBoolean(value, fallback = false) {
  return typeof value === 'boolean' ? value : fallback;
}

function toFiniteNumber(value, fallback = 0) {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function toStringList(value, fallback = []) {
  if (!Array.isArray(value)) {
    return [...fallback];
  }

  return value.map((item) => toTrimmedString(item)).filter(Boolean);
}

function createApprovedArtifact(input = {}) {
  return Object.freeze({
    path: toTrimmedString(input.path, 'unknown'),
    label: toTrimmedString(input.label, toTrimmedString(input.path, 'Unknown artifact')),
    reason: toTrimmedString(input.reason, 'Approved placeholder artifact.'),
  });
}

function toApprovedArtifactList(value, fallback = []) {
  if (!Array.isArray(value)) {
    return fallback.map((item) => createApprovedArtifact(typeof item === 'string' ? { path: item } : item));
  }

  return value.map((item, index) => {
    if (typeof item === 'string') {
      return createApprovedArtifact({
        path: item,
        label: item,
        reason: 'Approved placeholder artifact.',
      });
    }

    return createApprovedArtifact({
      ...item,
      path: toTrimmedString(item?.path, fallback[index]?.path ?? 'unknown'),
    });
  });
}

function createApprovedContextScope(input = {}) {
  return Object.freeze({
    suggestionId: toTrimmedString(input.suggestionId, 'rck-scope-suggestion-placeholder-v1'),
    targetType: toTrimmedString(input.targetType, 'unknown'),
    targetId: toTrimmedString(input.targetId, 'mock-context-scope-target-unknown'),
    targetLabel: toTrimmedString(input.targetLabel, 'Unresolved placeholder target'),
    depth: toFiniteNumber(input.depth, 0),
    includeAnchors: toBoolean(input.includeAnchors, false),
    includeEvidenceRefs: toBoolean(input.includeEvidenceRefs, false),
    includeDocs: toBoolean(input.includeDocs, false),
    selectedArtifacts: toApprovedArtifactList(input.selectedArtifacts, []),
  });
}

function normalizeApprovedContextScope(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return createApprovedContextScope();
  }

  return createApprovedContextScope(candidate);
}

function isApprovedContextScope(candidate) {
  return Boolean(
    candidate &&
      typeof candidate === 'object' &&
      typeof candidate.suggestionId === 'string' &&
      typeof candidate.targetType === 'string' &&
      typeof candidate.targetId === 'string' &&
      typeof candidate.targetLabel === 'string' &&
      typeof candidate.depth === 'number' &&
      Array.isArray(candidate.selectedArtifacts),
  );
}

function createContextPackReference(input = {}) {
  return Object.freeze({
    contextPackId: toTrimmedString(input.contextPackId, 'cp_generation_placeholder_v1'),
    contextPackHash: toTrimmedString(input.contextPackHash, 'sha256:placeholder-generation-context-pack-v1'),
    title: toTrimmedString(input.title, 'RufusChat ContextPack generation placeholder'),
    kind: toTrimmedString(input.kind, 'placeholder'),
  });
}

function createGenerationProvenanceSummary(input = {}) {
  return Object.freeze({
    sourceDocument: toTrimmedString(input.sourceDocument, 'apps/rufuschat-ui/RCK_CONTEXTPACK_GENERATION_CONTRACT.md'),
    requestContractDocument: toTrimmedString(input.requestContractDocument, 'apps/rufuschat-ui/rck-contextpack-generation-contract.mjs'),
    previewContractDocument: toTrimmedString(input.previewContractDocument, 'apps/rufuschat-ui/rck-contextpack-contract.mjs'),
    previewProviderDocument: toTrimmedString(input.previewProviderDocument, 'apps/rufuschat-ui/contextpack-preview-provider.mjs'),
    mode: toTrimmedString(input.mode, 'placeholder'),
    notes: toStringList(input.notes, [
      'Approved scope is converted into a request contract only.',
      'No real TraceSlice or ContextPack work happens in this phase.',
    ]),
  });
}

function createContextPackGenerationRequest(input = {}) {
  const approvedScope = normalizeApprovedContextScope(input.approvedScope ?? input);
  const createdAtUtc = toTrimmedString(input.createdAtUtc, new Date().toISOString());

  return Object.freeze({
    requestId: toTrimmedString(input.requestId, 'rck-context-pack-generation-request-placeholder-v1'),
    createdAtUtc,
    source: 'approved-scope',
    userIntentText: toTrimmedString(input.userIntentText, ''),
    approvedScope,
    requestedOutput: Object.freeze({
      contextPackSchemaVersion: toTrimmedString(
        input.requestedOutput?.contextPackSchemaVersion,
        CONTEXT_PACK_SCHEMA_VERSION,
      ),
      previewOnly: toBoolean(input.requestedOutput?.previewOnly, true),
    }),
    safety: Object.freeze({
      requireUserApprovalForInjection: toBoolean(
        input.safety?.requireUserApprovalForInjection,
        true,
      ),
      allowAutomaticInjection: toBoolean(input.safety?.allowAutomaticInjection, false),
    }),
  });
}

function normalizeContextPackGenerationRequest(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return createContextPackGenerationRequest();
  }

  return createContextPackGenerationRequest(candidate);
}

function isContextPackGenerationRequest(candidate) {
  return Boolean(
    candidate &&
      typeof candidate === 'object' &&
      typeof candidate.requestId === 'string' &&
      typeof candidate.createdAtUtc === 'string' &&
      candidate.source === 'approved-scope' &&
      typeof candidate.userIntentText === 'string' &&
      isApprovedContextScope(candidate.approvedScope) &&
      candidate.requestedOutput &&
      typeof candidate.requestedOutput === 'object' &&
      candidate.safety &&
      typeof candidate.safety === 'object',
  );
}

function createContextPackGenerationResponse(input = {}) {
  const request = normalizeContextPackGenerationRequest(input.request ?? input);
  const status = CONTEXT_PACK_GENERATION_STATUS_VALUES.has(input.status) ? input.status : 'not_connected';
  const contextPackReference = input.contextPackReference === null
    ? null
    : createContextPackReference(input.contextPackReference ?? input);
  const contextPackPreview = input.contextPackPreview === null ? null : input.contextPackPreview ?? null;

  return Object.freeze({
    requestId: toTrimmedString(input.requestId, request.requestId),
    status,
    contextPackReference,
    contextPackPreview,
    warnings: toStringList(input.warnings, DEFAULT_GENERATION_WARNINGS),
    constraints: toStringList(input.constraints, DEFAULT_GENERATION_CONSTRAINTS),
    provenanceSummary: createGenerationProvenanceSummary(input.provenanceSummary ?? input),
    message: toTrimmedString(
      input.message,
      'Not connected to real RCK generation in this phase.',
    ),
  });
}

function normalizeContextPackGenerationResponse(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return createContextPackGenerationResponse();
  }

  return createContextPackGenerationResponse(candidate);
}

function isContextPackGenerationResponse(candidate) {
  return Boolean(
    candidate &&
      typeof candidate === 'object' &&
      typeof candidate.requestId === 'string' &&
      typeof candidate.status === 'string' &&
      Array.isArray(candidate.warnings) &&
      Array.isArray(candidate.constraints) &&
      typeof candidate.provenanceSummary === 'object' &&
      typeof candidate.message === 'string',
  );
}

export {
  CONTEXT_PACK_SCHEMA_VERSION,
  CONTEXT_PACK_GENERATION_STATUS_VALUES,
  createApprovedContextScope,
  normalizeApprovedContextScope,
  isApprovedContextScope,
  createContextPackGenerationRequest,
  normalizeContextPackGenerationRequest,
  isContextPackGenerationRequest,
  createContextPackGenerationResponse,
  normalizeContextPackGenerationResponse,
  isContextPackGenerationResponse,
  createContextPackReference,
  createGenerationProvenanceSummary,
};
