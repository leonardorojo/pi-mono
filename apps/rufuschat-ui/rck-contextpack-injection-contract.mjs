const INJECTION_SOURCE = 'loaded-contextpack-json';
const INJECTION_STATUS_VALUES = new Set(['pending', 'injected', 'blocked', 'failed']);
const INJECTION_APPROVAL_MODE = 'explicit-click';
const INJECTION_APPROVER = 'local-user';

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

function toObject(value, fallback = {}) {
  return value && typeof value === 'object' && !Array.isArray(value) ? value : fallback;
}

function createContextPackReference(input = {}) {
  return Object.freeze({
    contextPackId: toTrimmedString(input.contextPackId, 'loaded-contextpack-rck-preview-v1'),
    contextPackHash: toTrimmedString(input.contextPackHash, 'sha256:loaded-contextpack-preview'),
    title: toTrimmedString(input.title, 'Loaded ContextPack preview'),
    kind: toTrimmedString(input.kind, 'loaded-json'),
  });
}

function normalizeLoadedPreview(input = {}) {
  const source = toObject(input);
  const reference = createContextPackReference(source.reference ?? source.contextPackReference ?? source);
  const sectionsVisible = Array.isArray(source.sectionsVisible) ? source.sectionsVisible : [];
  const sourceTraceSliceHashes = toStringList(source.sourceTraceSliceHashes, []);
  const provenanceSummary = toObject(source.provenanceSummary, {});
  const warnings = toStringList(source.warnings, []);
  const constraints = toStringList(source.constraints, []);
  const exactTextToInject = toTrimmedString(source.exactTextToInject, '');

  return Object.freeze({
    source: INJECTION_SOURCE,
    loadedFromJson: source.loadedFromJson !== false,
    placeholder: false,
    contextPackId: reference.contextPackId,
    contextPackHash: reference.contextPackHash,
    reference,
    sourceTraceSliceHashes,
    sectionsVisible,
    exactTextToInject,
    provenanceSummary: Object.freeze({
      sourceDocument: toTrimmedString(provenanceSummary.sourceDocument, 'apps/rufuschat-ui/RCK_LOAD_CONTEXTPACK_JSON_PREVIEW.md'),
      contractDocument: toTrimmedString(provenanceSummary.contractDocument, 'apps/rufuschat-ui/RCK_CONTEXTPACK_PREVIEW.md'),
      schemaDocument: toTrimmedString(provenanceSummary.schemaDocument, 'rck.context_pack.v0'),
      publishedCommit: toTrimmedString(provenanceSummary.publishedCommit, 'manual-json'),
      mode: toTrimmedString(provenanceSummary.mode, 'loaded-json'),
      notes: toStringList(provenanceSummary.notes, [
        'Loaded manually from JSON.',
        'RufusChat did not generate this ContextPack automatically.',
        'No Anchor recording exists in this phase.',
      ]),
    }),
    warnings,
    constraints,
    injectionPolicy: Object.freeze({
      requiresUserApproval: source.injectionPolicy?.requiresUserApproval !== false,
      allowAutomaticInjection: source.injectionPolicy?.allowAutomaticInjection === true,
      canConfirm: source.injectionPolicy?.canConfirm !== false,
      canPersistRecord: source.injectionPolicy?.canPersistRecord === true,
      reason: toTrimmedString(
        source.injectionPolicy?.reason,
        'Loaded JSON preview. Confirm injection is available as an explicit user action only.',
      ),
    }),
  });
}

function normalizeInjectionSection(section, index = 0) {
  const source = toObject(section);
  return Object.freeze({
    id: toTrimmedString(source.id, `section-${index + 1}`),
    title: toTrimmedString(source.title, `Section ${index + 1}`),
    summary: toTrimmedString(source.summary, ''),
    visible: source.visible !== false,
  });
}

function normalizeInjectionSectionList(value) {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.map((section, index) => normalizeInjectionSection(section, index)).filter((section) => section.visible);
}

