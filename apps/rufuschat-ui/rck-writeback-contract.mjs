const RCK_WRITEBACK_CHAT_TURN_STATE_KIND = 'rufuschat.chat_turn_state';
const RCK_WRITEBACK_CHAT_TURN_STATE_SCHEMA_VERSION = 'rufuschat.chat_turn_state.v0';
const RCK_WRITEBACK_CHAT_TURN_DELTA_KIND = 'rufuschat.chat_turn_delta';
const RCK_WRITEBACK_CHAT_TURN_DELTA_SCHEMA_VERSION = 'rufuschat.chat_turn_delta.v0';
const RCK_WRITEBACK_REGISTRATION_DRAFT_KIND = 'rufuschat.chat_turn_registration_draft';
const RCK_WRITEBACK_REGISTRATION_DRAFT_SCHEMA_VERSION = 'rufuschat.chat_turn_registration_draft.v0';
const RCK_WRITEBACK_REGISTRATION_RESULT_KIND = 'rufuschat.chat_turn_registration_result';
const RCK_WRITEBACK_REGISTRATION_RESULT_SCHEMA_VERSION = 'rufuschat.chat_turn_registration_result.v0';

function normalizeString(value, fallback = '') {
  if (typeof value !== 'string') {
    return fallback;
  }

  return value.trim() || fallback;
}

function normalizeNullableString(value) {
  const normalized = normalizeString(value);
  return normalized ? normalized : null;
}

function normalizeRecord(candidate) {
  if (!candidate || typeof candidate !== 'object' || Array.isArray(candidate)) {
    return {};
  }

  return { ...candidate };
}

function normalizeStringArray(candidate) {
  if (!Array.isArray(candidate)) {
    return [];
  }

  return candidate.map((entry) => normalizeString(entry)).filter(Boolean);
}

function normalizeNestedObject(candidate) {
  if (!candidate || typeof candidate !== 'object' || Array.isArray(candidate)) {
    return {};
  }

  return { ...candidate };
}

function normalizeNullableNestedObject(candidate) {
  if (!candidate || typeof candidate !== 'object' || Array.isArray(candidate)) {
    return null;
  }

  return { ...candidate };
}

export function normalizeRckWritebackArtifactRef(candidate) {
  const input = normalizeNestedObject(candidate);
  return {
    artifactId: normalizeNullableString(input.artifactId),
    kind: normalizeString(input.kind, 'artifact_reference'),
    label: normalizeNullableString(input.label),
    path: normalizeNullableString(input.path),
    url: normalizeNullableString(input.url),
    description: normalizeNullableString(input.description),
    source: normalizeNullableString(input.source),
    metadata: normalizeRecord(input.metadata),
  };
}

export function createRckWritebackArtifactRef(input = {}) {
  return normalizeRckWritebackArtifactRef(input);
}

export function normalizeRckWritebackEvidenceRef(candidate) {
  const input = normalizeNestedObject(candidate);
  return {
    evidenceId: normalizeNullableString(input.evidenceId),
    kind: normalizeString(input.kind, 'evidence_reference'),
    label: normalizeNullableString(input.label),
    source: normalizeNullableString(input.source),
    reference: normalizeNullableString(input.reference),
    status: normalizeString(input.status, 'unknown'),
    metadata: normalizeRecord(input.metadata),
  };
}

export function createRckWritebackEvidenceRef(input = {}) {
  return normalizeRckWritebackEvidenceRef(input);
}

export function normalizeRckWritebackToolExecution(candidate) {
  const input = normalizeNestedObject(candidate);
  return {
    toolExecutionId: normalizeNullableString(input.toolExecutionId),
    toolName: normalizeString(input.toolName, 'unknown-tool'),
    provider: normalizeNullableString(input.provider),
    status: normalizeString(input.status, 'unknown'),
    startedAtUtc: normalizeNullableString(input.startedAtUtc),
    finishedAtUtc: normalizeNullableString(input.finishedAtUtc),
    requestMetadata: normalizeRecord(input.requestMetadata),
    responseMetadata: normalizeRecord(input.responseMetadata),
    notes: normalizeStringArray(input.notes),
    metadata: normalizeRecord(input.metadata),
  };
}

