import {
  CONTEXT_PACK_SCHEMA_VERSION,
  createApprovedContextScope,
  createContextPackGenerationRequest,
  createContextPackGenerationResponse,
  createContextPackReference,
  normalizeApprovedContextScope,
  normalizeContextPackGenerationRequest,
  normalizeContextPackGenerationResponse,
} from './rck-contextpack-generation-contract.mjs';
import { buildMockContextPackPreview } from './contextpack-preview-provider.mjs';

const DEFAULT_GENERATION_REQUEST_WARNINGS = [
  'Placeholder / dev-only generation request.',
  'Not connected to real RCK generation in this phase.',
  'No TraceSlice was generated.',
  'No ContextPack was generated.',
  'Confirm injection stays disabled.',
];

const DEFAULT_GENERATION_REQUEST_CONSTRAINTS = [
  'No RCK Core execution.',
  'No .rck reads.',
  'No TraceSlice generation.',
  'No ContextPack generation.',
  'No persistence of the request.',
  'No persistence of injection records.',
  'No chat injection.',
  'No LLM calls.',
];

function makePlaceholderRequestId() {
  const random = globalThis.crypto?.randomUUID?.();
  return `rck-context-pack-generation-request-${random ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`}`;
}

function makePlaceholderContextPackReference(requestId) {
  return createContextPackReference({
    contextPackId: `cp_${requestId}`,
    contextPackHash: `sha256:placeholder-${requestId}`,
    title: 'RufusChat ContextPack generation placeholder',
    kind: 'placeholder',
  });
}

export function buildContextPackGenerationRequestFromApprovedScope(approvedScope, userIntentText = '', overrides = {}) {
  const normalizedScope = normalizeApprovedContextScope(approvedScope);
  return createContextPackGenerationRequest({
    requestId: overrides.requestId ?? makePlaceholderRequestId(),
    createdAtUtc: overrides.createdAtUtc ?? new Date().toISOString(),
    source: 'approved-scope',
    userIntentText,
    approvedScope: createApprovedContextScope(normalizedScope),
    requestedOutput: {
      contextPackSchemaVersion: overrides.requestedOutput?.contextPackSchemaVersion ?? CONTEXT_PACK_SCHEMA_VERSION,
      previewOnly: true,
      ...overrides.requestedOutput,
    },
    safety: {
      requireUserApprovalForInjection: true,
      allowAutomaticInjection: false,
      ...overrides.safety,
    },
  });
}

export function buildPlaceholderContextPackGenerationResponse(input = {}) {
  const request = normalizeContextPackGenerationRequest(
    input.request ?? buildContextPackGenerationRequestFromApprovedScope(input.approvedScope, input.userIntentText, input),
  );
  const reference = makePlaceholderContextPackReference(request.requestId);
  const preview = buildMockContextPackPreview({
    title: reference.title,
    reference,
    provenanceSummary: {
      sourceDocument: 'apps/rufuschat-ui/RCK_CONTEXTPACK_GENERATION_CONTRACT.md',
      contractDocument: 'apps/rufuschat-ui/rck-contextpack-generation-contract.mjs',
      schemaDocument: 'schemas/rck.context_pack.v0.schema.json',
      publishedCommit: '6a446731',
      mode: 'placeholder',
      notes: [
        'Approved scope was turned into a request contract only.',
        'No TraceSlice or ContextPack was generated.',
        'The preview is still a placeholder and is not derived from real RCK data.',
      ],
    },
    exactTextToInject: 'Not available in this phase. Preview is derived from the approved scope request contract.',
    userApprovalStatus: 'approved-placeholder',
    warnings: [
      ...DEFAULT_GENERATION_REQUEST_WARNINGS,
      `Request ID: ${request.requestId}`,
      `Approved target: ${request.approvedScope.targetLabel}`,
    ],
    constraints: DEFAULT_GENERATION_REQUEST_CONSTRAINTS,
  });

  return normalizeContextPackGenerationResponse({
    requestId: request.requestId,
    status: input.status ?? 'not_connected',
    contextPackReference: reference,
    contextPackPreview: preview,
    warnings: [
      ...DEFAULT_GENERATION_REQUEST_WARNINGS,
      `Approved scope converted into request ${request.requestId}.`,
      'Not connected to real RCK generation in this phase.',
    ],
    constraints: DEFAULT_GENERATION_REQUEST_CONSTRAINTS,
    provenanceSummary: {
      sourceDocument: 'apps/rufuschat-ui/RCK_CONTEXTPACK_GENERATION_CONTRACT.md',
      requestContractDocument: 'apps/rufuschat-ui/rck-contextpack-generation-contract.mjs',
      previewContractDocument: 'apps/rufuschat-ui/rck-contextpack-contract.mjs',
      previewProviderDocument: 'apps/rufuschat-ui/contextpack-preview-provider.mjs',
      mode: 'placeholder',
      notes: [
        'Approved scope is represented as a request contract first.',
        'The response remains placeholder / not connected.',
        'No real RCK generation or injection is performed.',
      ],
    },
    message: 'Not connected to real RCK generation in this phase.',
  });
}

export function buildContextPackGenerationRequestPreview(input = {}) {
  return buildPlaceholderContextPackGenerationResponse(input);
}
