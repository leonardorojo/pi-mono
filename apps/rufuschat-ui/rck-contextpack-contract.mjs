const CONTEXT_PACK_PREVIEW_SOURCE_DOCUMENT = 'docs/RUFUSCHAT_ADAPTER_DESIGN.md';
const CONTEXT_PACK_PREVIEW_CONTRACT_DOCUMENT = 'docs/CONTEXT_PACK_BOUNDARY.md';
const CONTEXT_PACK_PREVIEW_SCHEMA_DOCUMENT = 'schemas/rck.context_pack.v0.schema.json';

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

function toPreviewSection(section = {}, index = 0) {
  const fallbackTitle = `Section ${index + 1}`;

  return Object.freeze({
    id: toTrimmedString(section.id, `section_${index + 1}`),
    title: toTrimmedString(section.title, fallbackTitle),
    visible: section.visible !== false,
    summary: toTrimmedString(section.summary, 'Placeholder section summary.'),
    text: toTrimmedString(section.text, 'Placeholder section content.'),
  });
}

function toProvenanceSummary(input = {}) {
  return Object.freeze({
    sourceDocument: toTrimmedString(input.sourceDocument, CONTEXT_PACK_PREVIEW_SOURCE_DOCUMENT),
    contractDocument: toTrimmedString(input.contractDocument, CONTEXT_PACK_PREVIEW_CONTRACT_DOCUMENT),
    schemaDocument: toTrimmedString(input.schemaDocument, CONTEXT_PACK_PREVIEW_SCHEMA_DOCUMENT),
    publishedCommit: toTrimmedString(input.publishedCommit, '048d4c3'),
    mode: toTrimmedString(input.mode, 'placeholder'),
    notes: toStringList(input.notes, ['Mock provenance only.', 'No RCK Core internals are read in this phase.']),
  });
}

function toInjectionPolicy(input = {}) {
  return Object.freeze({
    canPreview: input.canPreview !== false,
    canConfirm: input.canConfirm === true ? true : false,
    canPersistRecord: input.canPersistRecord === true ? true : false,
    canReadRckFilesystem: input.canReadRckFilesystem === true ? true : false,
    canCallRckCore: input.canCallRckCore === true ? true : false,
    reason: toTrimmedString(
      input.reason,
      'Phase 19 is a contract/placeholder preview only. Injection and persistence are not available yet.',
    ),
  });
}

function toInjectionRecordDraft(input = {}) {
  return Object.freeze({
    status: toTrimmedString(input.status, 'draft'),
    recordId: null,
    createdAt: null,
    updatedAt: null,
    injectedAt: null,
    contextPackId: toTrimmedString(input.contextPackId, ''),
    contextPackHash: toTrimmedString(input.contextPackHash, ''),
    notes: toStringList(input.notes, ['No injection record is persisted in this phase.']),
  });
}

export function createContextPackReference(input = {}) {
  const contextPackId = toTrimmedString(input.contextPackId, 'cp_preview_placeholder_v1');
  const contextPackHash = toTrimmedString(input.contextPackHash, 'sha256:placeholder-context-pack-v1');

  return Object.freeze({
    contextPackId,
    contextPackHash,
    title: toTrimmedString(input.title, 'RufusChat ContextPack preview placeholder'),
    kind: toTrimmedString(input.kind, 'placeholder'),
  });
}

export function createContextPackPreviewSection(section, index = 0) {
  return toPreviewSection(section, index);
}

export function createContextPackProvenanceSummary(input = {}) {
  return toProvenanceSummary(input);
}

export function createContextPackInjectionPolicy(input = {}) {
  return toInjectionPolicy(input);
}

export function createContextPackInjectionRecordDraft(input = {}) {
  return toInjectionRecordDraft(input);
}