export function createRckWritebackToolExecution(input = {}) {
  return normalizeRckWritebackToolExecution(input);
}

export function normalizeRckWritebackDecision(candidate) {
  const input = normalizeNestedObject(candidate);
  return {
    decisionId: normalizeNullableString(input.decisionId),
    kind: normalizeString(input.kind, 'decision'),
    label: normalizeNullableString(input.label),
    value: normalizeNullableString(input.value),
    reason: normalizeNullableString(input.reason),
    decidedBy: normalizeNullableString(input.decidedBy),
    decidedAtUtc: normalizeNullableString(input.decidedAtUtc),
    metadata: normalizeRecord(input.metadata),
  };
}

export function createRckWritebackDecision(input = {}) {
  return normalizeRckWritebackDecision(input);
}

export function normalizeRckWritebackOpenQuestion(candidate) {
  const input = normalizeNestedObject(candidate);
  return {
    questionId: normalizeNullableString(input.questionId),
    kind: normalizeString(input.kind, 'open_question'),
    text: normalizeNullableString(input.text),
    status: normalizeString(input.status, 'open'),
    owner: normalizeNullableString(input.owner),
    metadata: normalizeRecord(input.metadata),
  };
}

export function createRckWritebackOpenQuestion(input = {}) {
  return normalizeRckWritebackOpenQuestion(input);
}

export function normalizeRckWritebackContextUsed(candidate) {
  const input = normalizeNestedObject(candidate);
  const approvedRckContext = normalizeNestedObject(input.approvedRckContext);

  return {
    approvedRckContext: {
      used: Boolean(approvedRckContext.used),
      injectionId: normalizeNullableString(approvedRckContext.injectionId),
      sourceTraceSliceHashes: normalizeStringArray(approvedRckContext.sourceTraceSliceHashes),
      contextPackReference: normalizeNullableNestedObject(approvedRckContext.contextPackReference),
    },
  };
}

export function createRckWritebackContextUsed(input = {}) {
  return normalizeRckWritebackContextUsed(input);
}

export function normalizeRckWritebackChatTurnMessage(candidate) {
  const input = normalizeNestedObject(candidate);
  return {
    messageId: normalizeNullableString(input.messageId),
    text: normalizeNullableString(input.text),
  };
}

export function createRckWritebackChatTurnMessage(input = {}) {
  return normalizeRckWritebackChatTurnMessage(input);
}

export function normalizeRckWritebackChatTurnStatePayload(candidate) {
  const input = normalizeNestedObject(candidate);
  const chat = normalizeNestedObject(input.chat);
  const messages = normalizeNestedObject(input.messages);

  return {
    kind: RCK_WRITEBACK_CHAT_TURN_STATE_KIND,
    schemaVersion: RCK_WRITEBACK_CHAT_TURN_STATE_SCHEMA_VERSION,
    chat: {
      chatId: normalizeNullableString(chat.chatId),
      turnId: normalizeNullableString(chat.turnId),
      parentTurnId: normalizeNullableString(chat.parentTurnId),
    },
    messages: {
      user: normalizeRckWritebackChatTurnMessage(messages.user),
      assistant: normalizeRckWritebackChatTurnMessage(messages.assistant),
    },
    contextUsed: {
      approvedRckContext: normalizeRckWritebackContextUsed(input.contextUsed).approvedRckContext,
    },
    toolExecutions: Array.isArray(input.toolExecutions)
      ? input.toolExecutions.map((entry) => normalizeRckWritebackToolExecution(entry))
      : [],
    artifacts: Array.isArray(input.artifacts)
      ? input.artifacts.map((entry) => normalizeRckWritebackArtifactRef(entry))
      : [],
    evidenceRefs: Array.isArray(input.evidenceRefs)
      ? input.evidenceRefs.map((entry) => normalizeRckWritebackEvidenceRef(entry))
      : [],
    decisions: Array.isArray(input.decisions)
      ? input.decisions.map((entry) => normalizeRckWritebackDecision(entry))
      : [],
    openQuestions: Array.isArray(input.openQuestions)
      ? input.openQuestions.map((entry) => normalizeRckWritebackOpenQuestion(entry))
      : [],
    verification: {
      level: normalizeString(input.verification?.level, 'unverified'),
      status: normalizeString(input.verification?.status, 'draft'),
    },
  };
}

