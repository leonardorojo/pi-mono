const APPROVED_CONTEXT_KIND = 'rck-approved-context';
const APPROVED_CONTEXT_SOURCE = 'contextpack-injection-record';
const APPROVED_CONTEXT_APPROVER = 'local-user';
const APPROVED_CONTEXT_APPROVAL_MODE = 'explicit-click';

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function toTrimmedString(value) {
  return typeof value === 'string' ? value.trim() : '';
}

function toStringList(value) {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.map((item) => toTrimmedString(item)).filter(Boolean);
}

function normalizeReference(candidate) {
  const source = isPlainObject(candidate) ? candidate : {};
  const contextPackId = toTrimmedString(source.contextPackId);
  const contextPackHash = toTrimmedString(source.contextPackHash);

  if (!contextPackId || !contextPackHash) {
    return null;
  }

  return Object.freeze({
    contextPackId,
    contextPackHash,
    title: toTrimmedString(source.title) || 'Loaded ContextPack',
    kind: toTrimmedString(source.kind) || 'loaded-json',
  });
}

function normalizeApprovedContextRecord(candidate) {
  if (!isPlainObject(candidate)) {
    return null;
  }

  const source = isPlainObject(candidate.injectionRecord) ? candidate.injectionRecord : candidate;
  const sourceReference = normalizeReference(candidate.contextPackReference ?? source.contextPackReference);
  const exactTextInjected = toTrimmedString(candidate.exactTextInjected ?? source.exactTextInjected);
  const injectionId = toTrimmedString(candidate.injectionId ?? source.injectionId);
  const approvalMode = toTrimmedString(candidate.approvalMode ?? source.approvalMode) || APPROVED_CONTEXT_APPROVAL_MODE;
  const approvedBy = toTrimmedString(candidate.approvedBy ?? source.approvedBy) || APPROVED_CONTEXT_APPROVER;
  const sourceTraceSliceHashes = toStringList(candidate.sourceTraceSliceHashes ?? source.sourceTraceSliceHashes);
  const warnings = toStringList(candidate.warnings ?? source.warnings);
  const constraints = toStringList(candidate.constraints ?? source.constraints);
  const provenanceSource = isPlainObject(candidate.provenanceSummary) ? candidate.provenanceSummary : source.provenanceSummary;
  const provenanceSummary = isPlainObject(provenanceSource) ? provenanceSource : {};
  const createdAtUtc = toTrimmedString(candidate.createdAtUtc ?? source.createdAtUtc);
  const consumedAtUtc = toTrimmedString(candidate.consumedAtUtc ?? source.consumedAtUtc);
  const approvalState = toTrimmedString(candidate.approvalState ?? source.approvalState);

  if (!injectionId || !sourceReference || !exactTextInjected) {
    return null;
  }

  if (approvalMode !== APPROVED_CONTEXT_APPROVAL_MODE || approvedBy !== APPROVED_CONTEXT_APPROVER) {
    return null;
  }

  if (approvalState === 'consumed' || consumedAtUtc) {
    return null;
  }

  return Object.freeze({
    kind: APPROVED_CONTEXT_KIND,
    source: APPROVED_CONTEXT_SOURCE,
    injectionId,
    contextPackReference: sourceReference,
    sourceTraceSliceHashes,
    exactTextInjected,
    warnings,
    constraints,
    provenanceSummary: Object.freeze({
      sourceDocument: toTrimmedString(provenanceSummary.sourceDocument),
      contractDocument: toTrimmedString(provenanceSummary.contractDocument),
      schemaDocument: toTrimmedString(provenanceSummary.schemaDocument),
      publishedCommit: toTrimmedString(provenanceSummary.publishedCommit),
      mode: toTrimmedString(provenanceSummary.mode),
      notes: toStringList(provenanceSummary.notes),
    }),
    approvedBy,
    approvalMode,
    createdAtUtc,
  });
}

function formatList(lines, emptyLabel = '- none') {
  if (!lines.length) {
    return [emptyLabel];
  }

  return lines.map((line) => `- ${line}`);
}

function formatProvenanceSummary(provenanceSummary) {
  const entries = [];
  for (const [key, value] of Object.entries(provenanceSummary)) {
    if (Array.isArray(value)) {
      entries.push(`${key}: ${value.join(' | ')}`);
    } else if (typeof value === 'string' && value) {
      entries.push(`${key}: ${value}`);
    }
  }

  return entries.length > 0 ? entries : ['- none'];
}

export function normalizeApprovedContextForCompletion(candidate) {
  return normalizeApprovedContextRecord(candidate);
}

export function buildApprovedContextBlock(candidate) {
  const approvedContext = normalizeApprovedContextForCompletion(candidate);
  if (!approvedContext || !approvedContext.exactTextInjected) {
    return '';
  }

  const reference = approvedContext.contextPackReference;
  const exactLines = approvedContext.exactTextInjected.split('\n');
  const provenanceLines = formatProvenanceSummary(approvedContext.provenanceSummary);
  const traceHashLines = formatList(approvedContext.sourceTraceSliceHashes, '- none');

  return [
    '[RCK APPROVED CONTEXT]',
    `Kind: ${approvedContext.kind}`,
    `Source: ${approvedContext.source} ${approvedContext.injectionId}`,
    `Approval: ${approvedContext.approvalMode} by ${approvedContext.approvedBy}`,
    `ContextPackReference: ${reference.contextPackId} | ${reference.contextPackHash} | ${reference.title} | ${reference.kind}`,
    `SourceTraceSliceHashes: ${approvedContext.sourceTraceSliceHashes.length}`,
    ...traceHashLines,
    'Warnings:',
    ...formatList(approvedContext.warnings),
    'Constraints:',
    ...formatList(approvedContext.constraints),
    'ProvenanceSummary:',
    ...provenanceLines.map((line) => `- ${line}`),
    'ExactTextInjected:',
    '<<<RCK-APPROVED-CONTEXT-START>>>',
    ...exactLines,
    '<<<RCK-APPROVED-CONTEXT-END>>>',
    '[/RCK APPROVED CONTEXT]',
  ].join('\n');
}

export function buildApprovedContextMessage(candidate) {
  const approvedContext = normalizeApprovedContextForCompletion(candidate);
  if (!approvedContext) {
    return null;
  }

  return Object.freeze({
    role: 'system',
    content: buildApprovedContextBlock(approvedContext),
    metadata: Object.freeze({
      kind: APPROVED_CONTEXT_KIND,
      source: APPROVED_CONTEXT_SOURCE,
      injectionId: approvedContext.injectionId,
    }),
  });
}

export function hasApprovedContextForCompletion(candidate) {
  return Boolean(normalizeApprovedContextForCompletion(candidate));
}

export { APPROVED_CONTEXT_APPROVAL_MODE, APPROVED_CONTEXT_APPROVER, APPROVED_CONTEXT_KIND, APPROVED_CONTEXT_SOURCE };