export function createContextPackPreview(input = {}) {
  const reference = createContextPackReference(input.reference ?? input);
  const sectionsVisible = Array.isArray(input.sectionsVisible)
    ? input.sectionsVisible.map((section, index) => createContextPackPreviewSection(section, index))
    : [
        createContextPackPreviewSection(
          {
            id: 'summary',
            title: 'Summary',
            summary: 'Placeholder only. No live RCK preview is connected.',
            text: 'This phase only exposes a contract-shaped mock preview for RufusChat.',
          },
          0,
        ),
        createContextPackPreviewSection(
          {
            id: 'provenance',
            title: 'Provenance',
            summary: 'Source docs and commit reference for the contract preview.',
            text: `${CONTEXT_PACK_PREVIEW_SOURCE_DOCUMENT} · ${CONTEXT_PACK_PREVIEW_CONTRACT_DOCUMENT} · ${CONTEXT_PACK_PREVIEW_SCHEMA_DOCUMENT}`,
          },
          1,
        ),
        createContextPackPreviewSection(
          {
            id: 'constraints',
            title: 'Constraints',
            summary: 'No RCK reads, no trace slice generation, no injection.',
            text: 'Placeholder-only contract. This panel is dev-preview and not connected to RCK Core.',
          },
          2,
        ),
      ];
  const hasExplicitPhase = typeof input.phase === 'number' && Number.isFinite(input.phase);
  const hasExplicitPreviewMode = typeof input.previewMode === 'string' && input.previewMode.trim();
  const hasExplicitPlaceholder = typeof input.placeholder === 'boolean';
  const hasExplicitEstimatedTokenCost = typeof input.estimatedTokenCost === 'number';
  const hasExplicitWarnings = Array.isArray(input.warnings);
  const hasExplicitConstraints = Array.isArray(input.constraints);
  const hasExplicitExactText = typeof input.exactTextToInject === 'string';
  const hasExplicitUserApprovalStatus = typeof input.userApprovalStatus === 'string' && input.userApprovalStatus.trim();
  const hasExplicitSourceDocuments = Array.isArray(input.sourceDocuments);

  return Object.freeze({
    phase: hasExplicitPhase ? input.phase : 19,
    previewMode: hasExplicitPreviewMode ? input.previewMode.trim() : 'placeholder',
    placeholder: hasExplicitPlaceholder ? input.placeholder : true,
    source: typeof input.source === 'string' && input.source.trim() ? input.source.trim() : 'placeholder',
    contextPackId: reference.contextPackId,
    contextPackHash: reference.contextPackHash,
    title: toTrimmedString(input.title, reference.title),
    sourceTraceSliceHashes: toStringList(input.sourceTraceSliceHashes, ['trace-slice-placeholder-a', 'trace-slice-placeholder-b']),
    sectionsVisible,
    estimatedTokenCost: hasExplicitEstimatedTokenCost ? input.estimatedTokenCost : null,
    warnings: hasExplicitWarnings
      ? toStringList(input.warnings, [])
      : [
          'Placeholder / dev-only preview.',
          'No RCK Core integration is active yet.',
          'Confirm injection is disabled in this phase.',
        ],
    constraints: hasExplicitConstraints
      ? toStringList(input.constraints, [])
      : [
          'Do not read .rck directly.',
          'Do not execute RCK Core.',
          'Do not generate a real ContextPack yet.',
          'Do not persist injection records yet.',
        ],
    provenanceSummary: createContextPackProvenanceSummary(input.provenanceSummary ?? input),
    exactTextToInject: hasExplicitExactText
      ? toTrimmedString(input.exactTextToInject, '')
      : 'Not available in this phase. Placeholder preview only.',
    userApprovalStatus: hasExplicitUserApprovalStatus ? input.userApprovalStatus.trim() : 'not-available',
    injectionPolicy: createContextPackInjectionPolicy(input.injectionPolicy ?? input),
    injectionRecordDraft: createContextPackInjectionRecordDraft({
      ...(input.injectionRecordDraft ?? {}),
      contextPackId: reference.contextPackId,
      contextPackHash: reference.contextPackHash,
    }),
    reference,
    sourceDocuments: hasExplicitSourceDocuments
      ? [...input.sourceDocuments]
      : [
          CONTEXT_PACK_PREVIEW_SOURCE_DOCUMENT,
          CONTEXT_PACK_PREVIEW_CONTRACT_DOCUMENT,
          CONTEXT_PACK_PREVIEW_SCHEMA_DOCUMENT,
        ],
  });
}

export function normalizeContextPackPreview(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return createContextPackPreview();
  }

  return createContextPackPreview(candidate);
}

export function isContextPackPreview(candidate) {
  return Boolean(
    candidate &&
      typeof candidate === 'object' &&
      typeof candidate.contextPackId === 'string' &&
      typeof candidate.contextPackHash === 'string' &&
      Array.isArray(candidate.sectionsVisible) &&
      typeof candidate.provenanceSummary === 'object' &&
      typeof candidate.exactTextToInject === 'string',
  );
}
