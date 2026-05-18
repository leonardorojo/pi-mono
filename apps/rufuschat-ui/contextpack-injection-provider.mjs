import {
  canConfirmContextPackInjection,
  createContextPackInjectionRecord,
  createContextPackInjectionRequest,
  createContextPackInjectionResult,
  normalizeContextPackInjectionRecord,
  normalizeContextPackInjectionRequest,
  normalizeLoadedPreview,
} from './rck-contextpack-injection-contract.mjs';

function makeInjectionError(message, code = 'invalid_context_pack_injection', issues = []) {
  const error = new Error(message);
  error.code = code;
  error.issues = Array.isArray(issues) ? issues : [];
  return error;
}

function requireLoadedPreview(loadedPreview) {
  const preview = normalizeLoadedPreview(loadedPreview);
  if (!preview || preview.placeholder || preview.source !== 'loaded-contextpack-json') {
    throw makeInjectionError(
      'Loaded ContextPack preview is required before confirming injection.',
      'missing_loaded_preview',
      ['Load a real ContextPack JSON preview first.'],
    );
  }

  return preview;
}

function requireExactTextToInject(preview, exactTextToInject) {
  const text = typeof exactTextToInject === 'string' ? exactTextToInject.trim() : '';
  if (!text) {
    throw makeInjectionError(
      'Exact text to inject is required.',
      'missing_exact_text',
      ['Loaded ContextPack JSON must include a non-empty exactTextToInject string.'],
    );
  }

  if (typeof preview.exactTextToInject === 'string' && preview.exactTextToInject.trim() && preview.exactTextToInject.trim() !== text) {
    throw makeInjectionError(
      'The confirm request text does not match the loaded preview text.',
      'mismatched_exact_text',
      ['The UI must send the same exactTextToInject that is visible in the loaded preview.'],
    );
  }

  return text;
}

export function buildContextPackInjectionRequest(input = {}) {
  const loadedPreview = requireLoadedPreview(input.loadedPreview ?? input.preview ?? input.contextPackPreview);
  const exactTextToInject = requireExactTextToInject(loadedPreview, input.exactTextToInject ?? loadedPreview.exactTextToInject);
  const request = createContextPackInjectionRequest({
    requestId: input.requestId,
    createdAtUtc: input.createdAtUtc,
    chatId: input.chatId,
    projectId: input.projectId,
    loadedPreview,
    exactTextToInject,
  });

  if (!canConfirmContextPackInjection(request)) {
    throw makeInjectionError(
      'ContextPack injection request is not eligible for confirmation.',
      'ineligible_injection_request',
      ['Loaded preview must contain non-empty exactTextToInject and source trace slice hashes.'],
    );
  }

  return request;
}

export function buildContextPackInjectionRecord(input = {}) {
  const request = normalizeContextPackInjectionRequest(
    input.request ?? buildContextPackInjectionRequest(input),
  );
  return createContextPackInjectionRecord({
    injectionId: input.injectionId,
    createdAtUtc: input.createdAtUtc,
    request,
  });
}

export function confirmContextPackInjection(input = {}) {
  const request = buildContextPackInjectionRequest(input);
  const injectionRecord = buildContextPackInjectionRecord({
    ...input,
    request,
  });

  return createContextPackInjectionResult({
    request,
    injectionRecord,
    message: 'Context injected into this chat session.',
    deliveryMode: 'visual-only',
    shouldSendToLlm: false,
  });
}

export function normalizeContextPackInjectionConfirmation(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return confirmContextPackInjection();
  }

  return createContextPackInjectionResult({
    request: normalizeContextPackInjectionRequest(candidate.request ?? candidate),
    injectionRecord: normalizeContextPackInjectionRecord(candidate.injectionRecord ?? candidate),
    message: candidate.message,
    deliveryMode: candidate.deliveryMode,
    shouldSendToLlm: candidate.shouldSendToLlm,
  });
}
