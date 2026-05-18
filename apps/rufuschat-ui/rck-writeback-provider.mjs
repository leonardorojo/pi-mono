import {
  createRckWritebackArtifactRef,
  createRckWritebackChatTurnDeltaPayload,
  createRckWritebackChatTurnStatePayload,
  createRckWritebackDecision,
  createRckWritebackEvidenceRef,
  createRckWritebackOpenQuestion,
  createRckWritebackRegistrationDraft,
  createRckWritebackRegistrationResult,
  createRckWritebackStateEvidence,
  createRckWritebackToolExecution,
} from './rck-writeback-contract.mjs';

function toPlainObject(candidate) {
  if (!candidate || typeof candidate !== 'object' || Array.isArray(candidate)) {
    return {};
  }

  return { ...candidate };
}

function toPlainArray(candidate) {
  if (!Array.isArray(candidate)) {
    return [];
  }

  return candidate.slice();
}

function resolveChatTurn(input = {}) {
  const candidate = toPlainObject(input.chatTurn ?? input);
  const chat = toPlainObject(candidate.chat);
  const messages = toPlainObject(candidate.messages);
  const contextUsed = toPlainObject(candidate.contextUsed);
  const evidence = toPlainObject(candidate.evidence);
  const completionMetadata = toPlainObject(candidate.completionMetadata);

  return {
    chatId: chat.chatId ?? candidate.chatId ?? null,
    turnId: chat.turnId ?? candidate.turnId ?? null,
    parentTurnId: chat.parentTurnId ?? candidate.parentTurnId ?? null,
    userMessage: messages.user ?? candidate.userMessage ?? {},
    assistantMessage: messages.assistant ?? candidate.assistantMessage ?? {},
    contextUsed,
    evidence,
    completionMetadata,
    toolExecutions: toPlainArray(candidate.toolExecutions),
    artifacts: toPlainArray(candidate.artifacts),
    evidenceRefs: toPlainArray(candidate.evidenceRefs),
    decisions: toPlainArray(candidate.decisions),
    openQuestions: toPlainArray(candidate.openQuestions),
    verification: toPlainObject(candidate.verification),
    delta: toPlainObject(candidate.delta),
    provenance: toPlainObject(candidate.provenance),
  };
}

export function buildChatTurnStatePayload(input = {}) {
  const chatTurn = resolveChatTurn(input);
  return createRckWritebackChatTurnStatePayload({
    chat: {
      chatId: chatTurn.chatId,
      turnId: chatTurn.turnId,
      parentTurnId: chatTurn.parentTurnId,
    },
    messages: {
      user: chatTurn.userMessage,
      assistant: chatTurn.assistantMessage,
    },
    contextUsed: {
      approvedRckContext: chatTurn.contextUsed?.approvedRckContext,
    },
    evidence: createRckWritebackStateEvidence({
      provider: chatTurn.evidence?.provider ?? chatTurn.completionMetadata?.provider ?? null,
      model: chatTurn.evidence?.model ?? chatTurn.completionMetadata?.model ?? null,
      requestMetadata: chatTurn.evidence?.requestMetadata ?? chatTurn.completionMetadata?.requestMetadata ?? {},
      responseMetadata: chatTurn.evidence?.responseMetadata ?? chatTurn.completionMetadata ?? {},
    }),
    toolExecutions: chatTurn.toolExecutions.map((entry) => createRckWritebackToolExecution(entry)),
    artifacts: chatTurn.artifacts.map((entry) => createRckWritebackArtifactRef(entry)),
    evidenceRefs: chatTurn.evidenceRefs.map((entry) => createRckWritebackEvidenceRef(entry)),
    decisions: chatTurn.decisions.map((entry) => createRckWritebackDecision(entry)),
    openQuestions: chatTurn.openQuestions.map((entry) => createRckWritebackOpenQuestion(entry)),
    verification: chatTurn.verification,
  });
}

export function buildChatTurnDeltaPayload(input = {}) {
  const chatTurn = resolveChatTurn(input);
  const deltaInput = chatTurn.delta;

  return createRckWritebackChatTurnDeltaPayload({
    reason: deltaInput.reason ?? 'assistant_response_added',
    chatId: chatTurn.chatId,
    fromTurnId: deltaInput.fromTurnId ?? chatTurn.parentTurnId,
    toTurnId: deltaInput.toTurnId ?? chatTurn.turnId,
    operations: Array.isArray(deltaInput.operations) && deltaInput.operations.length > 0
      ? deltaInput.operations
      : [{ op: 'append_chat_turn', turnId: chatTurn.turnId }],
    usedContextInjectionId:
      deltaInput.usedContextInjectionId ??
      (chatTurn.contextUsed?.approvedRckContext?.used
        ? chatTurn.contextUsed.approvedRckContext.injectionId
        : null),
  });
}

export function buildChatTurnRegistrationDraft(input = {}) {
  const statePayload = buildChatTurnStatePayload(input);
  const deltaPayload = buildChatTurnDeltaPayload(input);

  return createRckWritebackRegistrationDraft({
    status: 'draft',
    statePayload,
    deltaPayload,
    notes: Array.isArray(input.notes) ? input.notes : [],
    warnings: Array.isArray(input.warnings) ? input.warnings : [],
  });
}

export function registerChatTurnPlaceholder(input = {}) {
  const statePayload = buildChatTurnStatePayload(input);
  const deltaPayload = buildChatTurnDeltaPayload(input);

  return createRckWritebackRegistrationResult({
    ok: true,
    status: 'placeholder',
    message: 'RCK write-back is not connected in this phase.',
    statePayload,
    deltaPayload,
    stateId: null,
    deltaId: null,
  });
}
