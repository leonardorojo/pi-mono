import {
  createContextPackPreview,
  normalizeContextPackPreview,
} from './rck-contextpack-contract.mjs';

const MOCK_CONTEXT_PACK_REFERENCE = {
  contextPackId: 'cp_preview_placeholder_v1',
  contextPackHash: 'sha256:placeholder-context-pack-v1',
  title: 'RufusChat ContextPack preview placeholder',
  kind: 'placeholder',
};

export function buildMockContextPackPreview(overrides = {}) {
  return normalizeContextPackPreview(
    createContextPackPreview({
      reference: MOCK_CONTEXT_PACK_REFERENCE,
      title: 'RufusChat ContextPack preview placeholder',
      sourceTraceSliceHashes: ['trace-slice-placeholder-a', 'trace-slice-placeholder-b'],
      estimatedTokenCost: null,
      warnings: [
        'Placeholder / dev-only preview.',
        'No RCK Core integration is active yet.',
        'Confirm injection is disabled in this phase.',
      ],
      constraints: [
        'Do not read .rck directly.',
        'Do not execute RCK Core.',
        'Do not generate a real ContextPack yet.',
        'Do not persist injection records yet.',
      ],
      provenanceSummary: {
        sourceDocument: 'docs/RUFUSCHAT_ADAPTER_DESIGN.md',
        contractDocument: 'docs/CONTEXT_PACK_BOUNDARY.md',
        schemaDocument: 'schemas/rck.context_pack.v0.schema.json',
        publishedCommit: '048d4c3',
        mode: 'placeholder',
      },
      exactTextToInject: 'Not available in this phase.',
      userApprovalStatus: 'not-available',
      injectionPolicy: {
        canPreview: true,
        canConfirm: false,
        canPersistRecord: false,
        canReadRckFilesystem: false,
        canCallRckCore: false,
        reason: 'Phase 19 is placeholder-only. Confirm injection is not available yet.',
      },
      injectionRecordDraft: {
        status: 'draft',
        notes: ['Mock injection record draft only. No persistence in this phase.'],
      },
      ...overrides,
    }),
  );
}

export function getContextPackPreviewPlaceholder() {
  return buildMockContextPackPreview();
}