function createContextPackInjectionRequest(input = {}) {
  const loadedPreview = normalizeLoadedPreview(input.loadedPreview ?? input.preview ?? input.contextPackPreview ?? {});
  const exactTextToInject = toTrimmedString(input.exactTextToInject ?? loadedPreview.exactTextToInject, '');
  const createdAtUtc = toTrimmedString(input.createdAtUtc, new Date().toISOString());

  return Object.freeze({
    requestId: toTrimmedString(input.requestId, `rck-context-pack-injection-request-${crypto.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`}`),
    createdAtUtc,
    chatId: toTrimmedString(input.chatId, ''),
    projectId: toTrimmedString(input.projectId, ''),
    source: INJECTION_SOURCE,
    contextPackReference: loadedPreview.reference,
    sourceTraceSliceHashes: loadedPreview.sourceTraceSliceHashes,
    injectedSections: normalizeInjectionSectionList(loadedPreview.sectionsVisible).map((section) => section.id),
    exactTextToInject,
    provenanceSummary: loadedPreview.provenanceSummary,
    warnings: loadedPreview.warnings,
    constraints: loadedPreview.constraints,
    approvedBy: INJECTION_APPROVER,
    approvalMode: INJECTION_APPROVAL_MODE,
    status: 'pending',
    requiresUserApproval: loadedPreview.injectionPolicy.requiresUserApproval,
    allowAutomaticInjection: loadedPreview.injectionPolicy.allowAutomaticInjection,
  });
}

function normalizeContextPackInjectionRequest(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return createContextPackInjectionRequest();
  }

  return createContextPackInjectionRequest(candidate);
}

function isContextPackInjectionRequest(candidate) {
  return Boolean(
    candidate &&
      typeof candidate === 'object' &&
      typeof candidate.requestId === 'string' &&
      typeof candidate.createdAtUtc === 'string' &&
      candidate.source === INJECTION_SOURCE &&
      candidate.contextPackReference &&
      typeof candidate.contextPackReference === 'object' &&
      typeof candidate.exactTextToInject === 'string' &&
      Array.isArray(candidate.sourceTraceSliceHashes) &&
      Array.isArray(candidate.injectedSections),
  );
}

function createContextPackInjectionRecord(input = {}) {
  const request = normalizeContextPackInjectionRequest(input.request ?? input);
  const createdAtUtc = toTrimmedString(input.createdAtUtc, request.createdAtUtc);

  return Object.freeze({
    injectionId: toTrimmedString(input.injectionId, `rck-context-pack-injection-${crypto.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`}`),
    createdAtUtc,
    chatId: request.chatId,
    projectId: request.projectId,
    source: request.source,
    contextPackReference: request.contextPackReference,
    sourceTraceSliceHashes: request.sourceTraceSliceHashes,
    injectedSections: request.injectedSections,
    exactTextInjected: request.exactTextToInject,
    provenanceSummary: request.provenanceSummary,
    warnings: request.warnings,
    constraints: request.constraints,
    approvedBy: INJECTION_APPROVER,
    approvalMode: INJECTION_APPROVAL_MODE,
    status: 'injected',
    resultingAnchorId: null,
    deliveryMode: 'visual-only',
    llmRequestIncluded: false,
  });
}

function normalizeContextPackInjectionRecord(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return createContextPackInjectionRecord();
  }

  return createContextPackInjectionRecord(candidate);
}

function isContextPackInjectionRecord(candidate) {
  return Boolean(
    candidate &&
      typeof candidate === 'object' &&
      typeof candidate.injectionId === 'string' &&
      typeof candidate.createdAtUtc === 'string' &&
      typeof candidate.chatId === 'string' &&
      typeof candidate.projectId === 'string' &&
      candidate.source === INJECTION_SOURCE &&
      candidate.contextPackReference &&
      typeof candidate.contextPackReference === 'object' &&
      typeof candidate.exactTextInjected === 'string' &&
      candidate.status === 'injected' &&
      candidate.approvedBy === INJECTION_APPROVER &&
      candidate.approvalMode === INJECTION_APPROVAL_MODE,
  );
}

function createContextPackInjectionResult(input = {}) {
  const request = normalizeContextPackInjectionRequest(input.request ?? input);
  const injectionRecord = createContextPackInjectionRecord({
    ...input,
    request,
  });

  return Object.freeze({
    ok: true,
    request,
    injectionRecord,
    deliveryMode: 'visual-only',
    shouldSendToLlm: false,
    message: 'Context injected into this chat session.',
  });
}

function normalizeContextPackInjectionResult(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return createContextPackInjectionResult();
  }

  const request = normalizeContextPackInjectionRequest(candidate.request ?? candidate);
  const injectionRecord = normalizeContextPackInjectionRecord(candidate.injectionRecord ?? candidate);

  return Object.freeze({
    ok: candidate.ok !== false,
    request,
    injectionRecord,
    deliveryMode: toTrimmedString(candidate.deliveryMode, 'visual-only'),
    shouldSendToLlm: candidate.shouldSendToLlm === true,
    message: toTrimmedString(candidate.message, 'Context injected into this chat session.'),
  });
}

function canConfirmContextPackInjection(candidate) {
  const request = normalizeContextPackInjectionRequest(candidate);
  return Boolean(
    request.source === INJECTION_SOURCE &&
      request.contextPackReference &&
      typeof request.contextPackReference === 'object' &&
      typeof request.exactTextToInject === 'string' &&
      request.exactTextToInject.trim().length > 0 &&
      Array.isArray(request.sourceTraceSliceHashes) &&
      request.sourceTraceSliceHashes.length > 0,
  );
}

export {
  INJECTION_APPROVAL_MODE,
  INJECTION_APPROVER,
  INJECTION_SOURCE,
  INJECTION_STATUS_VALUES,
  createContextPackInjectionRecord,
  createContextPackInjectionRequest,
  createContextPackInjectionResult,
  createContextPackReference,
  canConfirmContextPackInjection,
  isContextPackInjectionRecord,
  isContextPackInjectionRequest,
  normalizeContextPackInjectionRecord,
  normalizeContextPackInjectionRequest,
  normalizeContextPackInjectionResult,
  normalizeLoadedPreview,
};