export function createRckWritebackChatTurnStatePayload(input = {}) {
  return normalizeRckWritebackChatTurnStatePayload(input);
}

export function normalizeRckWritebackChatTurnDeltaPayload(candidate) {
  const input = normalizeNestedObject(candidate);
  const operations = Array.isArray(input.operations)
    ? input.operations.map((operation) => {
        const normalizedOperation = normalizeNestedObject(operation);
        return {
          op: normalizeString(normalizedOperation.op, 'append_chat_turn'),
          turnId: normalizeNullableString(normalizedOperation.turnId),
        };
      })
    : [];

  return {
    kind: RCK_WRITEBACK_CHAT_TURN_DELTA_KIND,
    schemaVersion: RCK_WRITEBACK_CHAT_TURN_DELTA_SCHEMA_VERSION,
    reason: normalizeString(input.reason, 'assistant_response_added'),
    chatId: normalizeNullableString(input.chatId),
    fromTurnId: normalizeNullableString(input.fromTurnId),
    toTurnId: normalizeNullableString(input.toTurnId),
    operations,
    usedContextInjectionId: normalizeNullableString(input.usedContextInjectionId),
  };
}

export function createRckWritebackChatTurnDeltaPayload(input = {}) {
  return normalizeRckWritebackChatTurnDeltaPayload(input);
}

export function normalizeRckWritebackRegistrationDraft(candidate) {
  const input = normalizeNestedObject(candidate);
  return {
    kind: RCK_WRITEBACK_REGISTRATION_DRAFT_KIND,
    schemaVersion: RCK_WRITEBACK_REGISTRATION_DRAFT_SCHEMA_VERSION,
    status: normalizeString(input.status, 'draft'),
    statePayload: normalizeRckWritebackChatTurnStatePayload(input.statePayload),
    deltaPayload: normalizeRckWritebackChatTurnDeltaPayload(input.deltaPayload),
    notes: normalizeStringArray(input.notes),
    warnings: normalizeStringArray(input.warnings),
  };
}

export function createRckWritebackRegistrationDraft(input = {}) {
  return normalizeRckWritebackRegistrationDraft(input);
}

export function normalizeRckWritebackRegistrationResult(candidate) {
  const input = normalizeNestedObject(candidate);
  return {
    kind: RCK_WRITEBACK_REGISTRATION_RESULT_KIND,
    schemaVersion: RCK_WRITEBACK_REGISTRATION_RESULT_SCHEMA_VERSION,
    ok: Boolean(input.ok),
    status: normalizeString(input.status, 'placeholder'),
    message: normalizeString(input.message, 'RCK write-back is not connected in this phase.'),
    statePayload: normalizeRckWritebackChatTurnStatePayload(input.statePayload),
    deltaPayload: normalizeRckWritebackChatTurnDeltaPayload(input.deltaPayload),
    stateId: normalizeNullableString(input.stateId),
    deltaId: normalizeNullableString(input.deltaId),
  };
}

export function createRckWritebackRegistrationResult(input = {}) {
  return normalizeRckWritebackRegistrationResult(input);
}

export {
  RCK_WRITEBACK_CHAT_TURN_DELTA_KIND,
  RCK_WRITEBACK_CHAT_TURN_DELTA_SCHEMA_VERSION,
  RCK_WRITEBACK_CHAT_TURN_STATE_KIND,
  RCK_WRITEBACK_CHAT_TURN_STATE_SCHEMA_VERSION,
  RCK_WRITEBACK_REGISTRATION_DRAFT_KIND,
  RCK_WRITEBACK_REGISTRATION_DRAFT_SCHEMA_VERSION,
  RCK_WRITEBACK_REGISTRATION_RESULT_KIND,
  RCK_WRITEBACK_REGISTRATION_RESULT_SCHEMA_VERSION,
};
