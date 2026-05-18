import { createContextPackPreview, createContextPackReference, normalizeContextPackPreview } from './rck-contextpack-contract.mjs';

const CONTEXT_PACK_SCHEMA_VERSION = 'rck.context_pack.v0';
const MAX_CONTEXT_PACK_JSON_CHARS = 200000;
const LOADED_PREVIEW_REFERENCE_KIND = 'loaded-json';
const LOADED_PREVIEW_SOURCE = 'loaded-contextpack-json';

class ContextPackValidationError extends Error {
  constructor(message, issues = []) {
    super(message);
    this.name = 'ContextPackValidationError';
    this.code = 'invalid_context_pack';
    this.issues = Array.isArray(issues) ? issues : [];
  }
}

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

function toNumber(value, fallback = null) {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function toObject(value, fallback = {}) {
  return value && typeof value === 'object' && !Array.isArray(value) ? value : fallback;
}

function compactText(value, maxLength = 120) {
  if (typeof value !== 'string') {
    return '';
  }

  const normalized = value.replace(/\s+/g, ' ').trim();
  if (normalized.length <= maxLength) {
    return normalized;
  }

  return `${normalized.slice(0, Math.max(0, maxLength - 1)).trimEnd()}…`;
}

function resolveContextPackPayload(candidate) {
  if (!candidate || typeof candidate !== 'object' || Array.isArray(candidate)) {
    return null;
  }

  if (typeof candidate.schemaVersion === 'string' || Array.isArray(candidate.sourceTraceSlices) || candidate.structuralContext || candidate.narrativeContext || candidate.injectionInstructions || candidate.injectionPolicy) {
    return candidate;
  }

  if (candidate.contextPack && typeof candidate.contextPack === 'object' && !Array.isArray(candidate.contextPack)) {
    if (typeof candidate.contextPack.schemaVersion === 'string' || Array.isArray(candidate.contextPack.sourceTraceSlices) || candidate.contextPack.structuralContext || candidate.contextPack.narrativeContext || candidate.contextPack.injectionInstructions || candidate.contextPack.injectionPolicy) {
      return candidate.contextPack;
    }

    if (candidate.contextPack.title || candidate.contextPack.purpose) {
      return candidate;
    }
  }

  return candidate;
}

function normalizeSourceTraceSlice(slice, index = 0) {
  const source = toObject(slice);
  return Object.freeze({
    traceSliceHash: toTrimmedString(source.traceSliceHash, `trace-slice-${index + 1}`),
    traceSliceId: toTrimmedString(source.traceSliceId, ''),
    traceId: toTrimmedString(source.traceId, ''),
    title: toTrimmedString(source.title, ''),
  });
}

function normalizeProvenance(provenance = {}) {
  const source = toObject(provenance);
  return Object.freeze({
    traceSliceHashes: toStringList(source.traceSliceHashes, []),
    stateIds: toStringList(source.stateIds, []),
    deltaIds: toStringList(source.deltaIds, []),
    anchorIds: toStringList(source.anchorIds, []),
    evidenceRefIds: toStringList(source.evidenceRefIds, []),
    docIds: toStringList(source.docIds, []),
  });
}

function normalizePreviewSection(section, index = 0, kind = 'section') {
  const source = toObject(section);
  const title = toTrimmedString(source.title, `${kind === 'narrative' ? 'Narrative' : 'Structural'} section ${index + 1}`);
  const content = toTrimmedString(source.content, toTrimmedString(source.summary, ''));
  const provenance = normalizeProvenance(source.provenance);
  const visible = source.visible !== false;
  const summary = toTrimmedString(source.summary, compactText(content || title, 160) || 'Preview section.');

  return Object.freeze({
    id: toTrimmedString(source.id, `${kind}_section_${index + 1}`),
    title,
    visible,
    summary,
    text: content,
    provenance,
  });
}

function normalizeSectionList(sections, kind) {
  if (!Array.isArray(sections)) {
    return [];
  }

  return sections.map((section, index) => normalizePreviewSection(section, index, kind));
}

function validateContextPackShape(candidate) {
  const payload = resolveContextPackPayload(candidate);
  const issues = [];
  const warnings = [];

  if (!payload) {
    issues.push('ContextPack JSON must be a JSON object.');
    return { ok: false, payload: null, issues, warnings };
  }

  const schemaVersion = toTrimmedString(payload.schemaVersion, '');
  if (!schemaVersion) {
    issues.push('Missing schemaVersion. Expected rck.context_pack.v0.');
  } else if (schemaVersion !== CONTEXT_PACK_SCHEMA_VERSION) {
    issues.push(`Unsupported schemaVersion "${schemaVersion}". Expected rck.context_pack.v0.`);
  }

  if (!payload.contextPack || typeof payload.contextPack !== 'object' || Array.isArray(payload.contextPack)) {
    issues.push('Missing contextPack metadata object.');
  }

  if (!Array.isArray(payload.sourceTraceSlices)) {
    issues.push('Missing sourceTraceSlices array.');
  }

  if (!payload.structuralContext || typeof payload.structuralContext !== 'object' || Array.isArray(payload.structuralContext)) {
    issues.push('Missing structuralContext object.');
  }

  if (!payload.narrativeContext || typeof payload.narrativeContext !== 'object' || Array.isArray(payload.narrativeContext)) {
    issues.push('Missing narrativeContext object.');
  }

  if (!payload.injectionInstructions || typeof payload.injectionInstructions !== 'object' || Array.isArray(payload.injectionInstructions)) {
    issues.push('Missing injectionInstructions object.');
  }

  if (!payload.injectionPolicy || typeof payload.injectionPolicy !== 'object' || Array.isArray(payload.injectionPolicy)) {
    issues.push('Missing injectionPolicy object.');
  }

  if (payload.contextPack && typeof payload.contextPack === 'object' && !Array.isArray(payload.contextPack)) {
    const contextPack = payload.contextPack;
    if (!toTrimmedString(contextPack.title, '')) {
      warnings.push('Loaded ContextPack JSON does not include a title.');
    }
  }

  if (Array.isArray(payload.sourceTraceSlices) && payload.sourceTraceSlices.length === 0) {
    warnings.push('Loaded ContextPack JSON has no source trace slices.');
  }

  const structuralSections = Array.isArray(payload.structuralContext?.sections) ? payload.structuralContext.sections : [];
  const narrativeSections = Array.isArray(payload.narrativeContext?.sections) ? payload.narrativeContext.sections : [];
  if (structuralSections.length === 0 && narrativeSections.length === 0) {
    warnings.push('Loaded ContextPack JSON has no visible sections yet.');
  }

  const exactTextToInject = toTrimmedString(payload.injectionInstructions?.exactTextToInject, '');
  if (!exactTextToInject) {
    warnings.push('Loaded ContextPack JSON does not include exactTextToInject.');
  }

  return {
    ok: issues.length === 0,
    payload,
    issues,
    warnings,
  };
}

export function parseContextPackJson(input) {
  if (typeof input === 'string') {
    const raw = input.trim();
    if (!raw) {
      throw new ContextPackValidationError('ContextPack JSON is empty.', ['ContextPack JSON must not be empty.']);
    }

    if (raw.length > MAX_CONTEXT_PACK_JSON_CHARS) {
      throw new ContextPackValidationError(
        `ContextPack JSON is too large. Limit is ${MAX_CONTEXT_PACK_JSON_CHARS} characters.`,
        [`Paste a smaller ContextPack JSON payload. Limit is ${MAX_CONTEXT_PACK_JSON_CHARS} characters.`],
      );
    }

    try {
      return { value: JSON.parse(raw), warnings: [] };
    } catch {
      throw new ContextPackValidationError('ContextPack JSON is not valid JSON.', ['Request body must contain valid JSON.']);
    }
  }

  if (input && typeof input === 'object' && !Array.isArray(input)) {
    return { value: input, warnings: [] };
  }

  throw new ContextPackValidationError('ContextPack JSON must be an object or JSON string.', ['Provide a JSON object or a JSON string.']);
}

export function normalizeLoadedContextPack(candidate) {
  const validation = validateContextPackShape(candidate);
  if (!validation.ok) {
    throw new ContextPackValidationError('Invalid ContextPack JSON.', validation.issues);
  }

  const payload = validation.payload;
  const metadata = toObject(payload.contextPack, {});
  const schemaVersion = toTrimmedString(payload.schemaVersion, CONTEXT_PACK_SCHEMA_VERSION);
  const contextPackTitle = toTrimmedString(metadata.title, 'Loaded ContextPack preview');
  const contextPackPurpose = toTrimmedString(metadata.purpose, 'chat-context-injection');
  const sourceTraceSlices = Array.isArray(payload.sourceTraceSlices)
    ? payload.sourceTraceSlices.map((slice, index) => normalizeSourceTraceSlice(slice, index))
    : [];
  const sourceTraceSliceHashes = sourceTraceSlices.map((slice) => slice.traceSliceHash).filter(Boolean);
  const structuralSections = normalizeSectionList(payload.structuralContext?.sections, 'structural');
  const narrativeSections = normalizeSectionList(payload.narrativeContext?.sections, 'narrative');
  const exactTextToInject = toTrimmedString(payload.injectionInstructions?.exactTextToInject, '');
  const injectionInstructions = Object.freeze({
    exactTextToInject,
    constraints: toStringList(payload.injectionInstructions?.constraints, []),
    warnings: toStringList(payload.injectionInstructions?.warnings, []),
  });
  const injectionPolicy = Object.freeze({
    requiresUserApproval: toBoolean(payload.injectionPolicy?.requiresUserApproval, true),
    allowAutomaticInjection: toBoolean(payload.injectionPolicy?.allowAutomaticInjection, false),
    intendedUse: toTrimmedString(payload.injectionPolicy?.intendedUse, 'chat-context-injection'),
  });
  const contextPack = Object.freeze({
    title: contextPackTitle,
    purpose: contextPackPurpose,
  });
  const warnings = [
    ...validation.warnings,
    ...injectionInstructions.warnings,
    'Loaded manually from JSON. RufusChat did not generate this ContextPack automatically.',
  ];
  const constraints = [
    ...injectionInstructions.constraints,
    'Confirm injection remains disabled in this phase.',
  ];

  return Object.freeze({
    schemaVersion,
    source: LOADED_PREVIEW_SOURCE,
    contextPack,
    sourceTraceSlices,
    sourceTraceSliceHashes,
    structuralContext: Object.freeze({ sections: structuralSections }),
    narrativeContext: Object.freeze({ sections: narrativeSections }),
    injectionInstructions,
    injectionPolicy,
    warnings,
    constraints,
    title: contextPackTitle,
    purpose: contextPackPurpose,
    exactTextToInject,
  });
}

export function buildContextPackPreviewFromLoadedContextPack(candidate) {
  const loaded = normalizeLoadedContextPack(candidate);
  const previewSections = [
    ...loaded.structuralContext.sections.map((section, index) =>
      normalizePreviewSection(
        {
          id: section.id,
          title: section.title,
          visible: true,
          summary: section.summary,
          content: section.text,
          provenance: section.provenance,
        },
        index,
        'structural',
      ),
    ),
    ...loaded.narrativeContext.sections.map((section, index) =>
      normalizePreviewSection(
        {
          id: section.id,
          title: section.title,
          visible: true,
          summary: section.summary,
          content: section.text,
          provenance: section.provenance,
        },
        index,
        'narrative',
      ),
    ),
  ];

  const reference = createContextPackReference({
    contextPackId: `loaded-contextpack-${loaded.schemaVersion}`,
    contextPackHash: 'sha256:manual-loaded-contextpack-preview',
    title: `Loaded ContextPack preview — ${loaded.title}`,
    kind: LOADED_PREVIEW_REFERENCE_KIND,
  });

  return normalizeContextPackPreview({
    phase: 23,
    previewMode: 'loaded-json',
    placeholder: false,
    loadedFromJson: true,
    source: LOADED_PREVIEW_SOURCE,
    schemaVersion: loaded.schemaVersion,
    contextPackId: reference.contextPackId,
    contextPackHash: reference.contextPackHash,
    title: `Loaded ContextPack preview — ${loaded.title}`,
    contextPackTitle: loaded.title,
    contextPackPurpose: loaded.purpose,
    sourceTraceSliceHashes: loaded.sourceTraceSliceHashes,
    sectionsVisible: previewSections,
    estimatedTokenCost: null,
    warnings: loaded.warnings,
    constraints: loaded.constraints,
    provenanceSummary: {
      sourceDocument: 'apps/rufuschat-ui/RCK_LOAD_CONTEXTPACK_JSON_PREVIEW.md',
      contractDocument: 'apps/rufuschat-ui/RCK_CONTEXTPACK_PREVIEW.md',
      schemaDocument: loaded.schemaVersion,
      publishedCommit: 'manual-json',
      mode: 'loaded-json',
      notes: [
        'Loaded manually from JSON. RufusChat did not generate this ContextPack automatically.',
        'No RCK Core execution happened.',
        'Confirm injection remains disabled.',
      ],
    },
    exactTextToInject: loaded.exactTextToInject,
    userApprovalStatus: loaded.injectionPolicy.requiresUserApproval ? 'requires-user-approval' : 'approval-not-required',
    injectionPolicy: {
      canPreview: true,
      canConfirm: true,
      canPersistRecord: false,
      canReadRckFilesystem: false,
      canCallRckCore: false,
      reason: 'Loaded manually from JSON. Confirm injection is available only after the exact text is visible.',
    },
    injectionRecordDraft: {
      status: 'not-available',
      notes: [
        'Manual preview only. No injection record is persisted in this phase.',
      ],
    },
    reference,
    sourceDocuments: [
      'apps/rufuschat-ui/RCK_LOAD_CONTEXTPACK_JSON_PREVIEW.md',
      'apps/rufuschat-ui/RCK_CONTEXTPACK_PREVIEW.md',
      'schemas/rck.context_pack.v0.schema.json',
    ],
    loadedContextPack: loaded,
    source: LOADED_PREVIEW_SOURCE,
  });
}

export function isContextPackValidationError(error) {
  return Boolean(error && typeof error === 'object' && error.code === 'invalid_context_pack');
}

export { ContextPackValidationError, CONTEXT_PACK_SCHEMA_VERSION };
