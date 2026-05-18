const messagesEl = document.getElementById('messages');
const composerForm = document.getElementById('composer-form');
const composerInput = document.getElementById('composer-input');
const sendButton = composerForm.querySelector('button[type="submit"]');
const composerFooter = composerForm.querySelector('.composer__footer');
const messagesInnerEl = document.getElementById('messages-inner');
const currentProjectEl = document.getElementById('current-project');
const currentChatEl = document.getElementById('current-chat');
const memoryStatusEl = document.getElementById('current-memory-status');
let chatCancelButton = null;
const summaryStatusEl = document.getElementById('current-summary-status');
const rckTraceStatusEl = document.getElementById('current-rck-trace-status');
const traceChipEl = document.getElementById('current-trace-chip');
const chatSessionShellTraceEl = document.querySelector('.chat-session-shell__trace');
const statusPill = document.querySelector('.chat-header__status');
const confirmModal = document.getElementById('confirm-modal');
const confirmDescription = document.getElementById('confirm-modal-description');
const confirmCancelButton = document.getElementById('confirm-modal-cancel');
const confirmConfirmButton = document.getElementById('confirm-modal-confirm');
const createProjectModal = document.getElementById('create-project-modal');
const createProjectTitleInput = document.getElementById('create-project-name');
const createProjectRepositoryPathInput = document.getElementById('create-project-repository-path');
const createProjectError = document.getElementById('create-project-error');
const createProjectCancelButton = document.getElementById('create-project-cancel');
const createProjectForm = document.getElementById('create-project-form');
const projectTreeEl = document.getElementById('project-tree');
const slashMenuEl = document.getElementById('slash-menu');
const newProjectButton = document.getElementById('new-project-button');
const projectContextMenuEl = document.getElementById('project-context-menu');
const chatContextMenuEl = document.getElementById('chat-context-menu');
const productStateExportButton = document.getElementById('product-state-export-button');
const productStateImportButton = document.getElementById('product-state-import-button');
const productStateResetButton = document.getElementById('product-state-reset-button');
const productStateImportInput = document.getElementById('product-state-import-input');
const attachContextPackButton = document.getElementById('attach-context-pack-button');
const contextSidePanelEl = document.getElementById('context-side-panel');
const contextSidePanelBadgeEl = document.getElementById('context-side-panel-badge');
const contextSidePanelCloseButton = document.getElementById('context-side-panel-close');
const contextScopeSuggestionPanel = document.getElementById('context-scope-suggestion');
const contextScopeSuggestionTitleEl = document.getElementById('context-scope-suggestion-title');
const contextScopeSuggestionStatusEl = document.getElementById('context-scope-suggestion-status');
const contextScopeSuggestionSummaryEl = document.getElementById('context-scope-suggestion-summary');
const contextScopeSuggestionIntentEl = document.getElementById('context-scope-suggestion-intent');
const contextScopeSuggestionTargetEl = document.getElementById('context-scope-suggestion-target');
const contextScopeSuggestionDepthEl = document.getElementById('context-scope-suggestion-depth');
const contextScopeSuggestionArtifactsSelectedEl = document.getElementById('context-scope-suggestion-artifacts-selected');
const contextScopeSuggestionArtifactsCandidatesEl = document.getElementById('context-scope-suggestion-artifacts-candidates');
const contextScopeSuggestionArtifactsExcludedEl = document.getElementById('context-scope-suggestion-artifacts-excluded');
const contextScopeSuggestionRationaleEl = document.getElementById('context-scope-suggestion-rationale');
const contextScopeSuggestionConfidenceEl = document.getElementById('context-scope-suggestion-confidence');
const contextScopeSuggestionWarningsEl = document.getElementById('context-scope-suggestion-warnings');
const contextScopeSuggestionDecisionEl = document.getElementById('context-scope-suggestion-decision');
const contextScopeSuggestionPreviewEl = document.getElementById('context-scope-suggestion-preview');
const contextScopeSuggestionApproveButton = document.getElementById('context-scope-suggestion-approve');
const contextScopeSuggestionRejectButton = document.getElementById('context-scope-suggestion-reject');
const contextScopeSuggestionAdjustButton = document.getElementById('context-scope-suggestion-adjust');
const contextPackPreviewPanel = document.getElementById('context-pack-preview');
const contextPackPreviewTitleEl = document.getElementById('context-pack-preview-title');
const contextPackPreviewStatusEl = document.getElementById('context-pack-preview-status');
const contextPackPreviewSummaryEl = document.getElementById('context-pack-preview-summary');
const contextPackPreviewSchemaVersionEl = document.getElementById('context-pack-preview-schema-version');
const contextPackPreviewContextTitleEl = document.getElementById('context-pack-preview-context-title');
const contextPackPreviewReferenceEl = document.getElementById('context-pack-preview-reference');
const contextPackPreviewTraceHashesEl = document.getElementById('context-pack-preview-trace-hashes');
const contextPackPreviewSectionsEl = document.getElementById('context-pack-preview-sections');
const contextPackPreviewTokenCostEl = document.getElementById('context-pack-preview-token-cost');
const contextPackPreviewWarningsEl = document.getElementById('context-pack-preview-warnings');
const contextPackPreviewConstraintsEl = document.getElementById('context-pack-preview-constraints');
const contextPackPreviewProvenanceEl = document.getElementById('context-pack-preview-provenance');
const contextPackPreviewScopeDerivationEl = document.getElementById('context-pack-preview-scope-derivation');
const contextPackPreviewExactTextEl = document.getElementById('context-pack-preview-exact-text');
const contextPackPreviewApprovalStatusEl = document.getElementById('context-pack-preview-approval-status');
const contextPackPreviewJsonEl = document.getElementById('context-pack-preview-json');
const contextPackPreviewLoadButton = document.getElementById('context-pack-preview-load-button');
const contextPackPreviewLoadMessageEl = document.getElementById('context-pack-preview-load-message');
const contextPackPreviewConfirmButton = document.getElementById('context-pack-preview-confirm');
const contextPackPreviewCloseButton = document.getElementById('context-pack-preview-close');
const contextPackGenerationRequestPanel = document.getElementById('context-pack-generation-request');
const contextPackGenerationRequestTitleEl = document.getElementById('context-pack-generation-request-title');
const contextPackGenerationRequestStatusEl = document.getElementById('context-pack-generation-request-status');
const contextPackGenerationRequestSummaryEl = document.getElementById('context-pack-generation-request-summary');
const contextPackGenerationRequestIdEl = document.getElementById('context-pack-generation-request-id');
const contextPackGenerationRequestTargetEl = document.getElementById('context-pack-generation-request-target');
const contextPackGenerationRequestDepthEl = document.getElementById('context-pack-generation-request-depth');
const contextPackGenerationRequestArtifactsEl = document.getElementById('context-pack-generation-request-artifacts');
const contextPackGenerationRequestWarningsEl = document.getElementById('context-pack-generation-request-warnings');
const contextPackGenerationRequestConstraintsEl = document.getElementById('context-pack-generation-request-constraints');
const contextPackGenerationRequestProvenanceEl = document.getElementById('context-pack-generation-request-provenance');
const contextPackGenerationRequestMessageEl = document.getElementById('context-pack-generation-request-message');
const contextPackGenerationRequestPreviewEl = document.getElementById('context-pack-generation-request-preview');

const productStateEndpoint = '/api/product-state';
const contextScopeSuggestionEndpoint = '/api/rck/context-scope/suggest-placeholder';
const contextPackGenerationPlaceholderEndpoint = '/api/rck/context-pack/generate-placeholder';
const contextPackLoadPreviewEndpoint = '/api/rck/context-pack/load-preview';
const contextPackPreviewEndpoint = '/api/rck/context-pack/preview-placeholder';
const idleStatusText = 'Browser-local session';
const loadingStatusText = 'Loading local data...';
const savingStatusText = 'Saving local data...';
const savedStatusText = 'Saved';
const productStateExportErrorText = 'Local data could not be exported.';
const productStateImportErrorText = 'Local data could not be imported.';
const productStateResetErrorText = 'Local data could not be reset.';
const hydrateFailureText = 'Local data could not be loaded. Using an in-memory session.';
const saveFailureText = 'Local data could not be saved.';
const memoryPlaceholder = {
  memoryStatus: 'not-connected',
  semanticSummaryStatus: 'not-generated',
  linkedRckTraceStatus: 'not-linked',
  semanticSummaryPreview: null,
  linkedRckTraceId: null,
};
const tracePlaceholder = {
  status: 'not-linked',
  traceId: null,
  provider: 'pi-rck-bridge',
  futureProvider: 'rck-core-kernel',
  mode: 'placeholder',
};
let activeContextScopeSuggestion = null;
let activeContextPackPreview = null;
let activeLoadedContextPackPreview = null;
let activeContextPackGenerationRequest = null;
let activeContextPackGenerationResponse = null;
let isContextSidePanelOpen = false;
const contextPackCandidateSummaryLines = [
  'Current chat context placeholder',
  'ProductState metadata placeholder',
  'No internal evidence included',
];
const contextPackCandidateSafetyLines = ['Placeholder only', 'No raw evidence included', 'No .pi/rck read'];
const contextPackInjectionHistoryTitle = 'Context candidate';
const contextPackInjectionHistorySourceKind = 'placeholder';
const contextPackInjectedMessage = 'Context pack injected';
const contextPackCancelledMessage = 'Context candidate cancelled';
const localIntroMessage =
  'This chat is local. Trace tracking happens only when you confirm slash actions.';
const newChatAssistantMessage =
  'New chat ready. Start a conversation or type / for commands.';

function isSlashCommandText(text) {
  return typeof text === 'string' && text.trim().startsWith('/');
}

function getFirstLine(text) {
  return typeof text === 'string' ? text.split('\n')[0].trim() : '';
}

function getNonEmptyLines(text) {
  return typeof text === 'string'
    ? text
        .split('\n')
        .map((line) => line.trim())
        .filter(Boolean)
    : [];
}

function formatCompactMetadata(items) {
  return items.filter(Boolean).join(' · ');
}

function syncComposerHeight() {
  if (!composerInput) {
    return;
  }

  composerInput.style.height = 'auto';
  const nextHeight = Math.min(Math.max(composerInput.scrollHeight, composerMinHeight), composerMaxHeight);
  composerInput.style.height = `${nextHeight}px`;
  composerInput.style.overflowY = nextHeight >= composerMaxHeight ? 'auto' : 'hidden';
}

const chatCompletionEndpoint = '/api/chat/complete';
const chatStreamingEndpoint = '/api/chat/stream';
const chatCompletionContextLimit = 12;
const chatThinkingMessage = 'Thinking…';
const chatResponseCancelledMessage = 'Response cancelled.';
const chatRetryFailureMessage = "I couldn't retry that response. Please try again.";
const chatConfigMissingMessage = 'LLM provider is not configured. Check the Pi Agent GitHub Copilot authentication.';
const chatCompletionFailureMessage = "I couldn't reach the language model. Check the LLM configuration and try again.";
const composerMinHeight = 58;
const composerMaxHeight = 220;
const traceLinkedPlaceholderMessage =
  'Trace linking is not connected yet. This chat is currently not linked to a trace. Future versions will link chats to the trace system.';
const traceLinkPlaceholderMessage =
  'Trace linking is a placeholder in 10E. No trace was created or linked.';
const runtimeStatusEndpoint = '/api/runtime-status';

function createFallbackRuntimeStatus() {
  return {
    version: 1,
    runtime: {
      mode: 'local',
      label: 'Local session',
    },
    memory: {
      status: 'off',
      label: 'Memory off',
    },
    context: {
      status: 'off',
      label: 'Context off',
    },
    trace: {
      status: 'not_linked',
      label: 'Trace not linked',
    },
    llm: {
      status: 'off',
      label: 'LLM off',
    },
  };
}

function normalizeRuntimeStatus(candidate) {
  if (!isPlainObject(candidate) || candidate.version !== 1) {
    return null;
  }

  const runtime = isPlainObject(candidate.runtime) ? candidate.runtime : {};
  const memory = isPlainObject(candidate.memory) ? candidate.memory : {};
  const context = isPlainObject(candidate.context) ? candidate.context : {};
  const trace = isPlainObject(candidate.trace) ? candidate.trace : {};
  const llm = isPlainObject(candidate.llm) ? candidate.llm : {};

  return {
    version: 1,
    runtime: {
      mode: typeof runtime.mode === 'string' && runtime.mode.trim() ? runtime.mode.trim() : 'local',
      label: typeof runtime.label === 'string' && runtime.label.trim() ? runtime.label.trim() : 'Local session',
    },
    memory: {
      status: typeof memory.status === 'string' && memory.status.trim() ? memory.status.trim() : 'off',
      label: typeof memory.label === 'string' && memory.label.trim() ? memory.label.trim() : 'Memory off',
    },
    context: {
      status: typeof context.status === 'string' && context.status.trim() ? context.status.trim() : 'off',
      label: typeof context.label === 'string' && context.label.trim() ? context.label.trim() : 'Context off',
    },
    trace: {
      status: typeof trace.status === 'string' && trace.status.trim() ? trace.status.trim() : 'not_linked',
      label: typeof trace.label === 'string' && trace.label.trim() ? trace.label.trim() : 'Trace not linked',
    },
    llm: {
      status: typeof llm.status === 'string' && llm.status.trim() ? llm.status.trim() : 'off',
      label: typeof llm.label === 'string' && llm.label.trim() ? llm.label.trim() : 'LLM off',
    },
  };
}

let runtimeStatus = createFallbackRuntimeStatus();
const commandCatalog = [
  {
    name: '/status',
    kind: 'read-only',
    description: 'Check runtime status.',
    insertText: '/status',
    usage: '/status',
  },
  {
    name: '/checkpoint',
    kind: 'mutating',
    description: 'Create a governed checkpoint for this chat/work.',
    insertText: '/checkpoint ',
    usage: '/checkpoint <label>',
  },
  {
    name: '/inject',
    kind: 'placeholder',
    description: 'Open the placeholder RCK scope suggestion flow.',
    insertText: '/inject',
    usage: '/inject',
  },
  {
    name: '/hermes fake',
    kind: 'mutating',
    description: 'Run a safe fake Hermes inspection.',
    insertText: '/hermes fake ',
    usage: '/hermes fake <prompt>',
  },
  {
    name: '/trace',
    kind: 'read-only',
    description: 'Show trace-link placeholder information.',
    insertText: '/trace',
    usage: '/trace',
  },
  {
    name: '/trace link',
    kind: 'placeholder',
    description: 'Trace linking placeholder. Does not create a real trace yet.',
    insertText: '/trace link',
    usage: '/trace link',
  },
  {
    name: '/help',
    kind: 'read-only',
    description: 'Show available RufusChat commands.',
    insertText: '/help',
    usage: '/help',
  },
];

let confirmResolver = null;
let activeChatCompletionRun = null;

function makeId(prefix) {
  const random = globalThis.crypto?.randomUUID?.();
  return `${prefix}-${random ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`}`;
}

function nowIso() {
  return new Date().toISOString();
}

function makeContextPackId() {
  return `cp_${Date.now()}_${Math.random().toString(16).slice(2, 8)}`;
}

function isContextPackCandidateContent(content) {
  return (
    typeof content === 'string' &&
    (content.startsWith('Context candidate prepared') || content.startsWith('Context pack candidate prepared.'))
  );
}

function isContextPackInjectedContent(content) {
  return typeof content === 'string' && content.startsWith(contextPackInjectedMessage);
}

function isContextPackCancelledContent(content) {
  return typeof content === 'string' && content.startsWith(contextPackCancelledMessage);
}

function getMessageContextPackId(message) {
  return typeof message?.links?.contextPackId === 'string' ? message.links.contextPackId : null;
}

function getMessageCheckpointId(message) {
  return typeof message?.links?.checkpointId === 'string' ? message.links.checkpointId : null;
}

function makeCheckpointId() {
  return `chk_${Date.now()}_${Math.random().toString(16).slice(2, 8)}`;
}

function ensureChatCheckpointHistory(chat) {
  if (!Array.isArray(chat?.checkpoints)) {
    if (chat) {
      chat.checkpoints = [];
    }
    return chat?.checkpoints ?? [];
  }

  return chat.checkpoints;
}

function getChatCheckpointHistoryItem(chat, checkpointId) {
  return ensureChatCheckpointHistory(chat).find((item) => item?.checkpointId === checkpointId) ?? null;
}

function createChatCheckpointHistoryItem(checkpointId, label, sourceMessageId = null) {
  const timestamp = nowIso();

  return {
    checkpointId,
    status: 'created',
    label,
    summary: 'Product checkpoint only. No internal anchor was created. No raw evidence stored.',
    createdAt: timestamp,
    updatedAt: timestamp,
    sourceMessageId,
    resultMessageId: null,
    sourceKind: 'product',
    safeMetadata: {
      productOnly: true,
      command: '/checkpoint',
    },
  };
}

function upsertChatCheckpointHistoryItem(chat, checkpointId, updates = {}) {
  const history = ensureChatCheckpointHistory(chat);
  let item = getChatCheckpointHistoryItem(chat, checkpointId);

  if (!item) {
    item = createChatCheckpointHistoryItem(checkpointId, 'Untitled checkpoint');
    history.push(item);
  }

  Object.assign(item, updates);
  item.updatedAt = nowIso();
  return item;
}

function getChatCheckpointSummary(chat) {
  const summary = {
    total: 0,
    created: 0,
    superseded: 0,
    archived: 0,
  };

  for (const checkpoint of chat?.checkpoints ?? []) {
    if (!checkpoint || typeof checkpoint !== 'object') {
      continue;
    }

    summary.total += 1;

    if (checkpoint.status === 'superseded') {
      summary.superseded += 1;
    } else if (checkpoint.status === 'archived') {
      summary.archived += 1;
    } else {
      summary.created += 1;
    }
  }

  return summary;
}

function formatCheckpointSummary(summary) {
  if (!summary || summary.total === 0) {
    return '';
  }

  return summary.total === 1 ? 'Checkpoints: 1' : `Checkpoints: ${summary.total}`;
}

function formatSidebarCheckpointSummary(summary) {
  if (!summary || summary.total === 0) {
    return '';
  }

  return summary.total === 1 ? 'Checkpoints: 1' : `Checkpoints: ${summary.total}`;
}

function createCheckpointMessageBadge() {
  const badge = document.createElement('span');
  badge.className = 'message__checkpoint-badge';
  badge.textContent = 'Checkpoint';
  return badge;
}

function createCheckpointResultContent(label) {
  return [
    `Checkpoint created · ${label}`,
    'Product checkpoint only · No evidence stored',
  ].join('\n');
}

function getChatInjectionSummary(chat) {
  const summary = {
    total: 0,
    candidate: 0,
    injected: 0,
    cancelled: 0,
  };

  for (const injection of chat?.injections ?? []) {
    if (!injection || typeof injection !== 'object') {
      continue;
    }

    summary.total += 1;

    if (injection.status === 'injected') {
      summary.injected += 1;
    } else if (injection.status === 'cancelled') {
      summary.cancelled += 1;
    } else {
      summary.candidate += 1;
    }
  }

  return summary;
}

function getInjectionByContextPackId(chat, contextPackId) {
  if (!chat || typeof contextPackId !== 'string' || contextPackId.length === 0) {
    return null;
  }

  return (chat.injections ?? []).find((item) => item?.contextPackId === contextPackId) ?? null;
}

function getContextPackCandidateSummaryText() {
  return `${contextPackCandidateSummaryLines.join('. ')}.`;
}

function formatInjectionSummary(summary) {
  if (!summary || summary.total === 0) {
    return '';
  }

  const parts = [];
  if (summary.injected > 0) {
    parts.push(`${summary.injected} injected`);
  }
  if (summary.cancelled > 0) {
    parts.push(`${summary.cancelled} cancelled`);
  }
  if (summary.candidate > 0) {
    parts.push(`${summary.candidate} candidate${summary.candidate === 1 ? '' : 's'}`);
  }

  if (parts.length === 0) {
    return `${summary.total} context pack${summary.total === 1 ? '' : 's'}`;
  }

  return `Context: ${parts.join(' · ')}`;
}

function formatSidebarInjectionSummary(summary) {
  if (!summary || summary.total === 0) {
    return '';
  }

  if (summary.total === 1) {
    if (summary.injected === 1) {
      return '1 injected';
    }

    if (summary.cancelled === 1) {
      return '1 cancelled';
    }

    return '1 candidate';
  }

  if (summary.injected > 0 && summary.cancelled === 0 && summary.candidate === 0) {
    return `${summary.injected} injected`;
  }

  return `${summary.total} context packs`;
}

function getContextPackStatusLabel(status) {
  if (status === 'injected') {
    return 'Injected';
  }

  if (status === 'cancelled') {
    return 'Cancelled';
  }

  return 'Candidate';
}

function createContextPackStatusChip(status, className = 'context-pack-card__status') {
  const chip = document.createElement('span');
  chip.className = `${className} ${className}--${status}`;
  chip.textContent = getContextPackStatusLabel(status);
  return chip;
}

function createContextPackMessageBadge(status) {
  return createContextPackStatusChip(status, 'message__context-pack-badge');
}

function getContextPackMessageStatus(message, contextPackLifecycleById = new Map(), chat = null) {
  const contextPackId = getMessageContextPackId(message);
  if (!contextPackId) {
    return null;
  }

  if (isContextPackCandidateMessage(message)) {
    return contextPackLifecycleById.get(contextPackId) ?? 'candidate';
  }

  const historyItem = getInjectionByContextPackId(chat, contextPackId);
  if (historyItem?.status === 'injected' || historyItem?.status === 'cancelled') {
    return historyItem.status;
  }

  if (contextPackLifecycleById.has(contextPackId)) {
    return contextPackLifecycleById.get(contextPackId);
  }

  if (isContextPackInjectedContent(message?.content)) {
    return 'injected';
  }

  if (isContextPackCancelledContent(message?.content)) {
    return 'cancelled';
  }

  return null;
}

function ensureChatInjectionHistory(chat) {
  if (!Array.isArray(chat?.injections)) {
    if (chat) {
      chat.injections = [];
    }
    return chat?.injections ?? [];
  }

  return chat.injections;
}

function getChatInjectionHistoryItem(chat, contextPackId) {
  return ensureChatInjectionHistory(chat).find((item) => item?.contextPackId === contextPackId) ?? null;
}

function createChatInjectionHistoryItem(contextPackId) {
  const timestamp = nowIso();

  return {
    contextPackId,
    status: 'candidate',
    title: contextPackInjectionHistoryTitle,
    summary: getContextPackCandidateSummaryText(),
    createdAt: timestamp,
    updatedAt: timestamp,
    injectedAt: null,
    cancelledAt: null,
    candidateMessageId: null,
    resultMessageId: null,
    sourceKind: contextPackInjectionHistorySourceKind,
    safeMetadata: null,
  };
}

function upsertChatInjectionHistoryItem(chat, contextPackId, updates = {}) {
  const history = ensureChatInjectionHistory(chat);
  let item = getChatInjectionHistoryItem(chat, contextPackId);

  if (!item) {
    item = createChatInjectionHistoryItem(contextPackId);
    history.push(item);
  }

  Object.assign(item, updates);
  item.updatedAt = nowIso();
  return item;
}

function getContextPackLifecycleByChat(chat) {
  const lifecycle = new Map();

  for (const injection of chat?.injections ?? []) {
    const contextPackId = typeof injection?.contextPackId === 'string' ? injection.contextPackId : null;
    if (!contextPackId) {
      continue;
    }

    if (typeof injection.status === 'string') {
      lifecycle.set(contextPackId, injection.status);
    }
  }

  for (const message of chat?.messages ?? []) {
    const contextPackId = getMessageContextPackId(message);
    if (!contextPackId) {
      continue;
    }

    if (lifecycle.has(contextPackId)) {
      continue;
    }

    const content = typeof message?.content === 'string' ? message.content : '';
    if (isContextPackCandidateContent(content)) {
      lifecycle.set(contextPackId, 'candidate');
    } else if (isContextPackInjectedContent(content)) {
      lifecycle.set(contextPackId, 'injected');
    } else if (isContextPackCancelledContent(content)) {
      lifecycle.set(contextPackId, 'cancelled');
    }
  }

  return lifecycle;
}

function createContextPackCandidateContent(_contextPackId) {
  return [
    'Context candidate prepared.',
    'Current chat context placeholder.',
    'ProductState metadata placeholder.',
    'Placeholder only · No raw evidence · No .pi/rck read.',
  ].join('\n');
}

function createContextPackResultContent(_contextPackId, status) {
  const title = status === 'cancelled' ? contextPackCancelledMessage : contextPackInjectedMessage;
  const detail = status === 'cancelled' ? 'No changes applied' : 'Ready for use';

  return [title, detail, 'Product event only · No raw evidence stored'].join('\n');
}

function formatContextScopeList(items) {
  if (!Array.isArray(items) || items.length === 0) {
    return '';
  }

  const formatted = items
    .map((item) => {
      if (!item || typeof item !== 'object') {
        return '';
      }

      const label = typeof item.label === 'string' && item.label.trim() ? item.label.trim() : item.path;
      const path = typeof item.path === 'string' && item.path.trim() ? item.path.trim() : 'unknown';
      const reason = typeof item.reason === 'string' && item.reason.trim() ? item.reason.trim() : '';
      return reason ? `${label} (${path}) — ${reason}` : `${label} (${path})`;
    })
    .filter(Boolean)
    .join(' · ');

  return formatted || '';
}

function formatContextPackPreviewList(items) {
  if (!Array.isArray(items) || items.length === 0) {
    return '';
  }

  return items
    .map((item) => {
      if (typeof item === 'string') {
        return item.trim();
      }

      if (!item || typeof item !== 'object') {
        return '';
      }

      const title = typeof item.title === 'string' && item.title.trim() ? item.title.trim() : '';
      const summary = typeof item.summary === 'string' && item.summary.trim() ? item.summary.trim() : '';
      const text = typeof item.text === 'string' && item.text.trim() ? item.text.trim() : '';
      const label = title || summary || text;
      return label;
    })
    .filter(Boolean)
    .join(' · ');
}

function formatContextScopeConfidence(value) {
  if (typeof value !== 'number' || Number.isNaN(value)) {
    return '';
  }

  return `${Math.round(value * 100)}%`;
}

function hasRenderableValue(value) {
  if (value === null || value === undefined) {
    return false;
  }

  if (typeof value === 'string') {
    return value.trim().length > 0;
  }

  if (Array.isArray(value)) {
    return value.length > 0;
  }

  if (typeof value === 'number') {
    return Number.isFinite(value);
  }

  if (typeof value === 'boolean') {
    return true;
  }

  if (typeof value === 'object') {
    return Object.keys(value).length > 0;
  }

  return false;
}

function setFieldVisibility(fieldEl, value) {
  if (!fieldEl) {
    return;
  }

  fieldEl.hidden = !hasRenderableValue(value);
}

function setTextField(fieldEl, value, fallback = '') {
  if (!fieldEl) {
    return;
  }

  const nextValue = hasRenderableValue(value) ? String(value).trim() : fallback;
  fieldEl.textContent = nextValue;
}

function createFallbackContextScopeSuggestion(intentText = '') {
  const userIntentText = typeof intentText === 'string' && intentText.trim() ? intentText.trim() : 'Qué decisiones tomamos sobre ContextPack y RufusChat?';

  return {
    suggestionId: 'rck-scope-suggestion-placeholder-v1',
    status: 'suggested',
    userIntentText,
    suggestedTarget: {
      targetType: 'anchor',
      targetId: 'mock-anchor-rufuschat-contextpack-adapter',
      label: 'RufusChat ContextPack adapter boundary',
    },
    suggestedDepth: 4,
    includeAnchors: true,
    includeEvidenceRefs: true,
    includeDocs: true,
    selectedArtifacts: [
      {
        path: 'docs/CONTEXT_PACK_BOUNDARY.md',
        label: 'ContextPack boundary',
        reason: 'Defines the scope contract and the user approval boundary.',
      },
      {
        path: 'docs/RUFUSCHAT_ADAPTER_DESIGN.md',
        label: 'RufusChat adapter design',
        reason: 'Contains the integration boundary for the placeholder flow.',
      },
      {
        path: 'apps/rufuschat-ui/RCK_CONTEXTPACK_PREVIEW.md',
        label: 'ContextPack preview placeholder doc',
        reason: 'Documents the placeholder preview that appears after approval.',
      },
    ],
    candidateArtifacts: [
      {
        path: 'apps/rufuschat-ui/RCK_CONTEXT_SCOPE_SUGGESTION.md',
        label: 'Context scope suggestion doc',
        reason: 'Documents this dev-only suggestion layer.',
      },
      {
        path: 'apps/rufuschat-ui/server.mjs',
        label: 'RufusChat UI server',
        reason: 'Hosts the placeholder suggestion endpoint.',
      },
      {
        path: 'apps/rufuschat-ui/public/app.js',
        label: 'RufusChat UI client',
        reason: 'Renders the placeholder suggestion flow in memory only.',
      },
      {
        path: 'apps/rufuschat-ui/public/index.html',
        label: 'RufusChat UI shell',
        reason: 'Contains the visible Attach RCK Context panel.',
      },
    ],
    excludedArtifacts: [
      {
        path: 'storage/CLI docs',
        label: 'Storage / CLI docs',
        reason: 'Excluded because this phase is placeholder/dev-only and does not use storage data.',
      },
      {
        path: '.data product state',
        label: 'Product state files',
        reason: 'Excluded because suggestions stay in memory and do not persist to .data.',
      },
      {
        path: 'rck-core internals',
        label: 'RCK Core internals',
        reason: 'Excluded because the implementation does not read or execute RCK Core.',
      },
    ],
    rationale:
      'The intent mentions ContextPack and RufusChat integration, so the placeholder scope centers the adapter boundary, preview placeholder, and the UI/server files that host the dev-only flow.',
    confidence: 0.83,
    warnings: [
      'Placeholder / dev-only suggestion.',
      'No LLM call was made.',
      'No TraceSlice was generated.',
      'No ContextPack was generated.',
      'Confirm injection stays disabled.',
    ],
    preview: {
      previewId: 'rck-context-scope-preview-placeholder-v1',
      status: 'placeholder',
      placeholder: true,
      available: true,
      title: 'RufusChat ContextPack preview placeholder',
      summary: 'Demo/dev-only preview. The real TraceSlice and ContextPack chain is still pending.',
      derivedFromSuggestion: false,
      confirmationDisabled: true,
      nextSteps: [
        'Connect the scope to a real selector in a later phase.',
        'Generate a real TraceSlice from an approved scope.',
        'Build a real ContextPack from that TraceSlice.',
        'Show a real preview of the generated chain.',
        'Enable confirm injection only after the real chain exists.',
      ],
    },
    userDecision: {
      decision: 'pending',
      decidedAt: null,
      decidedBy: 'user',
      notes: ['User approval is required before the suggestion becomes approved locally.'],
    },
  };
}

function normalizeContextScopeSuggestion(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return createFallbackContextScopeSuggestion();
  }

  return {
    ...createFallbackContextScopeSuggestion(candidate.userIntentText),
    ...candidate,
    suggestedTarget: {
      ...createFallbackContextScopeSuggestion().suggestedTarget,
      ...(candidate.suggestedTarget ?? {}),
    },
    selectedArtifacts: Array.isArray(candidate.selectedArtifacts) ? candidate.selectedArtifacts : createFallbackContextScopeSuggestion().selectedArtifacts,
    candidateArtifacts: Array.isArray(candidate.candidateArtifacts) ? candidate.candidateArtifacts : createFallbackContextScopeSuggestion().candidateArtifacts,
    excludedArtifacts: Array.isArray(candidate.excludedArtifacts) ? candidate.excludedArtifacts : createFallbackContextScopeSuggestion().excludedArtifacts,
    warnings: Array.isArray(candidate.warnings) ? candidate.warnings : createFallbackContextScopeSuggestion().warnings,
    preview: {
      ...createFallbackContextScopeSuggestion().preview,
      ...(candidate.preview ?? {}),
    },
    userDecision: {
      ...createFallbackContextScopeSuggestion().userDecision,
      ...(candidate.userDecision ?? {}),
    },
  };
}

async function loadContextScopeSuggestion(intentText = '') {
  activeContextScopeSuggestion = normalizeContextScopeSuggestion(createFallbackContextScopeSuggestion(intentText));
  activeContextPackPreview = null;
  activeLoadedContextPackPreview = null;
  activeContextPackGenerationRequest = null;
  activeContextPackGenerationResponse = null;
  isContextSidePanelOpen = true;
  renderContextScopeSuggestionPanel();

  try {
    const data = await postJson(contextScopeSuggestionEndpoint, { intent: intentText });
    activeContextScopeSuggestion = normalizeContextScopeSuggestion(data.suggestion ?? data);
  } catch {
    activeContextScopeSuggestion = normalizeContextScopeSuggestion({
      ...createFallbackContextScopeSuggestion(intentText),
      warnings: [
        'Placeholder / dev-only suggestion.',
        'Suggestion endpoint is unavailable.',
        'Using local fallback mock only.',
      ],
    });
  }

  renderContextScopeSuggestionPanel();
}

function getSuggestedScopeIntentText() {
  return typeof composerInput?.value === 'string' && composerInput.value.trim()
    ? composerInput.value.trim()
    : 'Qué decisiones tomamos sobre ContextPack y RufusChat?';
}

function updateContextScopeSuggestionState(nextSuggestion) {
  activeContextScopeSuggestion = normalizeContextScopeSuggestion(nextSuggestion);
  renderContextScopeSuggestionPanel();
}

async function loadContextPackGenerationFromApprovedScope() {
  if (!activeContextScopeSuggestion || activeContextScopeSuggestion.status !== 'approved') {
    return null;
  }

  const approvedScope = {
    suggestionId: activeContextScopeSuggestion.suggestionId,
    targetType: activeContextScopeSuggestion.targetType ?? activeContextScopeSuggestion.suggestedTarget?.targetType ?? 'unknown',
    targetId: activeContextScopeSuggestion.targetId ?? activeContextScopeSuggestion.suggestedTarget?.targetId ?? 'mock-context-scope-target-unknown',
    targetLabel:
      activeContextScopeSuggestion.targetLabel ??
      activeContextScopeSuggestion.suggestedTarget?.label ??
      activeContextScopeSuggestion.suggestedTarget?.targetLabel ??
      'Unresolved placeholder target',
    depth: activeContextScopeSuggestion.depth ?? activeContextScopeSuggestion.suggestedDepth ?? 0,
    includeAnchors: activeContextScopeSuggestion.includeAnchors === true,
    includeEvidenceRefs: activeContextScopeSuggestion.includeEvidenceRefs === true,
    includeDocs: activeContextScopeSuggestion.includeDocs === true,
    selectedArtifacts: Array.isArray(activeContextScopeSuggestion.selectedArtifacts)
      ? activeContextScopeSuggestion.selectedArtifacts
      : [],
  };
  const userIntentText = approvedScope.userIntentText || getSuggestedScopeIntentText();
  const request = normalizeContextPackGenerationRequest(createFallbackContextPackGenerationRequest(approvedScope, userIntentText));
  activeContextPackGenerationRequest = request;

  try {
    const data = await postJson(contextPackGenerationPlaceholderEndpoint, {
      approvedScope: request.approvedScope,
      userIntentText: request.userIntentText,
    });
    activeContextPackGenerationResponse = normalizeContextPackGenerationResponse(data.response ?? data);
  } catch {
    activeContextPackGenerationResponse = normalizeContextPackGenerationResponse(
      createFallbackContextPackGenerationResponse(request.approvedScope, request.userIntentText),
    );
  }

  activeContextPackPreview = normalizeContextPackPreview(activeContextPackGenerationResponse.contextPackPreview ?? createFallbackContextPackPreview());
  renderContextPackGenerationRequestPanel();
  renderContextPackPreviewPanel();
  return activeContextPackGenerationResponse;
}

async function openContextPackPreview() {
  isContextSidePanelOpen = true;
  if (!activeContextScopeSuggestion) {
    await loadContextScopeSuggestion(getSuggestedScopeIntentText());
    return;
  }

  renderContextScopeSuggestionPanel();
}

function closeContextPackPreview() {
  isContextSidePanelOpen = false;
  renderContextScopeSuggestionPanel();
}

async function approveContextScopeSuggestion() {
  if (!activeContextScopeSuggestion) {
    return;
  }

  updateContextScopeSuggestionState({
    ...activeContextScopeSuggestion,
    status: 'approved',
    userDecision: {
      ...activeContextScopeSuggestion.userDecision,
      decision: 'approved',
      decidedAt: nowIso(),
      notes: ['Approved locally only. No TraceSlice, ContextPack, or injection was generated.'],
    },
  });

  await loadContextPackGenerationFromApprovedScope();
}


function rejectContextScopeSuggestion() {
  if (!activeContextScopeSuggestion) {
    return;
  }

  updateContextScopeSuggestionState({
    ...activeContextScopeSuggestion,
    status: 'rejected',
    userDecision: {
      ...activeContextScopeSuggestion.userDecision,
      decision: 'rejected',
      decidedAt: nowIso(),
      notes: ['Rejected locally. Nothing was generated, persisted, or injected.'],
    },
  });
  activeContextPackPreview = null;
  activeLoadedContextPackPreview = null;
  activeContextPackGenerationRequest = null;
  activeContextPackGenerationResponse = null;
}

async function adjustContextScopeSuggestion() {
  if (!activeContextScopeSuggestion) {
    return;
  }

  const currentIntent = activeContextScopeSuggestion.userIntentText || getSuggestedScopeIntentText();
  const nextIntent = window.prompt('Adjust the suggested scope intent', currentIntent);
  if (nextIntent === null) {
    return;
  }

  await loadContextScopeSuggestion(nextIntent);
  if (!activeContextScopeSuggestion) {
    return;
  }

  updateContextScopeSuggestionState({
    ...activeContextScopeSuggestion,
    status: 'adjusted',
    userIntentText: nextIntent.trim() || currentIntent,
    userDecision: {
      ...activeContextScopeSuggestion.userDecision,
      decision: 'adjusted',
      decidedAt: nowIso(),
      notes: ['Adjusted locally. The scope suggestion remains placeholder/dev-only.'],
    },
  });
}

function renderContextScopeSuggestionPanel() {
  if (!contextSidePanelEl) {
    return;
  }

  contextSidePanelEl.hidden = !isContextSidePanelOpen;
  if (attachContextPackButton) {
    attachContextPackButton.textContent = isContextSidePanelOpen ? 'Close RCK Context' : 'Attach RCK Context';
    attachContextPackButton.setAttribute('aria-expanded', isContextSidePanelOpen ? 'true' : 'false');
  }

  if (!isContextSidePanelOpen) {
    if (contextScopeSuggestionPanel) {
      contextScopeSuggestionPanel.hidden = true;
    }
    if (contextPackPreviewPanel) {
      contextPackPreviewPanel.hidden = true;
    }
    if (contextSidePanelBadgeEl) {
      contextSidePanelBadgeEl.textContent = 'Placeholder / not connected';
    }
    return;
  }

  const suggestion = activeContextScopeSuggestion;
  if (contextSidePanelBadgeEl) {
    contextSidePanelBadgeEl.textContent = suggestion?.status === 'approved'
      ? 'Approved placeholder'
      : suggestion?.status === 'rejected'
        ? 'Rejected locally'
        : suggestion?.status === 'adjusted'
          ? 'Adjusted locally'
          : 'Placeholder / not connected';
  }

  if (!suggestion) {
    if (contextScopeSuggestionPanel) {
      contextScopeSuggestionPanel.hidden = true;
    }
    if (contextPackPreviewPanel) {
      contextPackPreviewPanel.hidden = true;
    }
    return;
  }

  if (contextScopeSuggestionPanel) {
    contextScopeSuggestionPanel.hidden = false;
  }

  if (contextScopeSuggestionTitleEl) {
    contextScopeSuggestionTitleEl.textContent = 'Suggested RCK scope';
  }

  if (contextScopeSuggestionStatusEl) {
    contextScopeSuggestionStatusEl.textContent =
      suggestion.status === 'approved'
        ? 'Approved placeholder'
        : suggestion.status === 'rejected'
          ? 'Rejected locally'
          : suggestion.status === 'adjusted'
            ? 'Adjusted locally'
            : 'Placeholder / not generated';
  }

  if (contextScopeSuggestionSummaryEl) {
    contextScopeSuggestionSummaryEl.textContent =
      suggestion.status === 'approved'
        ? 'Approved locally only. No TraceSlice, ContextPack, or injection was generated.'
        : suggestion.status === 'rejected'
          ? 'Rejected locally. Nothing was generated, persisted, or injected.'
          : suggestion.status === 'adjusted'
            ? 'Adjusted locally. This remains a demo/dev-only placeholder.'
            : 'This suggestion is demo/dev-only and never calls RCK or an LLM.';
  }

  setFieldVisibility(document.getElementById('context-scope-suggestion-field-intent'), suggestion.userIntentText);
  setTextField(contextScopeSuggestionIntentEl, suggestion.userIntentText, '');

  const target = suggestion.suggestedTarget ?? {};
  const targetText = [target.targetType, target.label, target.targetId].filter(Boolean).join(' · ');
  setFieldVisibility(document.getElementById('context-scope-suggestion-field-target'), targetText);
  setTextField(contextScopeSuggestionTargetEl, targetText, '');

  setFieldVisibility(document.getElementById('context-scope-suggestion-field-depth'), suggestion.suggestedDepth);
  setTextField(contextScopeSuggestionDepthEl, suggestion.suggestedDepth, '');

  setFieldVisibility(document.getElementById('context-scope-suggestion-field-confidence'), suggestion.confidence);
  setTextField(contextScopeSuggestionConfidenceEl, formatContextScopeConfidence(suggestion.confidence), '');

  const selectedArtifacts = formatContextScopeList(suggestion.selectedArtifacts);
  setFieldVisibility(document.getElementById('context-scope-suggestion-field-selected-artifacts'), selectedArtifacts);
  setTextField(contextScopeSuggestionArtifactsSelectedEl, selectedArtifacts, '');

  const candidateArtifacts = formatContextScopeList(suggestion.candidateArtifacts);
  setFieldVisibility(document.getElementById('context-scope-suggestion-field-candidate-artifacts'), candidateArtifacts);
  setTextField(contextScopeSuggestionArtifactsCandidatesEl, candidateArtifacts, '');

  const excludedArtifacts = formatContextScopeList(suggestion.excludedArtifacts);
  setFieldVisibility(document.getElementById('context-scope-suggestion-field-excluded-artifacts'), excludedArtifacts);
  setTextField(contextScopeSuggestionArtifactsExcludedEl, excludedArtifacts, '');

  setFieldVisibility(document.getElementById('context-scope-suggestion-field-rationale'), suggestion.rationale);
  setTextField(contextScopeSuggestionRationaleEl, suggestion.rationale, '');

  const warningsText = Array.isArray(suggestion.warnings) ? suggestion.warnings.filter(Boolean).join(' · ') : '';
  setFieldVisibility(document.getElementById('context-scope-suggestion-field-warnings'), warningsText);
  setTextField(contextScopeSuggestionWarningsEl, warningsText, '');

  const decision = suggestion.userDecision ?? {};
  const decisionLabel = decision.decision === 'approved'
    ? 'Approved placeholder'
    : decision.decision === 'rejected'
      ? 'Rejected locally'
      : decision.decision === 'adjusted'
        ? 'Adjusted locally'
        : 'Pending approval';
  const decidedAt = typeof decision.decidedAt === 'string' && decision.decidedAt ? decision.decidedAt : '—';
  const decisionText = `${decisionLabel} · ${decidedAt}`;
  setFieldVisibility(document.getElementById('context-scope-suggestion-field-decision'), decisionText);
  setTextField(contextScopeSuggestionDecisionEl, decisionText, '');

  const previewRelationText = suggestion.preview?.derivedFromSuggestion
    ? 'Preview is derived from the approved scope.'
    : 'Preview placeholder exists, but it was not generated from this scope. Demo/dev-only only.';
  setFieldVisibility(document.getElementById('context-scope-suggestion-field-preview'), previewRelationText);
  setTextField(contextScopeSuggestionPreviewEl, previewRelationText, '');

  if (contextScopeSuggestionApproveButton) {
    contextScopeSuggestionApproveButton.disabled = suggestion.status === 'approved';
  }

  if (contextScopeSuggestionRejectButton) {
    contextScopeSuggestionRejectButton.disabled = suggestion.status === 'rejected';
  }

  if (contextScopeSuggestionAdjustButton) {
    contextScopeSuggestionAdjustButton.disabled = false;
  }

  renderContextPackGenerationRequestPanel();
  renderContextPackPreviewPanel();
}

function createFallbackContextPackPreview() {
  return {
    phase: 22,
    previewMode: 'placeholder',
    placeholder: true,
    contextPackId: 'cp_preview_placeholder_v1',
    contextPackHash: 'sha256:placeholder-context-pack-v1',
    title: 'RufusChat ContextPack preview placeholder',
    sourceTraceSliceHashes: ['trace-slice-placeholder-a', 'trace-slice-placeholder-b'],
    sectionsVisible: [
      {
        id: 'summary',
        title: 'Summary',
        visible: true,
        summary: 'Placeholder only. No live RCK preview is connected.',
        text: 'This phase only exposes a contract-shaped mock preview for RufusChat.',
      },
      {
        id: 'provenance',
        title: 'Provenance',
        visible: true,
        summary: 'Source docs and commit reference for the contract preview.',
        text: 'apps/rufuschat-ui/RCK_CONTEXTPACK_GENERATION_CONTRACT.md · docs/CONTEXT_PACK_BOUNDARY.md · schemas/rck.context_pack.v0.schema.json',
      },
      {
        id: 'constraints',
        title: 'Constraints',
        visible: true,
        summary: 'No RCK reads, no trace slice generation, no injection.',
        text: 'Placeholder-only contract. This panel is dev-preview and not connected to RCK Core.',
      },
    ],
    estimatedTokenCost: null,
    warnings: ['Placeholder / dev-only preview.', 'No RCK Core integration is active yet.', 'Confirm injection is disabled in this phase.'],
    constraints: ['Do not read .rck directly.', 'Do not execute RCK Core.', 'Do not generate a real ContextPack yet.', 'Do not persist injection records yet.'],
    provenanceSummary: {
      sourceDocument: 'apps/rufuschat-ui/RCK_CONTEXTPACK_GENERATION_CONTRACT.md',
      contractDocument: 'apps/rufuschat-ui/RCK_CONTEXTPACK_PREVIEW.md',
      schemaDocument: 'schemas/rck.context_pack.v0.schema.json',
      publishedCommit: '6a446731',
      mode: 'placeholder',
      notes: ['Mock provenance only.', 'No RCK Core internals are read in this phase.'],
    },
    exactTextToInject: 'Not available in this phase.',
    userApprovalStatus: 'not-available',
    injectionPolicy: {
      canPreview: true,
      canConfirm: false,
      canPersistRecord: false,
      canReadRckFilesystem: false,
      canCallRckCore: false,
      reason: 'Phase 22 is placeholder-only. Confirm injection is not available yet.',
    },
    injectionRecordDraft: {
      status: 'draft',
      recordId: null,
      createdAt: null,
      updatedAt: null,
      injectedAt: null,
      contextPackId: 'cp_preview_placeholder_v1',
      contextPackHash: 'sha256:placeholder-context-pack-v1',
      notes: ['Mock injection record draft only. No persistence in this phase.'],
    },
    reference: {
      contextPackId: 'cp_preview_placeholder_v1',
      contextPackHash: 'sha256:placeholder-context-pack-v1',
      title: 'RufusChat ContextPack preview placeholder',
      kind: 'placeholder',
    },
    sourceDocuments: [
      'apps/rufuschat-ui/RCK_CONTEXTPACK_GENERATION_CONTRACT.md',
      'apps/rufuschat-ui/RCK_CONTEXTPACK_PREVIEW.md',
      'schemas/rck.context_pack.v0.schema.json',
    ],
  };
}

function normalizeContextPackPreview(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return createFallbackContextPackPreview();
  }

  const fallback = createFallbackContextPackPreview();
  return {
    ...fallback,
    ...candidate,
    reference: {
      ...fallback.reference,
      ...(candidate.reference ?? {}),
    },
    injectionPolicy: {
      ...fallback.injectionPolicy,
      ...(candidate.injectionPolicy ?? {}),
    },
    injectionRecordDraft: {
      ...fallback.injectionRecordDraft,
      ...(candidate.injectionRecordDraft ?? {}),
    },
    provenanceSummary: {
      ...fallback.provenanceSummary,
      ...(candidate.provenanceSummary ?? {}),
    },
    preview: {
      ...fallback.preview,
      ...(candidate.preview ?? {}),
    },
  };
}

function createFallbackContextPackGenerationRequest(approvedScope = {}, userIntentText = '') {
  const approvedTarget = approvedScope?.targetLabel ?? approvedScope?.suggestedTarget?.label ?? 'Unresolved placeholder target';
  const requestId = `rck-context-pack-generation-request-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;

  return {
    requestId,
    createdAtUtc: nowIso(),
    source: 'approved-scope',
    userIntentText: typeof userIntentText === 'string' && userIntentText.trim() ? userIntentText.trim() : 'Qué decisiones tomamos sobre ContextPack y RufusChat?',
    approvedScope: {
      suggestionId: typeof approvedScope?.suggestionId === 'string' && approvedScope.suggestionId.trim()
        ? approvedScope.suggestionId.trim()
        : 'rck-scope-suggestion-placeholder-v1',
      targetType: typeof approvedScope?.targetType === 'string' && approvedScope.targetType.trim()
        ? approvedScope.targetType.trim()
        : 'unknown',
      targetId: typeof approvedScope?.targetId === 'string' && approvedScope.targetId.trim()
        ? approvedScope.targetId.trim()
        : 'mock-context-scope-target-unknown',
      targetLabel: typeof approvedScope?.targetLabel === 'string' && approvedScope.targetLabel.trim()
        ? approvedScope.targetLabel.trim()
        : approvedTarget,
      depth: typeof approvedScope?.depth === 'number' && Number.isFinite(approvedScope.depth) ? approvedScope.depth : 0,
      includeAnchors: approvedScope?.includeAnchors === true,
      includeEvidenceRefs: approvedScope?.includeEvidenceRefs === true,
      includeDocs: approvedScope?.includeDocs === true,
      selectedArtifacts: Array.isArray(approvedScope?.selectedArtifacts) ? approvedScope.selectedArtifacts : [],
    },
    requestedOutput: {
      contextPackSchemaVersion: 'rck.context_pack.v0',
      previewOnly: true,
    },
    safety: {
      requireUserApprovalForInjection: true,
      allowAutomaticInjection: false,
    },
  };
}

function normalizeContextPackGenerationRequest(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return createFallbackContextPackGenerationRequest();
  }

  const approvedScope = candidate.approvedScope && typeof candidate.approvedScope === 'object' ? candidate.approvedScope : {};
  return {
    requestId: typeof candidate.requestId === 'string' && candidate.requestId.trim() ? candidate.requestId.trim() : createFallbackContextPackGenerationRequest(approvedScope, candidate.userIntentText).requestId,
    createdAtUtc: typeof candidate.createdAtUtc === 'string' && candidate.createdAtUtc.trim()
      ? candidate.createdAtUtc.trim()
      : nowIso(),
    source: candidate.source === 'approved-scope' ? candidate.source : 'approved-scope',
    userIntentText: typeof candidate.userIntentText === 'string' && candidate.userIntentText.trim()
      ? candidate.userIntentText.trim()
      : createFallbackContextPackGenerationRequest(approvedScope, '').userIntentText,
    approvedScope: {
      suggestionId: typeof approvedScope.suggestionId === 'string' && approvedScope.suggestionId.trim() ? approvedScope.suggestionId.trim() : 'rck-scope-suggestion-placeholder-v1',
      targetType: typeof approvedScope.targetType === 'string' && approvedScope.targetType.trim() ? approvedScope.targetType.trim() : 'unknown',
      targetId: typeof approvedScope.targetId === 'string' && approvedScope.targetId.trim() ? approvedScope.targetId.trim() : 'mock-context-scope-target-unknown',
      targetLabel: typeof approvedScope.targetLabel === 'string' && approvedScope.targetLabel.trim() ? approvedScope.targetLabel.trim() : 'Unresolved placeholder target',
      depth: typeof approvedScope.depth === 'number' && Number.isFinite(approvedScope.depth) ? approvedScope.depth : 0,
      includeAnchors: approvedScope.includeAnchors === true,
      includeEvidenceRefs: approvedScope.includeEvidenceRefs === true,
      includeDocs: approvedScope.includeDocs === true,
      selectedArtifacts: Array.isArray(approvedScope.selectedArtifacts) ? approvedScope.selectedArtifacts : [],
    },
    requestedOutput: {
      contextPackSchemaVersion:
        typeof candidate.requestedOutput?.contextPackSchemaVersion === 'string' && candidate.requestedOutput.contextPackSchemaVersion.trim()
          ? candidate.requestedOutput.contextPackSchemaVersion.trim()
          : 'rck.context_pack.v0',
      previewOnly: candidate.requestedOutput?.previewOnly !== false,
    },
    safety: {
      requireUserApprovalForInjection: candidate.safety?.requireUserApprovalForInjection !== false,
      allowAutomaticInjection: candidate.safety?.allowAutomaticInjection === true,
    },
  };
}

function createFallbackContextPackGenerationResponse(approvedScope = {}, userIntentText = '') {
  const request = createFallbackContextPackGenerationRequest(approvedScope, userIntentText);
  const preview = createFallbackContextPackPreview();
  return {
    requestId: request.requestId,
    status: 'not_connected',
    contextPackReference: {
      contextPackId: `cp_${request.requestId}`,
      contextPackHash: `sha256:placeholder-${request.requestId}`,
      title: 'RufusChat ContextPack generation placeholder',
      kind: 'placeholder',
    },
    contextPackPreview: preview,
    warnings: [
      'Placeholder / dev-only generation request.',
      'Not connected to real RCK generation in this phase.',
      'No TraceSlice was generated.',
      'No ContextPack was generated.',
      'Confirm injection stays disabled.',
    ],
    constraints: [
      'No RCK Core execution.',
      'No .rck reads.',
      'No TraceSlice generation.',
      'No ContextPack generation.',
      'No persistence of the request.',
      'No persistence of injection records.',
      'No chat injection.',
      'No LLM calls.',
    ],
    provenanceSummary: {
      sourceDocument: 'apps/rufuschat-ui/RCK_CONTEXTPACK_GENERATION_CONTRACT.md',
      requestContractDocument: 'apps/rufuschat-ui/rck-contextpack-generation-contract.mjs',
      previewContractDocument: 'apps/rufuschat-ui/rck-contextpack-contract.mjs',
      previewProviderDocument: 'apps/rufuschat-ui/contextpack-preview-provider.mjs',
      mode: 'placeholder',
      notes: ['Approved scope is represented as a request contract first.', 'The response is placeholder / not connected.'],
    },
    message: 'Not connected to real RCK generation in this phase.',
  };
}

function normalizeContextPackGenerationResponse(candidate) {
  if (!candidate || typeof candidate !== 'object') {
    return createFallbackContextPackGenerationResponse();
  }

  return {
    requestId: typeof candidate.requestId === 'string' && candidate.requestId.trim() ? candidate.requestId.trim() : createFallbackContextPackGenerationResponse().requestId,
    status: typeof candidate.status === 'string' && candidate.status.trim() ? candidate.status.trim() : 'not_connected',
    contextPackReference: candidate.contextPackReference && typeof candidate.contextPackReference === 'object'
      ? {
          contextPackId: typeof candidate.contextPackReference.contextPackId === 'string' && candidate.contextPackReference.contextPackId.trim()
            ? candidate.contextPackReference.contextPackId.trim()
            : createFallbackContextPackGenerationResponse().contextPackReference.contextPackId,
          contextPackHash: typeof candidate.contextPackReference.contextPackHash === 'string' && candidate.contextPackReference.contextPackHash.trim()
            ? candidate.contextPackReference.contextPackHash.trim()
            : createFallbackContextPackGenerationResponse().contextPackReference.contextPackHash,
          title: typeof candidate.contextPackReference.title === 'string' && candidate.contextPackReference.title.trim()
            ? candidate.contextPackReference.title.trim()
            : createFallbackContextPackGenerationResponse().contextPackReference.title,
          kind: typeof candidate.contextPackReference.kind === 'string' && candidate.contextPackReference.kind.trim()
            ? candidate.contextPackReference.kind.trim()
            : createFallbackContextPackGenerationResponse().contextPackReference.kind,
        }
      : null,
    contextPackPreview: candidate.contextPackPreview && typeof candidate.contextPackPreview === 'object' ? candidate.contextPackPreview : createFallbackContextPackGenerationResponse().contextPackPreview,
    warnings: Array.isArray(candidate.warnings) && candidate.warnings.length > 0
      ? candidate.warnings.filter((item) => typeof item === 'string' && item.trim()).map((item) => item.trim())
      : createFallbackContextPackGenerationResponse().warnings,
    constraints: Array.isArray(candidate.constraints) && candidate.constraints.length > 0
      ? candidate.constraints.filter((item) => typeof item === 'string' && item.trim()).map((item) => item.trim())
      : createFallbackContextPackGenerationResponse().constraints,
    provenanceSummary: {
      ...createFallbackContextPackGenerationResponse().provenanceSummary,
      ...(typeof candidate.provenanceSummary === 'object' && candidate.provenanceSummary ? candidate.provenanceSummary : {}),
    },
    message: typeof candidate.message === 'string' && candidate.message.trim()
      ? candidate.message.trim()
      : 'Not connected to real RCK generation in this phase.',
  };
}

function formatContextPackGenerationRequestTarget(request) {
  const approvedScope = request?.approvedScope ?? {};
  return [approvedScope.targetType, approvedScope.targetLabel, approvedScope.targetId].filter(Boolean).join(' · ');
}

function formatContextPackGenerationRequestArtifacts(request) {
  return formatContextScopeList(request?.approvedScope?.selectedArtifacts ?? []);
}

function renderContextPackGenerationRequestPanel() {
  if (!contextPackGenerationRequestPanel) {
    return;
  }

  const request = activeContextPackGenerationRequest;
  const response = activeContextPackGenerationResponse ?? createFallbackContextPackGenerationResponse(request?.approvedScope, request?.userIntentText);
  const shouldShow = Boolean(request && isContextSidePanelOpen && activeContextScopeSuggestion?.status === 'approved');
  contextPackGenerationRequestPanel.hidden = !shouldShow;

  if (!shouldShow) {
    return;
  }

  if (contextPackGenerationRequestTitleEl) {
    contextPackGenerationRequestTitleEl.textContent = 'ContextPack generation request';
  }

  if (contextPackGenerationRequestStatusEl) {
    contextPackGenerationRequestStatusEl.textContent = response.status === 'ready' ? 'ready' : response.status === 'failed' ? 'failed' : 'placeholder / not_connected';
  }

  if (contextPackGenerationRequestSummaryEl) {
    contextPackGenerationRequestSummaryEl.textContent = response.message;
  }

  setFieldVisibility(document.getElementById('context-pack-generation-request-field-id'), request.requestId);
  setTextField(contextPackGenerationRequestIdEl, request.requestId, '');

  const targetText = formatContextPackGenerationRequestTarget(request);
  setFieldVisibility(document.getElementById('context-pack-generation-request-field-target'), targetText);
  setTextField(contextPackGenerationRequestTargetEl, targetText, '');

  setFieldVisibility(document.getElementById('context-pack-generation-request-field-depth'), request.approvedScope?.depth);
  setTextField(contextPackGenerationRequestDepthEl, String(request.approvedScope?.depth ?? ''), '');

  const artifactsText = formatContextPackGenerationRequestArtifacts(request);
  setFieldVisibility(document.getElementById('context-pack-generation-request-field-artifacts'), artifactsText);
  setTextField(contextPackGenerationRequestArtifactsEl, artifactsText, '');

  const warningsText = formatContextPackPreviewList(response.warnings);
  setFieldVisibility(document.getElementById('context-pack-generation-request-field-warnings'), warningsText);
  setTextField(contextPackGenerationRequestWarningsEl, warningsText, '');

  const constraintsText = formatContextPackPreviewList(response.constraints);
  setFieldVisibility(document.getElementById('context-pack-generation-request-field-constraints'), constraintsText);
  setTextField(contextPackGenerationRequestConstraintsEl, constraintsText, '');

  const provenanceText = [
    response.provenanceSummary?.sourceDocument,
    response.provenanceSummary?.requestContractDocument,
    response.provenanceSummary?.previewContractDocument,
    response.provenanceSummary?.mode,
  ]
    .filter(Boolean)
    .join(' · ');
  setFieldVisibility(document.getElementById('context-pack-generation-request-field-provenance'), provenanceText);
  setTextField(contextPackGenerationRequestProvenanceEl, provenanceText, '');

  setFieldVisibility(document.getElementById('context-pack-generation-request-field-message'), response.message);
  setTextField(contextPackGenerationRequestMessageEl, response.message, '');

  const previewRelationText = response.contextPackPreview?.placeholder
    ? 'Preview placeholder is derived from this approved-scope request contract.'
    : 'Preview is not connected to real generation in this phase.';
  setFieldVisibility(document.getElementById('context-pack-generation-request-field-preview'), previewRelationText);
  setTextField(contextPackGenerationRequestPreviewEl, previewRelationText, '');
}

function renderContextPackPreviewPanel() {
  if (!contextPackPreviewPanel) {
    return;
  }

  const preview = activeLoadedContextPackPreview ?? activeContextPackGenerationResponse?.contextPackPreview ?? activeContextPackPreview;
  const scopeApproved = activeContextScopeSuggestion?.status === 'approved';
  const shouldShow = Boolean(preview && scopeApproved && isContextSidePanelOpen);
  contextPackPreviewPanel.hidden = !shouldShow;

  if (!shouldShow) {
    if (contextSidePanelBadgeEl && !activeContextScopeSuggestion) {
      contextSidePanelBadgeEl.textContent = 'Placeholder';
    }

    if (contextPackPreviewLoadMessageEl) {
      contextPackPreviewLoadMessageEl.textContent = '';
    }

    return;
  }

  const isLoadedPreview = Boolean(activeLoadedContextPackPreview);
  const panelBadge = isLoadedPreview
    ? 'Loaded JSON / preview only'
    : preview.placeholder
      ? 'Placeholder / not connected'
      : 'Connected';

  if (contextSidePanelBadgeEl) {
    contextSidePanelBadgeEl.textContent = panelBadge;
  }

  if (contextPackPreviewTitleEl) {
    contextPackPreviewTitleEl.textContent = isLoadedPreview ? 'Loaded ContextPack preview' : preview.title;
  }

  if (contextPackPreviewStatusEl) {
    contextPackPreviewStatusEl.textContent = panelBadge;
  }

  if (contextPackPreviewSummaryEl) {
    contextPackPreviewSummaryEl.textContent = isLoadedPreview
      ? 'Loaded manually from JSON. RufusChat did not generate this ContextPack automatically in this phase.'
      : preview.placeholder
        ? 'Approved scope acknowledged locally. This preview is still a demo/dev-only placeholder and was not generated from the approved scope.'
        : 'A live preview is connected.';
  }

  setFieldVisibility(document.getElementById('context-pack-preview-field-schema-version'), preview.schemaVersion ?? 'rck.context_pack.v0');
  setTextField(contextPackPreviewSchemaVersionEl, preview.schemaVersion ?? 'rck.context_pack.v0', '');

  setFieldVisibility(document.getElementById('context-pack-preview-field-context-title'), preview.contextPackTitle ?? preview.title);
  setTextField(contextPackPreviewContextTitleEl, preview.contextPackTitle ?? preview.title, '');

  const referenceText = `${preview.reference?.contextPackId ?? preview.contextPackId} · ${preview.reference?.contextPackHash ?? preview.contextPackHash}`;
  setFieldVisibility(document.getElementById('context-pack-preview-field-reference'), referenceText);
  setTextField(contextPackPreviewReferenceEl, referenceText, '');

  const traceHashText = formatContextPackPreviewList(preview.sourceTraceSliceHashes);
  setFieldVisibility(document.getElementById('context-pack-preview-field-trace-hashes'), traceHashText);
  setTextField(contextPackPreviewTraceHashesEl, traceHashText, '');

  const visibleSectionsText = Array.isArray(preview.sectionsVisible)
    ? preview.sectionsVisible
        .filter((section) => section?.visible !== false)
        .map((section) => `${section.title}: ${section.summary}`)
        .join(' · ')
    : '';
  setFieldVisibility(document.getElementById('context-pack-preview-field-sections'), visibleSectionsText);
  setTextField(contextPackPreviewSectionsEl, visibleSectionsText, '');

  setFieldVisibility(document.getElementById('context-pack-preview-field-token-cost'), preview.estimatedTokenCost);
  setTextField(contextPackPreviewTokenCostEl, preview.estimatedTokenCost === null ? '' : String(preview.estimatedTokenCost), '');

  const warningsText = formatContextPackPreviewList(preview.warnings);
  setFieldVisibility(document.getElementById('context-pack-preview-field-warnings'), warningsText);
  setTextField(contextPackPreviewWarningsEl, warningsText, '');

  const constraintsText = formatContextPackPreviewList(preview.constraints);
  setFieldVisibility(document.getElementById('context-pack-preview-field-constraints'), constraintsText);
  setTextField(contextPackPreviewConstraintsEl, constraintsText, '');

  const provenance = preview.provenanceSummary ?? {};
  const provenanceText = [
    provenance.sourceDocument,
    provenance.contractDocument,
    provenance.schemaDocument,
    provenance.publishedCommit,
    provenance.mode,
  ]
    .filter(Boolean)
    .join(' · ');
  setFieldVisibility(document.getElementById('context-pack-preview-field-provenance'), provenanceText);
  setTextField(contextPackPreviewProvenanceEl, provenanceText, '');

  const scopeDerivationText = isLoadedPreview
    ? 'Loaded manually from JSON. Not generated automatically by RufusChat in this phase.'
    : activeContextPackGenerationRequest?.requestId
      ? `Approved locally from ${activeContextScopeSuggestion.suggestionId}. Preview is derived from the approved-scope generation request ${activeContextPackGenerationRequest.requestId}.`
      : `Approved locally from ${activeContextScopeSuggestion.suggestionId}. Preview was not generated from that scope.`;
  setFieldVisibility(document.getElementById('context-pack-preview-field-scope-derivation'), scopeDerivationText);
  setTextField(contextPackPreviewScopeDerivationEl, scopeDerivationText, '');

  setFieldVisibility(document.getElementById('context-pack-preview-field-exact-text'), preview.exactTextToInject);
  setTextField(contextPackPreviewExactTextEl, preview.exactTextToInject, '');

  const approvalStatusText = isLoadedPreview
    ? 'Loaded JSON / preview only'
    : 'Approved placeholder';
  setFieldVisibility(document.getElementById('context-pack-preview-field-approval-status'), approvalStatusText);
  setTextField(contextPackPreviewApprovalStatusEl, approvalStatusText, '');

  if (contextPackPreviewLoadMessageEl) {
    contextPackPreviewLoadMessageEl.textContent = isLoadedPreview
      ? 'Manual/dev-safe load only. RufusChat did not generate this ContextPack automatically.'
      : 'Paste a ContextPack JSON object here, then load a preview.';
  }

  if (contextPackPreviewJsonEl && !contextPackPreviewJsonEl.value.trim() && !isLoadedPreview) {
    contextPackPreviewJsonEl.placeholder = 'Paste ContextPack JSON here. Manual/dev-safe load only.';
  }

  if (contextPackPreviewLoadButton) {
    contextPackPreviewLoadButton.disabled = false;
    contextPackPreviewLoadButton.textContent = 'Load preview';
  }

  if (contextPackPreviewConfirmButton) {
    contextPackPreviewConfirmButton.disabled = true;
    contextPackPreviewConfirmButton.textContent = 'Confirm injection (disabled)';
    contextPackPreviewConfirmButton.title = preview.injectionPolicy?.reason ?? 'Not available in this phase.';
  }
}

async function loadContextPackPreview() {
  try {
    const response = await fetch(contextPackPreviewEndpoint, { headers: { Accept: 'application/json' } });
    if (!response.ok) {
      throw new Error(`Preview request failed with ${response.status}`);
    }

    const data = await response.json();
    activeContextPackPreview = normalizeContextPackPreview(data.preview ?? data);
  } catch {
    activeContextPackPreview = normalizeContextPackPreview({
      ...createFallbackContextPackPreview(),
      warnings: [
        'Placeholder / dev-only preview.',
        'ContextPack preview endpoint is unavailable.',
        'Using local fallback mock only.',
      ],
    });
  }

  renderContextPackPreviewPanel();
}

async function loadLoadedContextPackPreviewFromJson() {
  if (!contextPackPreviewJsonEl) {
    return null;
  }

  const rawJson = contextPackPreviewJsonEl.value.trim();
  if (!rawJson) {
    if (contextPackPreviewLoadMessageEl) {
      contextPackPreviewLoadMessageEl.textContent = 'Paste a ContextPack JSON object first.';
    }
    return null;
  }

  if (rawJson.length > 200000 && contextPackPreviewLoadMessageEl) {
    contextPackPreviewLoadMessageEl.textContent = 'The pasted JSON is large; RufusChat will still try to load it as a preview-only payload.';
  }

  if (contextPackPreviewLoadButton) {
    contextPackPreviewLoadButton.disabled = true;
    contextPackPreviewLoadButton.textContent = 'Loading preview...';
  }

  try {
    const data = await postJson(contextPackLoadPreviewEndpoint, { contextPackJson: rawJson });
    if (!data || data.ok !== true) {
      const message = typeof data?.error?.message === 'string' ? data.error.message : 'ContextPack load request failed.';
      const issuesText = Array.isArray(data?.issues) && data.issues.length > 0 ? ` ${data.issues.join(' · ')}` : '';
      throw new Error(`${message}${issuesText}`.trim());
    }

    activeLoadedContextPackPreview = normalizeContextPackPreview(data.preview ?? data);
    if (contextPackPreviewLoadMessageEl) {
      contextPackPreviewLoadMessageEl.textContent = 'Loaded manually from JSON. RufusChat did not generate this ContextPack automatically.';
    }
    renderContextPackPreviewPanel();
    return data;
  } catch (error) {
    if (contextPackPreviewLoadMessageEl) {
      contextPackPreviewLoadMessageEl.textContent = error instanceof Error
        ? error.message
        : 'ContextPack JSON load failed.';
    }
    return null;
  } finally {
    if (contextPackPreviewLoadButton) {
      contextPackPreviewLoadButton.disabled = false;
      contextPackPreviewLoadButton.textContent = 'Load preview';
    }
  }
}

function isContextPackCandidateMessage(message) {
  return getMessageContextPackId(message) !== null && isContextPackCandidateContent(message?.content);
}

function createContextPackCandidateControls(chatId, contextPackId, status) {
  void chatId;
  void contextPackId;
  const container = document.createElement('div');
  container.className = 'context-pack-card__actions';

  if (status === 'injected' || status === 'cancelled') {
    const statusChip = document.createElement('span');
    statusChip.className = `context-pack-card__status context-pack-card__status--${status}`;
    statusChip.textContent = status === 'injected' ? 'Injected' : 'Cancelled';
    container.appendChild(statusChip);
    return container;
  }

  const placeholderButton = document.createElement('button');
  placeholderButton.type = 'button';
  placeholderButton.className = 'button button--primary button--compact context-pack-card__action';
  placeholderButton.textContent = 'Not available in this phase';
  placeholderButton.disabled = true;
  placeholderButton.title = 'Attach / confirm injection is not available in this phase.';

  const cancelButton = document.createElement('button');
  cancelButton.type = 'button';
  cancelButton.className = 'button button--ghost button--compact context-pack-card__action';
  cancelButton.textContent = 'Not available in this phase';
  cancelButton.disabled = true;
  cancelButton.title = 'ContextPack injection is placeholder-only in this phase.';

  container.append(placeholderButton, cancelButton);
  return container;
}

function promptForName(message, defaultValue = '') {
  const response = window.prompt(message, defaultValue);
  if (response === null) {
    return null;
  }

  const value = response.trim();
  return value || null;
}

function normalizeRepositoryPath(value) {
  return typeof value === 'string' ? value.trim() : '';
}

function truncateText(value, maxLength = 48) {
  if (typeof value !== 'string' || value.length <= maxLength) {
    return value;
  }

  if (maxLength <= 1) {
    return '…';
  }

  return `${value.slice(0, maxLength - 1).trimEnd()}…`;
}

function formatProjectRepositoryPath(path) {
  if (typeof path !== 'string' || path.trim().length === 0) {
    return '';
  }

  return `repo · ${truncateText(path.trim(), 42)}`;
}

function formatSimpleDate(iso) {
  if (typeof iso !== 'string' || iso.length === 0) {
    return '—';
  }

  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return '—';
  }

  const now = new Date();
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const startOfTarget = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const dayDiff = Math.round((startOfToday.getTime() - startOfTarget.getTime()) / 86400000);

  if (dayDiff === 0) {
    return 'Today';
  }

  if (dayDiff === 1) {
    return 'Yesterday';
  }

  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
  }).format(date);
}

function formatMessageCount(count) {
  return count === 1 ? '1 message' : `${count} messages`;
}

function createMessage(role, text, variant = 'normal', overrides = {}) {
  const timestamp = overrides.createdAt ?? nowIso();
  const content = typeof overrides.content === 'string' ? overrides.content : text;
  const messageVariant = overrides.variant ?? variant;

  return {
    id: overrides.id ?? makeId('message'),
    role,
    text,
    content,
    variant: messageVariant,
    kind: overrides.kind ?? (messageVariant === 'error' ? 'error' : 'normal'),
    createdAt: timestamp,
    links: overrides.links ?? null,
  };
}

function createChat(title, messages = [], overrides = {}) {
  const timestamp = overrides.createdAt ?? nowIso();
  const chatId = overrides.id ?? makeId('chat');
  const projectId = overrides.projectId ?? null;

  return {
    ...overrides,
    id: chatId,
    projectId,
    title,
    kind: overrides.kind ?? 'normal',
    messages: messages.map((message) =>
      createMessage(message.role, message.text ?? message.content ?? '', message.variant ?? 'normal', {
        ...message,
        projectId,
      }),
    ),
    createdAt: timestamp,
    updatedAt: overrides.updatedAt ?? timestamp,
    memoryStatus: overrides.memoryStatus ?? memoryPlaceholder.memoryStatus,
    semanticSummaryStatus: overrides.semanticSummaryStatus ?? memoryPlaceholder.semanticSummaryStatus,
    semanticSummaryPreview: overrides.semanticSummaryPreview ?? memoryPlaceholder.semanticSummaryPreview,
    linkedRckTraceStatus: overrides.linkedRckTraceStatus ?? memoryPlaceholder.linkedRckTraceStatus,
    linkedRckTraceId: overrides.linkedRckTraceId ?? memoryPlaceholder.linkedRckTraceId,
    linkedRckTrace: overrides.linkedRckTrace ? { ...overrides.linkedRckTrace } : { ...tracePlaceholder },
    injections: sanitizeChatInjectionHistory(overrides.injections ?? []),
    checkpoints: sanitizeChatCheckpointHistory(overrides.checkpoints ?? []),
  };
}

function createProject(name, chats = [], overrides = {}) {
  const timestamp = overrides.createdAt ?? nowIso();
  const projectId = overrides.id ?? makeId('project');
  const repositoryPath = normalizeRepositoryPath(overrides.repositoryPath ?? overrides.repoPath ?? '');

  return {
    ...overrides,
    id: projectId,
    name: typeof name === 'string' ? name.trim() : name,
    repositoryPath,
    chats: chats.map((chat) =>
      createChat(chat.title ?? 'New chat', chat.messages ?? [], {
        ...chat,
        id: chat.id,
        projectId,
      }),
    ),
    createdAt: timestamp,
    updatedAt: overrides.updatedAt ?? timestamp,
  };
}

function createInitialProjects() {
  return [
    createProject('PI Agent', [
      createChat('Branch · RufusChat Fase 10', [createMessage('assistant', localIntroMessage)]),
      createChat('RufusChat Fase 10', [createMessage('assistant', localIntroMessage)]),
      createChat('RufusChat Fase 9', [createMessage('assistant', localIntroMessage)]),
      createChat('Adjust · RufusChat UX', [createMessage('assistant', localIntroMessage)]),
      createChat('RufusChat Fase 8', [createMessage('assistant', localIntroMessage)]),
    ]),
    createProject('CivilPlan', [createChat('CivilPlan', [createMessage('assistant', localIntroMessage)])]),
    createProject('Wise', [createChat('Wise', [createMessage('assistant', localIntroMessage)])]),
    createProject('CC Analysis', [createChat('CC Analysis', [createMessage('assistant', localIntroMessage)])]),
    createProject('Hermes WSL2', [createChat('Hermes WSL2', [createMessage('assistant', localIntroMessage)])]),
  ];
}

let state = {
  version: '0',
  projects: createInitialProjects(),
  currentProjectId: null,
  currentChatId: null,
  createdAt: nowIso(),
  updatedAt: nowIso(),
};

state.currentProjectId = state.projects[0]?.id ?? null;
state.currentChatId = state.projects[0]?.chats[0]?.id ?? null;

let productStateBannerText = idleStatusText;
let productStateBannerResetTimer = null;
let productStateHydrating = true;
let productStateLocalDirty = false;
let productStateSaveTimer = null;
let productStateSaveInFlight = false;
let productStateSaveQueued = false;
let productStateLastSavedSnapshot = null;

function syncStatusPill() {
  if (statusPill) {
    statusPill.textContent = productStateBannerText;
  }
}

function setProductStateBanner(text, { resetAfterMs = null } = {}) {
  productStateBannerText = text;
  syncStatusPill();

  if (productStateBannerResetTimer) {
    clearTimeout(productStateBannerResetTimer);
    productStateBannerResetTimer = null;
  }

  if (resetAfterMs !== null) {
    productStateBannerResetTimer = setTimeout(() => {
      productStateBannerResetTimer = null;
      productStateBannerText = idleStatusText;
      syncStatusPill();
    }, resetAfterMs);
  }
}

function touchRootState({ dirty = true } = {}) {
  state.updatedAt = nowIso();
  if (dirty) {
    productStateLocalDirty = true;
  }
}

function touchProject(project, { dirty = true } = {}) {
  if (project) {
    project.updatedAt = nowIso();
  }

  if (dirty) {
    productStateLocalDirty = true;
  }
}

function touchChat(chat, { dirty = true } = {}) {
  if (chat) {
    chat.updatedAt = nowIso();
  }

  if (dirty) {
    productStateLocalDirty = true;
  }
}

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function validateImportedProductState(candidate) {
  if (!isPlainObject(candidate)) {
    return productStateImportErrorText;
  }

  if (typeof candidate.version !== 'string') {
    return productStateImportErrorText;
  }

  if (!Array.isArray(candidate.projects)) {
    return productStateImportErrorText;
  }

  return null;
}

function getProductStateExportFilename() {
  return `rufuschat-product-state-${new Date().toISOString().slice(0, 10)}.json`;
}

function downloadProductStateJson(filename, payload) {
  const blob = new Blob([payload], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.rel = 'noopener';
  anchor.style.display = 'none';
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

async function putProductStatePayload(payload) {
  const response = await fetch(productStateEndpoint, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(payload),
  });
  const data = await readJsonResponse(response);

  if (!response.ok) {
    throw new Error(data?.message ?? data?.error ?? saveFailureText);
  }

  return data;
}

async function syncProductStateFromPayload(payload) {
  setProductStateBanner(savingStatusText);

  const response = await putProductStatePayload(payload);
  const savedState = response?.state ?? payload;
  replaceStateFromProductState(savedState);
  const selectionAdjusted = applySelectionFallback({ persist: false });
  productStateLocalDirty = false;
  productStateLastSavedSnapshot = JSON.stringify(savedState);
  render();
  renderSlashMenu();

  if (selectionAdjusted) {
    markProductStateChanged();
    clearTimeout(productStateSaveTimer);
    productStateSaveTimer = null;
    await saveProductStateNow();
    return response;
  }

  setProductStateBanner(savedStatusText, { resetAfterMs: 1200 });
  return response;
}

async function exportProductState() {
  try {
    const payload = buildProductStatePayload();
    downloadProductStateJson(getProductStateExportFilename(), `${JSON.stringify(payload, null, 2)}\n`);
    setProductStateBanner(savedStatusText, { resetAfterMs: 1200 });
  } catch {
    setProductStateBanner(productStateExportErrorText, { resetAfterMs: 3000 });
  }
}

async function importProductStateFromFile(file) {
  if (!file) {
    return;
  }

  try {
    const text = await file.text();
    const parsed = JSON.parse(text);
    const validationError = validateImportedProductState(parsed);

    if (validationError) {
      setProductStateBanner(validationError, { resetAfterMs: 3000 });
      return;
    }

    if (!(await confirmAction('Import local data and replace the current session?'))) {
      return;
    }

    await syncProductStateFromPayload(parsed);
  } catch {
    setProductStateBanner(productStateImportErrorText, { resetAfterMs: 3000 });
  } finally {
    if (productStateImportInput) {
      productStateImportInput.value = '';
    }
  }
}

async function resetProductState() {
  if (!(await confirmAction('Reset local data to a safe starter session?'))) {
    return;
  }

  const typedReset = window.prompt('Type RESET to continue', '');
  if (typedReset !== 'RESET') {
    setProductStateBanner(productStateResetErrorText, { resetAfterMs: 3000 });
    return;
  }

  try {
    await syncProductStateFromPayload(createResetProductStatePayload());
  } catch {
    setProductStateBanner(productStateResetErrorText, { resetAfterMs: 3000 });
  }
}

function sanitizeNullableString(value) {
  return typeof value === 'string' ? value : null;
}

function sanitizeInjectionHistoryItem(item) {
  if (!isPlainObject(item)) {
    return null;
  }

  const contextPackId = sanitizeNullableString(item.contextPackId);
  if (!contextPackId) {
    return null;
  }

  const status = item.status === 'candidate' || item.status === 'injected' || item.status === 'cancelled' || item.status === 'expired' ? item.status : 'candidate';
  const sourceKind = item.sourceKind === 'placeholder' || item.sourceKind === 'manual' || item.sourceKind === 'rck' || item.sourceKind === 'future' ? item.sourceKind : 'placeholder';

  return {
    contextPackId,
    status,
    title: typeof item.title === 'string' ? item.title : contextPackInjectionHistoryTitle,
    summary: typeof item.summary === 'string' ? item.summary : getContextPackCandidateSummaryText(),
    createdAt: typeof item.createdAt === 'string' ? item.createdAt : nowIso(),
    updatedAt: typeof item.updatedAt === 'string' ? item.updatedAt : typeof item.createdAt === 'string' ? item.createdAt : nowIso(),
    injectedAt: item.injectedAt === null || typeof item.injectedAt === 'string' ? item.injectedAt : null,
    cancelledAt: item.cancelledAt === null || typeof item.cancelledAt === 'string' ? item.cancelledAt : null,
    candidateMessageId: item.candidateMessageId === null || typeof item.candidateMessageId === 'string' ? item.candidateMessageId : null,
    resultMessageId: item.resultMessageId === null || typeof item.resultMessageId === 'string' ? item.resultMessageId : null,
    sourceKind,
    safeMetadata: isPlainObject(item.safeMetadata) ? { ...item.safeMetadata } : null,
  };
}

function sanitizeChatInjectionHistory(items) {
  if (!Array.isArray(items)) {
    return [];
  }

  return items.map((item) => sanitizeInjectionHistoryItem(item)).filter(Boolean);
}

function sanitizeCheckpointHistoryItem(item) {
  if (!isPlainObject(item)) {
    return null;
  }

  const checkpointId = sanitizeNullableString(item.checkpointId);
  if (!checkpointId) {
    return null;
  }

  const status = item.status === 'created' || item.status === 'superseded' || item.status === 'archived' ? item.status : 'created';
  const sourceKind = item.sourceKind === 'product' || item.sourceKind === 'manual' || item.sourceKind === 'future-rck' ? item.sourceKind : 'product';

  return {
    checkpointId,
    status,
    label: typeof item.label === 'string' ? item.label : 'Untitled checkpoint',
    summary: typeof item.summary === 'string' ? item.summary : 'Product checkpoint only. No internal anchor was created. No raw evidence stored.',
    createdAt: typeof item.createdAt === 'string' ? item.createdAt : nowIso(),
    updatedAt: typeof item.updatedAt === 'string' ? item.updatedAt : typeof item.createdAt === 'string' ? item.createdAt : nowIso(),
    sourceMessageId: item.sourceMessageId === null || typeof item.sourceMessageId === 'string' ? item.sourceMessageId : null,
    resultMessageId: item.resultMessageId === null || typeof item.resultMessageId === 'string' ? item.resultMessageId : null,
    sourceKind,
    safeMetadata: isPlainObject(item.safeMetadata) ? { ...item.safeMetadata } : null,
  };
}

function sanitizeChatCheckpointHistory(items) {
  if (!Array.isArray(items)) {
    return [];
  }

  return items.map((item) => sanitizeCheckpointHistoryItem(item)).filter(Boolean);
}

function sanitizeMessageLinks(links) {
  if (!isPlainObject(links)) {
    return null;
  }

  const output = {};
  let hasAny = false;

  for (const field of ['rckTraceId', 'contextPackId', 'checkpointId']) {
    if (links[field] === undefined) {
      continue;
    }

    if (links[field] === null || typeof links[field] === 'string') {
      output[field] = links[field];
      hasAny = true;
    }
  }

  return hasAny ? output : null;
}

function normalizeChatKind(kind) {
  return kind === 'phase' || kind === 'decision' || kind === 'debug' ? kind : 'normal';
}

function normalizeMessageKind(kind, variant) {
  if (kind === 'command' || kind === 'command-result' || kind === 'error' || kind === 'placeholder') {
    return kind;
  }

  return variant === 'error' ? 'error' : 'normal';
}

function uiMessageFromProductMessage(message) {
  const content = typeof message?.content === 'string' ? message.content : typeof message?.text === 'string' ? message.text : '';
  const kind = normalizeMessageKind(message?.kind, message?.variant);
  const variant = kind === 'error' ? 'error' : typeof message?.variant === 'string' ? message.variant : 'normal';

  return createMessage(message?.role ?? 'user', content, variant, {
    id: typeof message?.id === 'string' ? message.id : undefined,
    content,
    kind,
    createdAt: typeof message?.createdAt === 'string' ? message.createdAt : undefined,
    links: sanitizeMessageLinks(message?.links),
  });
}

function uiChatFromProductChat(chat, projectIdFallback) {
  const now = nowIso();
  const linkedRckTrace = isPlainObject(chat?.linkedRckTrace)
    ? { ...tracePlaceholder, ...chat.linkedRckTrace }
    : { ...tracePlaceholder };

  return createChat(chat?.title ?? 'New chat', Array.isArray(chat?.messages) ? chat.messages.map(uiMessageFromProductMessage) : [], {
    id: typeof chat?.id === 'string' ? chat.id : undefined,
    projectId: typeof chat?.projectId === 'string' ? chat.projectId : projectIdFallback,
    kind: normalizeChatKind(chat?.kind),
    createdAt: typeof chat?.createdAt === 'string' ? chat.createdAt : now,
    updatedAt: typeof chat?.updatedAt === 'string' ? chat.updatedAt : now,
    memoryStatus: typeof chat?.memoryStatus === 'string' ? chat.memoryStatus : memoryPlaceholder.memoryStatus,
    semanticSummaryStatus: typeof chat?.semanticSummaryStatus === 'string' ? chat.semanticSummaryStatus : memoryPlaceholder.semanticSummaryStatus,
    semanticSummaryPreview: typeof chat?.semanticSummaryPreview === 'string' ? chat.semanticSummaryPreview : null,
    linkedRckTraceStatus: typeof chat?.linkedRckTraceStatus === 'string' ? chat.linkedRckTraceStatus : linkedRckTrace.status,
    linkedRckTraceId: sanitizeNullableString(chat?.linkedRckTraceId),
    linkedRckTrace,
    injections: sanitizeChatInjectionHistory(chat?.injections),
    checkpoints: sanitizeChatCheckpointHistory(chat?.checkpoints),
  });
}

function uiProjectFromProductProject(project) {
  const now = nowIso();
  const projectId = typeof project?.id === 'string' ? project.id : makeId('project');
  const repositoryPath = normalizeRepositoryPath(project?.repositoryPath ?? project?.repoPath ?? '');

  return createProject(project?.name ?? 'New project', Array.isArray(project?.chats) ? project.chats.map((chat) => uiChatFromProductChat(chat, projectId)) : [], {
    id: projectId,
    repositoryPath,
    createdAt: typeof project?.createdAt === 'string' ? project.createdAt : now,
    updatedAt: typeof project?.updatedAt === 'string' ? project.updatedAt : now,
  });
}

function replaceStateFromProductState(productState) {
  const now = nowIso();
  const projects = Array.isArray(productState?.projects)
    ? productState.projects.map((project) => uiProjectFromProductProject(project))
    : [];

  state = {
    version: typeof productState?.version === 'string' ? productState.version : '0',
    projects,
    currentProjectId: typeof productState?.currentProjectId === 'string' ? productState.currentProjectId : null,
    currentChatId: typeof productState?.currentChatId === 'string' ? productState.currentChatId : null,
    createdAt: typeof productState?.createdAt === 'string' ? productState.createdAt : now,
    updatedAt: typeof productState?.updatedAt === 'string' ? productState.updatedAt : now,
  };
}

function buildProductStatePayload() {
  const now = nowIso();

  return {
    version: typeof state.version === 'string' ? state.version : '0',
    projects: state.projects.map((project) => ({
      id: project.id,
      name: project.name,
      repositoryPath: normalizeRepositoryPath(project.repositoryPath ?? project.repoPath ?? ''),
      chats: project.chats.map((chat) => ({
        id: chat.id,
        projectId: chat.projectId ?? project.id,
        title: chat.title,
        kind: normalizeChatKind(chat.kind),
        messages: chat.messages.map((message) => ({
          id: message.id ?? makeId('message'),
          role: message.role,
          content: typeof message.content === 'string' ? message.content : typeof message.text === 'string' ? message.text : '',
          createdAt: typeof message.createdAt === 'string' ? message.createdAt : now,
          ...(normalizeMessageKind(message.kind, message.variant) === 'normal' ? {} : { kind: normalizeMessageKind(message.kind, message.variant) }),
          ...(sanitizeMessageLinks(message.links) ? { links: sanitizeMessageLinks(message.links) } : {}),
        })),
        createdAt: typeof chat.createdAt === 'string' ? chat.createdAt : now,
        updatedAt: typeof chat.updatedAt === 'string' ? chat.updatedAt : now,
        memoryStatus: typeof chat.memoryStatus === 'string' ? chat.memoryStatus : memoryPlaceholder.memoryStatus,
        semanticSummaryStatus: typeof chat.semanticSummaryStatus === 'string' ? chat.semanticSummaryStatus : memoryPlaceholder.semanticSummaryStatus,
        semanticSummaryPreview: typeof chat.semanticSummaryPreview === 'string' ? chat.semanticSummaryPreview : null,
        linkedRckTraceStatus: typeof chat.linkedRckTraceStatus === 'string' ? chat.linkedRckTraceStatus : memoryPlaceholder.linkedRckTraceStatus,
        linkedRckTrace: isPlainObject(chat.linkedRckTrace)
          ? {
              status: typeof chat.linkedRckTrace.status === 'string' ? chat.linkedRckTrace.status : tracePlaceholder.status,
              traceId: sanitizeNullableString(chat.linkedRckTrace.traceId),
              provider: chat.linkedRckTrace.provider ?? tracePlaceholder.provider,
              futureProvider: chat.linkedRckTrace.futureProvider ?? tracePlaceholder.futureProvider,
              mode: chat.linkedRckTrace.mode ?? tracePlaceholder.mode,
            }
          : { ...tracePlaceholder },
        injections: sanitizeChatInjectionHistory(chat.injections),
        checkpoints: sanitizeChatCheckpointHistory(chat.checkpoints),
      })),
      createdAt: typeof project.createdAt === 'string' ? project.createdAt : now,
      updatedAt: typeof project.updatedAt === 'string' ? project.updatedAt : now,
    })),
    currentProjectId: typeof state.currentProjectId === 'string' ? state.currentProjectId : null,
    currentChatId: typeof state.currentChatId === 'string' ? state.currentChatId : null,
    createdAt: typeof state.createdAt === 'string' ? state.createdAt : now,
    updatedAt: typeof state.updatedAt === 'string' ? state.updatedAt : now,
  };
}

function getProductStateSnapshot() {
  return JSON.stringify(buildProductStatePayload());
}

function markProductStateChanged() {
  touchRootState();
  scheduleProductStateSave();
}

function scheduleProductStateSave({ immediate = false } = {}) {
  if (productStateHydrating) {
    productStateSaveQueued = true;
    return;
  }

  if (immediate) {
    clearTimeout(productStateSaveTimer);
    productStateSaveTimer = null;
    void saveProductStateNow();
    return;
  }

  clearTimeout(productStateSaveTimer);
  productStateSaveTimer = setTimeout(() => {
    productStateSaveTimer = null;
    void saveProductStateNow();
  }, 500);
}

async function saveProductStateNow() {
  if (productStateHydrating) {
    productStateSaveQueued = true;
    return;
  }

  const snapshot = getProductStateSnapshot();
  if (snapshot === productStateLastSavedSnapshot) {
    return;
  }

  if (productStateSaveInFlight) {
    productStateSaveQueued = true;
    return;
  }

  productStateSaveInFlight = true;
  setProductStateBanner(savingStatusText);

  try {
    const response = await fetch(productStateEndpoint, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: snapshot,
    });
    const data = await readJsonResponse(response);

    if (!response.ok) {
      throw new Error(data?.message ?? data?.error ?? saveFailureText);
    }

    if (typeof data?.state?.updatedAt === 'string') {
      state.updatedAt = data.state.updatedAt;
    }

    productStateLocalDirty = false;
    productStateLastSavedSnapshot = JSON.stringify(data?.state ?? buildProductStatePayload());
    setProductStateBanner(savedStatusText, { resetAfterMs: 1200 });
  } catch {
    setProductStateBanner(saveFailureText);
  } finally {
    productStateSaveInFlight = false;

    if (productStateSaveQueued) {
      productStateSaveQueued = false;
      scheduleProductStateSave({ immediate: true });
    }
  }
}

function applySelectionFallback({ persist = true } = {}) {
  if (state.projects.length === 0) {
    const project = createDefaultProject();
    state.projects.push(project);
    state.currentProjectId = project.id;
    state.currentChatId = project.chats[0]?.id ?? null;
    touchRootState({ dirty: persist });
    if (persist) {
      markProductStateChanged();
    }
    return true;
  }

  const project = getProjectById(state.currentProjectId) ?? state.projects[0];
  if (!project) {
    return false;
  }

  let chat = getChatById(state.currentChatId);
  if (!chat || !project.chats.some((item) => item.id === chat?.id)) {
    chat = getChatsForProject(project)[0] ?? null;
  }

  let changed = false;
  if (!chat) {
    chat = createEmptyChat('New chat', project.id);
    project.chats.push(chat);
    touchProject(project);
    changed = true;
  }

  if (state.currentProjectId !== project.id) {
    state.currentProjectId = project.id;
    changed = true;
  }

  if (state.currentChatId !== chat.id) {
    state.currentChatId = chat.id;
    changed = true;
  }

  if (changed) {
    if (persist) {
      markProductStateChanged();
    } else {
      touchRootState({ dirty: false });
    }
  }

  return changed;
}

async function hydrateProductState() {
  setProductStateBanner(loadingStatusText);

  try {
    const data = await getJson(productStateEndpoint);
    if (productStateLocalDirty) {
      setProductStateBanner(idleStatusText);
      return;
    }

    replaceStateFromProductState(data?.state);
    const selectionAdjusted = applySelectionFallback({ persist: false });
    productStateLocalDirty = false;
    productStateLastSavedSnapshot = getProductStateSnapshot();
    productStateHydrating = false;
    setProductStateBanner(idleStatusText);
    render();
    renderSlashMenu();
    if (selectionAdjusted) {
      markProductStateChanged();
    }
  } catch {
    productStateHydrating = false;
    setProductStateBanner(hydrateFailureText);
    render();
    renderSlashMenu();
  } finally {
    productStateHydrating = false;
    if (productStateSaveQueued || productStateLocalDirty) {
      productStateSaveQueued = false;
      scheduleProductStateSave({ immediate: true });
    }
  }
}

async function hydrateRuntimeStatus() {
  try {
    const data = await getJson(runtimeStatusEndpoint);
    const nextRuntimeStatus = normalizeRuntimeStatus(data);

    if (nextRuntimeStatus) {
      runtimeStatus = nextRuntimeStatus;
      renderHeader();
    }
  } catch {
    runtimeStatus = createFallbackRuntimeStatus();
  }
}

function getProjectById(projectId) {
  return state.projects.find((project) => project.id === projectId) ?? null;
}

function getChatById(chatId) {
  for (const project of state.projects) {
    const chat = project.chats.find((item) => item.id === chatId);
    if (chat) {
      return chat;
    }
  }

  return null;
}

function getCurrentProject() {
  return getProjectById(state.currentProjectId);
}

function getCurrentChat() {
  return getChatById(state.currentChatId);
}

function getProjectIndexById(projectId) {
  return state.projects.findIndex((project) => project.id === projectId);
}

function getProjectByChatId(chatId) {
  return state.projects.find((project) => project.chats.some((chat) => chat.id === chatId)) ?? null;
}

function getChatsForProject(project) {
  if (!project) {
    return [];
  }

  return [...project.chats].sort((left, right) => {
    const leftUpdatedAt = typeof left.updatedAt === 'string' ? left.updatedAt : '';
    const rightUpdatedAt = typeof right.updatedAt === 'string' ? right.updatedAt : '';
    if (leftUpdatedAt !== rightUpdatedAt) {
      return rightUpdatedAt.localeCompare(leftUpdatedAt);
    }

    const leftCreatedAt = typeof left.createdAt === 'string' ? left.createdAt : '';
    const rightCreatedAt = typeof right.createdAt === 'string' ? right.createdAt : '';
    return rightCreatedAt.localeCompare(leftCreatedAt);
  });
}

function createEmptyChat(title = 'New chat', projectId = null) {
  return createChat(title, [], { projectId });
}

function createDefaultChat() {
  return createEmptyChat('New chat');
}

function createDefaultProject() {
  return createProject('RufusChat', [createDefaultChat()]);
}

function createResetProductStatePayload() {
  const project = createDefaultProject();
  const timestamp = nowIso();

  return {
    version: typeof state.version === 'string' ? state.version : '0',
    projects: [project],
    currentProjectId: project.id,
    currentChatId: project.chats[0]?.id ?? null,
    createdAt: timestamp,
    updatedAt: timestamp,
  };
}

function ensureProjectListHasItems() {
  if (state.projects.length === 0) {
    const project = createDefaultProject();
    state.projects.push(project);
    return project;
  }

  return null;
}

function ensureSelection({ persist = true } = {}) {
  return applySelectionFallback({ persist });
}

function getProjectByIdOrDefault(projectId) {
  return getProjectById(projectId) ?? state.projects[0] ?? null;
}

function getHeaderContextSummary(chat) {
  const injectionSummary = getChatInjectionSummary(chat);
  const checkpointSummary = getChatCheckpointSummary(chat);

  if (injectionSummary.total === 0 && checkpointSummary.total === 0) {
    return 'Context off';
  }

  if (injectionSummary.total > 0 && checkpointSummary.total === 0) {
    return `${injectionSummary.total} context pack${injectionSummary.total === 1 ? '' : 's'}`;
  }

  if (checkpointSummary.total > 0 && injectionSummary.total === 0) {
    return `${checkpointSummary.total} checkpoint${checkpointSummary.total === 1 ? '' : 's'}`;
  }

  return `${injectionSummary.total} context pack${injectionSummary.total === 1 ? '' : 's'} · ${checkpointSummary.total} checkpoint${checkpointSummary.total === 1 ? '' : 's'}`;
}

function getLinkedRckTrace(chat) {
  return chat?.linkedRckTrace ?? {
    status: chat?.linkedRckTraceStatus ?? tracePlaceholder.status,
    traceId: chat?.linkedRckTraceId ?? tracePlaceholder.traceId,
    provider: tracePlaceholder.provider,
    futureProvider: tracePlaceholder.futureProvider,
    mode: tracePlaceholder.mode,
  };
}

function scrollChatToBottom() {
  requestAnimationFrame(() => {
    messagesEl.scrollTop = messagesEl.scrollHeight;
  });
}

function setBusy(isBusy, label = 'Running...') {
  const hasActiveChat = Boolean(getCurrentChat());
  const isDisabled = isBusy || !hasActiveChat;

  composerInput.disabled = isDisabled;
  sendButton.disabled = isDisabled;
  if (newProjectButton) {
    newProjectButton.disabled = isBusy;
  }
  if (productStateExportButton) {
    productStateExportButton.disabled = isBusy;
  }
  if (productStateImportButton) {
    productStateImportButton.disabled = isBusy;
  }
  if (productStateResetButton) {
    productStateResetButton.disabled = isBusy;
  }
  if (statusPill) {
    statusPill.textContent = isBusy ? label : productStateBannerText;
  }

  if (isBusy) {
    hideSlashMenu();
  }

  updateChatCompletionControls();
}

function renderHeader() {
  const project = getCurrentProject();
  const chat = getCurrentChat();
  const fallbackRuntimeStatus = createFallbackRuntimeStatus();
  const memoryStatus = runtimeStatus.memory ?? fallbackRuntimeStatus.memory;
  const contextStatus = runtimeStatus.context ?? fallbackRuntimeStatus.context;
  const traceStatus = runtimeStatus.trace ?? fallbackRuntimeStatus.trace;

  currentProjectEl.textContent = project?.name ?? '—';
  currentProjectEl.title = 'Current project';
  currentChatEl.textContent = chat?.title ?? '—';
  currentChatEl.title = 'Current chat';

  if (memoryStatusEl) {
    memoryStatusEl.textContent = memoryStatus.label;
    memoryStatusEl.classList.toggle('chat-header__chip--muted', memoryStatus.status === 'off');
    memoryStatusEl.classList.toggle('chat-header__chip--accent', memoryStatus.status !== 'off');
  }

  if (summaryStatusEl) {
    summaryStatusEl.textContent = contextStatus.label;
    summaryStatusEl.classList.toggle('chat-header__chip--muted', contextStatus.status === 'off');
    summaryStatusEl.classList.toggle('chat-header__chip--accent', contextStatus.status !== 'off');
  }

  if (rckTraceStatusEl) {
    rckTraceStatusEl.textContent = traceStatus.label;
    rckTraceStatusEl.classList.toggle('chat-header__chip--muted', traceStatus.status === 'not_linked');
    rckTraceStatusEl.classList.toggle('chat-header__chip--accent', traceStatus.status !== 'not_linked');
  }

  if (traceChipEl) {
    traceChipEl.hidden = true;
    traceChipEl.textContent = '';
    traceChipEl.removeAttribute('title');
    traceChipEl.removeAttribute('aria-label');
  }

  if (chatSessionShellTraceEl) {
    chatSessionShellTraceEl.hidden = true;
  }

  if (statusPill) {
    statusPill.textContent = 'Local session';
  }
}

function renderSidebar() {
  projectTreeEl.replaceChildren();

  if (state.projects.length === 0) {
    const emptyState = document.createElement('div');
    emptyState.className = 'empty-state';
    emptyState.textContent = 'No projects yet.';
    projectTreeEl.appendChild(emptyState);
    return;
  }

  for (const project of state.projects) {
    const projectChats = getChatsForProject(project);
    const section = document.createElement('section');
    section.className = 'project-group';
    if (project.id === state.currentProjectId) {
      section.classList.add('project-group--active');
    }

    const header = document.createElement('div');
    header.className = 'project-group__header';

    const titleButton = document.createElement('button');
    titleButton.type = 'button';
    titleButton.className = 'project-group__title';
    titleButton.dataset.action = 'select-project';
    titleButton.dataset.projectId = project.id;
    titleButton.setAttribute('aria-current', project.id === state.currentProjectId ? 'true' : 'false');

    const titleRow = document.createElement('span');
    titleRow.className = 'project-group__title-row';

    const titleText = document.createElement('span');
    titleText.className = 'project-group__title-text';
    titleText.textContent = project.name;
    titleRow.appendChild(titleText);

    titleButton.append(titleRow);

    const projectMeta = document.createElement('span');
    projectMeta.className = 'project-group__meta';
    projectMeta.textContent = formatProjectRepositoryPath(project.repositoryPath);
    projectMeta.title = typeof project.repositoryPath === 'string' ? project.repositoryPath : '';
    projectMeta.hidden = !projectMeta.textContent;

    titleButton.append(projectMeta);
    const projectMenuButton = document.createElement('button');
    projectMenuButton.type = 'button';
    projectMenuButton.className = 'project-group__menu';
    projectMenuButton.dataset.action = 'open-project-menu';
    projectMenuButton.dataset.projectId = project.id;
    projectMenuButton.textContent = '…';
    projectMenuButton.setAttribute('aria-label', `Project actions for ${project.name}`);

    if (
      activeContextMenu?.type === 'project' &&
      activeContextMenu.projectId === project.id &&
      activeContextMenu.anchorEl instanceof HTMLButtonElement
    ) {
      projectMenuButton.classList.add('is-context-menu-open');
    }

    header.append(titleButton, projectMenuButton);
    section.appendChild(header);

    const children = document.createElement('div');
    children.className = 'project-group__children';

    if (projectChats.length === 0) {
      const emptyState = document.createElement('div');
      emptyState.className = 'empty-state empty-state--sidebar';
      emptyState.textContent = 'No chats yet.';
      children.appendChild(emptyState);
    } else {
      for (const chat of projectChats) {
        const chatRow = document.createElement('div');
        chatRow.className = 'chat-item-row';

        const chatButton = document.createElement('button');
        chatButton.type = 'button';
        chatButton.className = 'chat-item';
        chatButton.dataset.action = 'select-chat';
        chatButton.dataset.projectId = project.id;
        chatButton.dataset.chatId = chat.id;
        chatButton.setAttribute('aria-current', project.id === state.currentProjectId && chat.id === state.currentChatId ? 'true' : 'false');

        const chatTitleRow = document.createElement('span');
        chatTitleRow.className = 'chat-item__title-row';

        const chatTitleText = document.createElement('span');
        chatTitleText.className = 'chat-item__title-text';
        chatTitleText.textContent = chat.title;
        chatTitleRow.appendChild(chatTitleText);

        const chatMeta = document.createElement('span');
        chatMeta.className = 'chat-item__meta';
        const messageCount = document.createElement('span');
        messageCount.className = 'chat-item__meta-item';
        messageCount.textContent = formatMessageCount(chat.messages.length);
        const updatedAt = document.createElement('span');
        updatedAt.className = 'chat-item__meta-item';
        updatedAt.textContent = formatSimpleDate(chat.updatedAt);
        chatMeta.append(messageCount, updatedAt);

        chatButton.append(chatTitleRow, chatMeta);

        if (project.id === state.currentProjectId && chat.id === state.currentChatId) {
          chatButton.classList.add('chat-item--active');
        }

        const chatMenuButton = document.createElement('button');
        chatMenuButton.type = 'button';
        chatMenuButton.className = 'chat-item__menu';
        chatMenuButton.dataset.action = 'open-chat-menu';
        chatMenuButton.dataset.projectId = project.id;
        chatMenuButton.dataset.chatId = chat.id;
        chatMenuButton.textContent = '…';
        chatMenuButton.setAttribute('aria-label', `Chat actions for ${chat.title}`);

        if (
          activeContextMenu?.type === 'chat' &&
          activeContextMenu.projectId === project.id &&
          activeContextMenu.chatId === chat.id &&
          activeContextMenu.anchorEl instanceof HTMLButtonElement
        ) {
          chatMenuButton.classList.add('is-context-menu-open');
        }

        chatRow.append(chatButton, chatMenuButton);
        children.appendChild(chatRow);
      }
    }

    section.appendChild(children);
    projectTreeEl.appendChild(section);
  }
}

let activeContextMenu = null;

function hideContextMenus() {
  if (activeContextMenu?.anchorEl instanceof HTMLElement) {
    activeContextMenu.anchorEl.classList.remove('is-context-menu-open');
  }

  activeContextMenu = null;

  for (const menuEl of [projectContextMenuEl, chatContextMenuEl]) {
    if (!menuEl) {
      continue;
    }

    menuEl.hidden = true;
    menuEl.replaceChildren();
    menuEl.style.left = '';
    menuEl.style.top = '';
    menuEl.style.visibility = '';
  }
}

function positionContextMenu(menuEl, anchorEl) {
  const anchorRect = anchorEl.getBoundingClientRect();
  const menuRect = menuEl.getBoundingClientRect();
  const viewportWidth = window.innerWidth;
  const viewportHeight = window.innerHeight;
  const margin = 12;

  let left = anchorRect.right - menuRect.width;
  let top = anchorRect.bottom + 8;

  if (left + menuRect.width > viewportWidth - margin) {
    left = viewportWidth - menuRect.width - margin;
  }

  if (left < margin) {
    left = margin;
  }

  if (top + menuRect.height > viewportHeight - margin) {
    top = anchorRect.top - menuRect.height - 8;
  }

  if (top < margin) {
    top = margin;
  }

  menuEl.style.left = `${left}px`;
  menuEl.style.top = `${top}px`;
  menuEl.style.visibility = 'visible';
}

function renderContextMenu(menuEl, items, anchorEl, context) {
  if (!menuEl) {
    return;
  }

  hideContextMenus();

  if (anchorEl instanceof HTMLElement) {
    anchorEl.classList.add('is-context-menu-open');
  }

  menuEl.replaceChildren();
  menuEl.hidden = false;
  menuEl.style.visibility = 'hidden';
  menuEl.dataset.contextType = context.type;

  for (const item of items) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = `context-menu__item${item.destructive ? ' context-menu__item--destructive' : ''}`;
    button.dataset.contextAction = item.action;
    button.textContent = item.label;
    menuEl.appendChild(button);
  }

  activeContextMenu = { type: context.type, projectId: context.projectId, chatId: context.chatId, menuEl, anchorEl };
  positionContextMenu(menuEl, anchorEl);
}

function openProjectContextMenu(projectId, anchorEl) {
  renderContextMenu(
    projectContextMenuEl,
    [
      { action: 'rename-project', label: 'Rename project' },
      { action: 'new-chat', label: 'New chat in project' },
      { action: 'delete-project', label: 'Delete project', destructive: true },
    ],
    anchorEl,
    { type: 'project', projectId },
  );
}

function openChatContextMenu(projectId, chatId, anchorEl) {
  renderContextMenu(
    chatContextMenuEl,
    [
      { action: 'rename-chat', label: 'Rename chat' },
      { action: 'clear-chat', label: 'Clear messages', destructive: true },
      { action: 'delete-chat', label: 'Delete chat', destructive: true },
    ],
    anchorEl,
    { type: 'chat', projectId, chatId },
  );
}

function createChatInProject(project, title = 'New chat', messages = [createMessage('assistant', newChatAssistantMessage)]) {
  const chat = createChat(title, messages, { projectId: project.id });
  project.chats.push(chat);
  touchProject(project);
  return chat;
}

function getUniqueProjectName(baseName = 'New project') {
  const existingNames = new Set(state.projects.map((project) => project.name));
  if (!existingNames.has(baseName)) {
    return baseName;
  }

  let suffix = 2;
  while (existingNames.has(`${baseName} ${suffix}`)) {
    suffix += 1;
  }

  return `${baseName} ${suffix}`;
}

function getUniqueChatTitle(project, baseTitle = 'New chat') {
  const existingTitles = new Set(project.chats.map((chat) => chat.title));
  if (!existingTitles.has(baseTitle)) {
    return baseTitle;
  }

  let suffix = 2;
  while (existingTitles.has(`${baseTitle} ${suffix}`)) {
    suffix += 1;
  }

  return `${baseTitle} ${suffix}`;
}

function createProjectWithInitialChat(name, repositoryPath = '') {
  return createProject(name, [createEmptyChat('New chat')], { repositoryPath });
}

function selectProjectAndChat(projectId, chatId = null) {
  setCurrentSelection(projectId, chatId ?? undefined);
}

let createProjectDialogResolver = null;

function openCreateProjectDialog() {
  if (!createProjectModal || !createProjectForm || !createProjectTitleInput || !createProjectRepositoryPathInput) {
    return Promise.resolve(null);
  }

  if (createProjectDialogResolver) {
    return Promise.resolve(null);
  }

  hideContextMenus();
  createProjectForm.reset();
  createProjectError.hidden = true;
  createProjectError.textContent = '';
  createProjectTitleInput.removeAttribute('aria-invalid');
  createProjectModal.hidden = false;
  createProjectModal.classList.add('is-open');
  createProjectTitleInput.focus();

  return new Promise((resolve) => {
    createProjectDialogResolver = resolve;
  });
}

function closeCreateProjectDialog(result = null) {
  const resolve = createProjectDialogResolver;
  createProjectDialogResolver = null;

  if (createProjectModal) {
    createProjectModal.classList.remove('is-open');
    createProjectModal.hidden = true;
  }

  if (createProjectError) {
    createProjectError.hidden = true;
    createProjectError.textContent = '';
  }

  if (createProjectTitleInput) {
    createProjectTitleInput.removeAttribute('aria-invalid');
  }

  if (typeof resolve === 'function') {
    resolve(result);
  }

  if (newProjectButton instanceof HTMLButtonElement) {
    newProjectButton.focus();
  }
}

function submitCreateProjectDialog() {
  if (!createProjectTitleInput || !createProjectRepositoryPathInput) {
    return;
  }

  const projectName = createProjectTitleInput.value.trim();
  const repositoryPath = normalizeRepositoryPath(createProjectRepositoryPathInput.value);

  if (!projectName) {
    createProjectError.textContent = 'Project name is required.';
    createProjectError.hidden = false;
    createProjectTitleInput.setAttribute('aria-invalid', 'true');
    createProjectTitleInput.focus();
    return;
  }

  const uniqueName = getUniqueProjectName(projectName);
  closeCreateProjectDialog({ projectName: uniqueName, repositoryPath });
}

async function createNewProject() {
  const details = await openCreateProjectDialog();
  if (!details) {
    return;
  }

  const project = createProjectWithInitialChat(details.projectName, details.repositoryPath);
  state.projects.push(project);
  markProductStateChanged();
  selectProjectAndChat(project.id, project.chats[0]?.id ?? null);
}

function renameProject(projectId) {
  const project = getProjectById(projectId);
  if (!project) {
    return;
  }

  const nextName = promptForName('Rename project', project.name);
  if (!nextName) {
    return;
  }

  project.name = nextName;
  touchProject(project);
  markProductStateChanged();
  render();
}


function createNewChatForProject(projectId) {
  const project = getProjectById(projectId);
  if (!project) {
    return;
  }

  const chat = createChatInProject(project, getUniqueChatTitle(project));
  selectProjectAndChat(project.id, chat.id);
}

function renameChat(chatId) {
  const chat = getChatById(chatId);
  if (!chat) {
    return;
  }

  const nextTitle = promptForName('Rename chat', chat.title);
  if (!nextTitle) {
    return;
  }

  chat.title = nextTitle;
  touchChat(chat);
  touchProject(getProjectByChatId(chat.id));
  markProductStateChanged();
  render();
}

async function deleteProject(projectId) {
  const projectIndex = getProjectIndexById(projectId);
  if (projectIndex === -1) {
    return;
  }

  const project = state.projects[projectIndex];
  if (!project) {
    return;
  }

  if (!(await confirmAction(`Delete project "${project.name}"?`))) {
    return;
  }

  const wasCurrentProject = project.id === state.currentProjectId;
  state.projects = state.projects.filter((item) => item.id !== projectId);

  if (state.projects.length === 0) {
    state.currentProjectId = null;
    state.currentChatId = null;
  } else if (wasCurrentProject || !getProjectById(state.currentProjectId)) {
    const nextProject = state.projects[projectIndex] ?? state.projects[projectIndex - 1] ?? state.projects[0];
    state.currentProjectId = nextProject.id;
    state.currentChatId = nextProject.chats[0]?.id ?? null;
  }

  touchRootState();
  markProductStateChanged();
  render();
  await saveProductStateNow();
}

async function deleteChat(projectId, chatId) {
  const project = getProjectById(projectId);
  if (!project) {
    return;
  }

  const chatIndex = project.chats.findIndex((chat) => chat.id === chatId);
  if (chatIndex === -1) {
    return;
  }

  const chat = project.chats[chatIndex];
  if (!chat) {
    return;
  }

  if (!(await confirmAction(`Delete chat "${chat.title}"?`))) {
    return;
  }

  const wasActive = project.id === state.currentProjectId && chat.id === state.currentChatId;
  project.chats.splice(chatIndex, 1);
  touchProject(project);
  touchRootState();

  if (project.chats.length === 0) {
    if (wasActive) {
      state.currentChatId = null;
    }
  } else if (wasActive) {
    const nextChat = project.chats[chatIndex] ?? project.chats[chatIndex - 1] ?? project.chats[0];
    state.currentProjectId = project.id;
    state.currentChatId = nextChat.id;
  }

  if (!state.projects.some((item) => item.id === state.currentProjectId)) {
    const nextProject = state.projects[0] ?? null;
    state.currentProjectId = nextProject?.id ?? null;
    state.currentChatId = nextProject?.chats[0]?.id ?? null;
  }

  markProductStateChanged();
  render();
  await saveProductStateNow();
}

async function clearChatMessages(projectId, chatId) {
  const project = getProjectById(projectId);
  if (!project) {
    return;
  }

  const chat = project.chats.find((item) => item.id === chatId);
  if (!chat) {
    return;
  }

  if (!(await confirmAction(`Clear all messages in chat "${chat.title}"?`))) {
    return;
  }

  chat.messages = [];
  touchChat(chat);
  touchProject(project);
  touchRootState();
  markProductStateChanged();
  render();
}

function createContextPackCandidateCard(chatId, contextPackId, status) {
  const card = document.createElement('div');
  card.className = `context-pack-card context-pack-card--${status}`;

  const header = document.createElement('div');
  header.className = 'context-pack-card__header';

  const eyebrow = document.createElement('div');
  eyebrow.className = 'context-pack-card__eyebrow';
  eyebrow.textContent = 'Context candidate';

  const title = document.createElement('div');
  title.className = 'context-pack-card__title';
  title.textContent = 'Current chat context placeholder';

  const summary = document.createElement('p');
  summary.className = 'context-pack-card__summary';
  summary.textContent = 'ProductState metadata placeholder. Placeholder only. No raw evidence. No .pi/rck read.';

  const footer = document.createElement('div');
  footer.className = 'context-pack-card__footer';
  footer.textContent = 'User approval required before injection.';

  header.append(eyebrow, title);
  card.append(header, summary, footer, createContextPackCandidateControls(chatId, contextPackId, status));
  return card;
}

function createMessageElement(message, contextPackLifecycleById = new Map(), chat = null, retryMessageId = null) {
  const role = message.role;
  const text = message.content ?? message.text ?? '';
  const variant = message.variant ?? 'normal';
  const kind = normalizeMessageKind(message.kind, message.variant);
  const messageEl = document.createElement('article');
  messageEl.className = `message message--${role}${variant === 'error' ? ' message--error' : ''}${variant === 'placeholder' ? ' message--placeholder' : ''}`;
  messageEl.dataset.messageId = message.id;

  if (kind === 'command-result') {
    messageEl.classList.add('message--command-result');
  }

  if (role === 'user' && isSlashCommandText(text)) {
    messageEl.classList.add('message--slash-command');
  }

  const roleLabel = document.createElement('span');
  roleLabel.className = 'message__role';
  roleLabel.textContent = role === 'assistant' ? 'Assistant' : 'You';

  const body = document.createElement('div');
  body.className = 'message__body';

  if (kind === 'command-result') {
    body.classList.add('message__body--command-result');
  }

  const isThinkingPlaceholder = kind === 'placeholder' && role === 'assistant' && text === chatThinkingMessage;
  if (isThinkingPlaceholder) {
    messageEl.classList.add('message--thinking');
    body.classList.add('message__body--thinking');
    body.setAttribute('aria-live', 'polite');
    body.appendChild(createThinkingIndicatorElement());
  }

  if (role === 'user' && isSlashCommandText(text)) {
    body.classList.add('message__body--slash-command');
  }

  const contextPackId = getMessageContextPackId(message);
  const checkpointId = getMessageCheckpointId(message);
  if (contextPackId) {
    messageEl.classList.add('message--context-pack');
  }

  if (checkpointId) {
    messageEl.classList.add('message--checkpoint');
  }

  if (isContextPackCandidateMessage(message) && contextPackId) {
    const status = contextPackLifecycleById.get(contextPackId) ?? 'candidate';
    body.classList.add('message__body--context-pack');
    body.appendChild(createContextPackCandidateCard(chat?.id ?? getCurrentChat()?.id ?? state.currentChatId, contextPackId, status));
  } else if (contextPackId) {
    const status = getContextPackMessageStatus(message, contextPackLifecycleById, chat) ?? 'candidate';
    body.classList.add('message__body--context-pack');
    body.append(createContextPackMessageBadge(status));

    const content = document.createElement('div');
    content.className = 'message__context-pack-content';
    content.textContent = text;
    body.appendChild(content);
  } else if (!isThinkingPlaceholder) {
    if (checkpointId && role === 'assistant' && kind === 'command-result') {
      const checkpointContent = document.createElement('div');
      checkpointContent.className = 'message__checkpoint-content';
      checkpointContent.appendChild(createCheckpointMessageBadge());

      const content = document.createElement('div');
      content.className = 'message__checkpoint-text';
      content.textContent = text;
      checkpointContent.appendChild(content);
      body.appendChild(checkpointContent);
    } else {
      body.textContent = text;
    }
  }

  if (role === 'assistant' && message.id === retryMessageId && !isThinkingPlaceholder && kind === 'normal') {
    const actions = document.createElement('div');
    actions.className = 'message__actions';

    const retryButton = document.createElement('button');
    retryButton.type = 'button';
    retryButton.className = 'button button--ghost button--compact message__action message__action--retry';
    retryButton.dataset.action = 'retry-message';
    retryButton.dataset.messageId = message.id;
    retryButton.textContent = 'Retry';
    actions.appendChild(retryButton);

    body.appendChild(actions);
  }

  messageEl.append(roleLabel, body);
  return messageEl;
}

let slashMenuSuppressed = false;

function getSlashMenuQuery() {
  const value = composerInput.value;
  if (!value.startsWith('/')) {
    return null;
  }

  return value.slice(1).trim().toLowerCase();
}

function getMatchingCommands(query) {
  if (!query) {
    return commandCatalog;
  }

  return commandCatalog.filter((command) => {
    const haystack = `${command.name} ${command.kind} ${command.description} ${command.usage}`.toLowerCase();
    return haystack.includes(query);
  });
}

function hideSlashMenu({ suppress = false } = {}) {
  if (suppress) {
    slashMenuSuppressed = true;
  }

  if (slashMenuEl) {
    slashMenuEl.hidden = true;
    slashMenuEl.replaceChildren();
  }
}

function renderSlashMenu() {
  if (!slashMenuEl) {
    return;
  }

  const query = getSlashMenuQuery();
  if (composerInput.disabled || slashMenuSuppressed || query === null) {
    hideSlashMenu();
    return;
  }

  const commands = getMatchingCommands(query);
  slashMenuEl.replaceChildren();

  if (commands.length === 0) {
    hideSlashMenu();
    return;
  }

  for (const command of commands) {
    const item = document.createElement('button');
    item.type = 'button';
    item.className = 'slash-menu__item slash-menu__item--' + command.kind;
    item.dataset.insertText = command.insertText;

    const row = document.createElement('div');
    row.className = 'slash-menu__row';

    const name = document.createElement('span');
    name.className = 'slash-menu__name';
    name.textContent = command.name;

    const badge = document.createElement('span');
    badge.className = `slash-menu__badge slash-menu__badge--${command.kind}`;
    badge.textContent = command.kind;

    row.append(name, badge);

    const description = document.createElement('div');
    description.className = 'slash-menu__description';
    description.textContent = command.description;

    item.append(row, description);
    slashMenuEl.appendChild(item);
  }

  slashMenuEl.hidden = false;
}

function insertCommandText(insertText) {
  composerInput.value = insertText;
  slashMenuSuppressed = false;
  syncComposerHeight();
  renderSlashMenu();
  composerInput.focus();
  const cursor = composerInput.value.length;
  composerInput.setSelectionRange(cursor, cursor);
}

function renderMessages({ autoScroll = false } = {}) {
  const chat = getCurrentChat();
  const currentProject = getCurrentProject();
  messagesInnerEl.replaceChildren();
  const contextPackLifecycleById = getContextPackLifecycleByChat(chat);
  const retryMessageId = getRetryableAssistantMessageId(chat);

  if (!chat) {
    const emptyState = document.createElement('div');
    emptyState.className = 'empty-state';
    emptyState.textContent = currentProject ? 'Create a chat in this project to start.' : 'Select or create a project first.';
    messagesInnerEl.appendChild(emptyState);
    return;
  }

  if (chat.messages.length === 0) {
    const emptyState = document.createElement('div');
    emptyState.className = 'empty-state';
    emptyState.textContent = 'Start a conversation in this chat.';
    messagesInnerEl.appendChild(emptyState);

    if (autoScroll) {
      scrollChatToBottom();
    }

    return;
  }

  for (const message of chat.messages) {
    messagesInnerEl.appendChild(createMessageElement(message, contextPackLifecycleById, chat, retryMessageId));
  }

  if (autoScroll) {
    scrollChatToBottom();
  }
}

function render() {
  renderHeader();
  renderSidebar();
  renderContextPackPreviewPanel();
  renderMessages();
  scrollChatToBottom();
  setBusy(false);
}


function setCurrentSelection(projectId, chatId) {
  const project = getProjectById(projectId) ?? state.projects[0];
  if (!project) {
    return;
  }

  const nextChat = chatId ? project.chats.find((item) => item.id === chatId) : getChatsForProject(project)[0] ?? null;
  let changed = false;

  if (state.currentProjectId !== project.id) {
    state.currentProjectId = project.id;
    changed = true;
  }

  const nextChatId = nextChat?.id ?? null;
  if (state.currentChatId !== nextChatId) {
    state.currentChatId = nextChatId;
    changed = true;
  }

  if (changed) {
    markProductStateChanged();
  }

  hideContextMenus();
  render();
  renderSlashMenu();
  composerInput.focus();
}

function deriveChatTitle(text) {
  const compact = text.replace(/\s+/g, ' ').trim();
  if (!compact) {
    return 'New chat';
  }

  const title = compact.split(' ').slice(0, 4).join(' ');
  return title.length > 28 ? `${title.slice(0, 28).trimEnd()}…` : title;
}

function maybeRenameChatFromFirstUserMessage(chat, userText) {
  if (chat.title !== 'New chat') {
    return;
  }

  if (userText.trim().startsWith('/')) {
    return;
  }

  const userMessages = chat.messages.filter((message) => message.role === 'user');
  if (userMessages.length !== 1) {
    return;
  }

  chat.title = deriveChatTitle(userText);
  touchChat(chat);
  touchProject(getProjectByChatId(chat.id));
  markProductStateChanged();
}

function appendMessageToChat(chatId, role, text, variant = 'normal', overrides = {}) {
  const chat = getChatById(chatId);
  if (!chat) {
    return;
  }

  const message = createMessage(role, text, variant, overrides);
  chat.messages.push(message);
  touchChat(chat);
  touchProject(getProjectByChatId(chat.id));
  markProductStateChanged();

  if (chat.id === state.currentChatId) {
    renderMessages({ autoScroll: true });
  }

  return message;
}

function replaceMessageInChat(chatId, messageId, nextMessage) {
  const chat = getChatById(chatId);
  if (!chat) {
    return null;
  }

  const messageIndex = chat.messages.findIndex((message) => message.id === messageId);
  if (messageIndex === -1) {
    return null;
  }

  chat.messages[messageIndex] = nextMessage;
  touchChat(chat);
  touchProject(getProjectByChatId(chat.id));
  markProductStateChanged();

  if (chat.id === state.currentChatId) {
    renderMessages({ autoScroll: true });
  }

  renderSidebar();
  renderHeader();
  return nextMessage;
}

function isChatCompletionTranscriptMessage(message) {
  if (!message || (message.role !== 'user' && message.role !== 'assistant')) {
    return false;
  }

  if (message.kind === 'command' || message.kind === 'command-result' || message.kind === 'placeholder') {
    return false;
  }

  const text = typeof message.content === 'string' ? message.content.trim() : typeof message.text === 'string' ? message.text.trim() : '';
  if (!text) {
    return false;
  }

  return text !== newChatAssistantMessage && text !== localIntroMessage;
}

function isChatCompletionPlainUserMessage(message) {
  if (!isChatCompletionTranscriptMessage(message) || message.role !== 'user') {
    return false;
  }

  const text = typeof message.content === 'string' ? message.content : typeof message.text === 'string' ? message.text : '';
  return !isSlashCommandText(text);
}

function isChatCompletionAssistantReply(message) {
  return isChatCompletionTranscriptMessage(message) && message.role === 'assistant';
}

function getChatCompletionMessages(chat, limit = chatCompletionContextLimit, options = {}) {
  if (!chat) {
    return [];
  }

  const excludeMessageIds = options.excludeMessageIds instanceof Set ? options.excludeMessageIds : new Set(options.excludeMessageIds ?? []);
  const transcript = chat.messages.filter((message) => isChatCompletionTranscriptMessage(message) && !excludeMessageIds.has(message.id));
  return transcript.slice(-Math.max(1, limit)).map((message) => ({
    role: message.role,
    content: typeof message.content === 'string' ? message.content : typeof message.text === 'string' ? message.text : '',
  }));
}

function getRetryableAssistantMessageId(chat) {
  if (!chat || (activeChatCompletionRun && !activeChatCompletionRun.completed)) {
    return null;
  }

  for (let index = chat.messages.length - 1; index >= 0; index -= 1) {
    const message = chat.messages[index];
    if (isChatCompletionAssistantReply(message)) {
      return message.id;
    }
  }

  return null;
}

function getRetrySourceUserMessage(chat, assistantMessageId) {
  if (!chat || !assistantMessageId) {
    return null;
  }

  const assistantIndex = chat.messages.findIndex((message) => message.id === assistantMessageId);
  if (assistantIndex === -1) {
    return null;
  }

  for (let index = assistantIndex - 1; index >= 0; index -= 1) {
    const message = chat.messages[index];
    if (isChatCompletionPlainUserMessage(message)) {
      return message;
    }
  }

  return null;
}

function createThinkingIndicatorElement() {
  const indicator = document.createElement('span');
  indicator.className = 'thinking-indicator';
  indicator.setAttribute('aria-hidden', 'true');

  const label = document.createElement('span');
  label.className = 'thinking-indicator__label';
  label.textContent = 'Thinking';

  const dots = document.createElement('span');
  dots.className = 'thinking-indicator__dots';

  for (let index = 0; index < 3; index += 1) {
    const dot = document.createElement('span');
    dot.className = 'thinking-indicator__dot';
    dot.style.animationDelay = `${index * 0.16}s`;
    dots.appendChild(dot);
  }

  indicator.append(label, dots);
  return indicator;
}

function createChatCompletionPlaceholderMessage() {
  return createMessage('assistant', chatThinkingMessage, 'placeholder', { kind: 'placeholder' });
}

function getChatCompletionRequestBody(chat, userMessage, options = {}) {
  const project = getProjectByChatId(chat.id);
  const messages = getChatCompletionMessages(chat, chatCompletionContextLimit, options);
  const projectId = typeof project?.id === 'string' ? project.id : chat.projectId ?? null;

  if (userMessage && !messages.some((message) => message.role === 'user' && message.content === userMessage.content)) {
    messages.push({ role: 'user', content: userMessage.content ?? userMessage.text ?? '' });
  }

  return {
    projectId,
    chatId: chat.id,
    messages,
  };
}

async function postChatCompletion(body, options = {}) {
  const response = await fetch(chatCompletionEndpoint, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(body),
    signal: options.signal,
  });
  const data = await readJsonResponse(response);

  if (!response.ok) {
    const error = new Error(data?.error?.message ?? data?.message ?? 'Request failed');
    error.statusCode = response.status;
    error.response = data;
    throw error;
  }

  return data;
}

async function postChatCompletionStream(body, handlers = {}, options = {}) {
  const response = await fetch(chatStreamingEndpoint, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
    body: JSON.stringify(body),
    signal: options.signal,
  });

  if (!response.ok) {
    const data = await readJsonResponse(response);
    const error = new Error(data?.error?.message ?? data?.message ?? 'Request failed');
    error.statusCode = response.status;
    error.response = data;
    throw error;
  }

  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.includes('text/event-stream')) {
    const data = await readJsonResponse(response);
    const error = new Error(data?.error?.message ?? 'Streaming response not available.');
    error.statusCode = response.status;
    error.response = data;
    throw error;
  }

  await consumeSseResponse(response, handlers);
}

async function consumeSseResponse(response, handlers = {}) {
  const reader = response.body?.getReader();
  if (!reader) {
    throw new Error('Streaming response body is unavailable.');
  }

  const decoder = new TextDecoder();
  let buffer = '';

  const dispatchEvent = (eventName, data) => {
    const handler = handlers[eventName];
    if (typeof handler === 'function') {
      handler(data);
    }
  };

  const parseEventBlock = (block) => {
    const lines = block.split(/\r?\n/);
    let eventName = 'message';
    let dataText = '';

    for (const line of lines) {
      if (line.startsWith('event:')) {
        eventName = line.slice(6).trim();
      } else if (line.startsWith('data:')) {
        dataText += `${dataText ? '\n' : ''}${line.slice(5).trimStart()}`;
      }
    }

    if (!dataText) {
      dispatchEvent(eventName, null);
      return;
    }

    let data;
    try {
      data = JSON.parse(dataText);
    } catch {
      data = { raw: dataText };
    }

    dispatchEvent(eventName, data);
  };

  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) {
        break;
      }

      buffer += decoder.decode(value, { stream: true });
      let boundaryIndex = buffer.indexOf('\n\n');

      while (boundaryIndex !== -1) {
        const block = buffer.slice(0, boundaryIndex).trim();
        buffer = buffer.slice(boundaryIndex + 2);
        if (block) {
          parseEventBlock(block);
        }
        boundaryIndex = buffer.indexOf('\n\n');
      }
    }

    buffer += decoder.decode();
    const tail = buffer.trim();
    if (tail) {
      parseEventBlock(tail);
    }
  } finally {
    reader.releaseLock();
  }
}

function getStreamErrorMessage(error) {
  const responseError = error?.response?.error;
  const responseMessage = typeof responseError?.message === 'string' ? responseError.message : typeof error?.message === 'string' ? error.message : '';
  const responseCode = typeof responseError?.code === 'string' ? responseError.code : null;

  if (responseCode === 'invalid_request') {
    return responseMessage || 'The chat completion request was invalid.';
  }

  if (responseCode === 'llm_empty_response') {
    return 'The language model returned no reply. Please try again.';
  }

  if (responseCode === 'llm_unavailable') {
    return responseMessage || chatCompletionFailureMessage;
  }

  return chatCompletionFailureMessage;
}

function getChatCompletionErrorMessage(error) {
  const responseError = error?.response?.error;
  const responseMessage = typeof responseError?.message === 'string' ? responseError.message : typeof error?.message === 'string' ? error.message : '';
  const responseCode = typeof responseError?.code === 'string' ? responseError.code : null;

  if (responseCode === 'invalid_request') {
    return responseMessage || 'The chat completion request was invalid.';
  }

  if (responseCode === 'llm_empty_response') {
    return 'The language model returned no reply. Please try again.';
  }

  if (responseCode === 'llm_unavailable') {
    return responseMessage || chatCompletionFailureMessage;
  }

  return chatCompletionFailureMessage;
}

function isAbortError(error) {
  return error?.name === 'AbortError' || error?.code === 20;
}

function ensureChatCancelButton() {
  if (chatCancelButton || !composerFooter) {
    return chatCancelButton;
  }

  chatCancelButton = document.createElement('button');
  chatCancelButton.type = 'button';
  chatCancelButton.className = 'button button--ghost button--compact composer__cancel-button';
  chatCancelButton.textContent = 'Cancel';
  chatCancelButton.hidden = true;
  chatCancelButton.addEventListener('click', () => {
    void cancelActiveChatCompletion();
  });

  composerFooter.insertBefore(chatCancelButton, sendButton);
  return chatCancelButton;
}

function getActiveChatCompletionRun() {
  const run = activeChatCompletionRun;
  if (!run) {
    return null;
  }

  const isActive = !run.cancelled && !run.completed && !run.finalized && !run.controller.signal.aborted;
  if (!isActive) {
    activeChatCompletionRun = null;
    return null;
  }

  return run;
}

function updateChatCompletionControls() {
  const button = ensureChatCancelButton();
  if (!button) {
    return;
  }

  const isStreaming = Boolean(getActiveChatCompletionRun());
  button.hidden = !isStreaming;
  button.disabled = !isStreaming;
}

function appendTransientMessageToChat(chatId, message) {
  const chat = getChatById(chatId);
  if (!chat) {
    return null;
  }

  chat.messages.push(message);
  touchChat(chat, { dirty: false });
  touchProject(getProjectByChatId(chat.id), { dirty: false });
  touchRootState({ dirty: false });

  if (chat.id === state.currentChatId) {
    renderMessages({ autoScroll: true });
  }

  renderSidebar();
  renderHeader();
  return message;
}

function updateTransientMessageInChat(chatId, messageId, mutator) {
  const chat = getChatById(chatId);
  if (!chat) {
    return null;
  }

  const message = chat.messages.find((item) => item.id === messageId);
  if (!message) {
    return null;
  }

  mutator(message);
  touchChat(chat, { dirty: false });
  touchProject(getProjectByChatId(chat.id), { dirty: false });
  touchRootState({ dirty: false });

  if (chat.id === state.currentChatId) {
    renderMessages({ autoScroll: true });
  }

  renderSidebar();
  renderHeader();
  return message;
}

async function cancelActiveChatCompletion() {
  const run = activeChatCompletionRun;
  if (!run || run.finalized || run.completed) {
    return false;
  }

  run.cancelled = true;
  run.finalized = true;
  activeChatCompletionRun = null;
  run.controller.abort();
  setBusy(false);
  updateChatCompletionControls();

  if (typeof run.chatId === 'string' && typeof run.responseMessageId === 'string') {
    replaceMessageInChat(
      run.chatId,
      run.responseMessageId,
      createMessage('assistant', chatResponseCancelledMessage, 'normal', {
        id: run.responseMessageId,
        kind: 'command-result',
      }),
    );
    clearTimeout(productStateSaveTimer);
    productStateSaveTimer = null;
    await saveProductStateNow();
  }

  composerInput.focus();
  return true;
}

async function runChatCompletion(targetChatId, userMessage, options = {}) {
  const chat = getChatById(targetChatId);
  if (!chat) {
    return;
  }

  const runId = makeId('chat-completion-run');
  const controller = new AbortController();
  const run = {
    runId,
    controller,
    chatId: targetChatId,
    responseMessageId: typeof options.responseMessageId === 'string' ? options.responseMessageId : null,
    cancelled: false,
    completed: false,
    finalized: false,
  };

  activeChatCompletionRun = run;

  const placeholderMessage = run.responseMessageId
    ? updateTransientMessageInChat(targetChatId, run.responseMessageId, (message) => {
        message.text = chatThinkingMessage;
        message.content = chatThinkingMessage;
        message.variant = 'placeholder';
        message.kind = 'placeholder';
      })
    : appendTransientMessageToChat(targetChatId, createChatCompletionPlaceholderMessage());

  if (!placeholderMessage) {
    activeChatCompletionRun = null;
    updateChatCompletionControls();
    return;
  }

  run.responseMessageId = placeholderMessage.id;
  updateChatCompletionControls();
  setBusy(true, chatThinkingMessage);

  const requestBody = getChatCompletionRequestBody(chat, userMessage, {
    excludeMessageIds: [placeholderMessage.id],
  });

  clearTimeout(productStateSaveTimer);
  productStateSaveTimer = null;
  await saveProductStateNow();

  let streamedText = '';
  let streamStarted = false;
  let streamMetadata = null;

  try {
    await postChatCompletionStream(
      requestBody,
      {
        start: (data) => {
          if (run.cancelled || run.finalized || activeChatCompletionRun?.runId !== runId) {
            return;
          }

          streamStarted = true;
          streamMetadata = data?.metadata ?? null;
        },
        delta: (data) => {
          if (run.cancelled || run.finalized || activeChatCompletionRun?.runId !== runId) {
            return;
          }

          if (typeof data?.text !== 'string' || !data.text) {
            return;
          }

          streamedText += data.text;
          updateTransientMessageInChat(targetChatId, placeholderMessage.id, (message) => {
            message.text = streamedText;
            message.content = streamedText;
            message.variant = 'normal';
            message.kind = 'normal';
          });
        },
        done: (data) => {
          if (run.cancelled || run.finalized || activeChatCompletionRun?.runId !== runId) {
            return;
          }

          streamStarted = true;
          const nextAssistantText = typeof data?.message?.content === 'string' ? data.message.content.trim() : streamedText.trim();

          if (!nextAssistantText) {
            throw new Error('LLM response did not contain assistant text.');
          }

          streamedText = nextAssistantText;
          replaceMessageInChat(
            targetChatId,
            placeholderMessage.id,
            createMessage('assistant', nextAssistantText, 'normal', {
              id: placeholderMessage.id,
              kind: 'normal',
            }),
          );
          run.completed = true;
        },
        error: (data) => {
          if (run.cancelled || run.finalized || activeChatCompletionRun?.runId !== runId) {
            return;
          }

          const error = new Error(data?.error?.message ?? data?.message ?? 'Request failed');
          error.statusCode = data?.error?.code ? 503 : undefined;
          error.response = data;
          throw error;
        },
      },
      { signal: controller.signal },
    );

    if (run.cancelled || controller.signal.aborted || activeChatCompletionRun?.runId !== runId) {
      return;
    }

    if (!streamedText.trim()) {
      throw new Error('The language model returned no reply.');
    }

    clearTimeout(productStateSaveTimer);
    productStateSaveTimer = null;
    await saveProductStateNow();
    run.finalized = true;
  } catch (error) {
    if (run.cancelled || run.finalized || controller.signal.aborted || isAbortError(error) || activeChatCompletionRun?.runId !== runId) {
      return;
    }

    const responseCode = error?.response?.error?.code ?? null;
    if (!streamStarted && responseCode !== 'invalid_request') {
      try {
        const fallbackResponse = await postChatCompletion(requestBody, { signal: controller.signal });
        const nextAssistantText = typeof fallbackResponse?.message?.content === 'string' ? fallbackResponse.message.content.trim() : '';

        if (nextAssistantText) {
          replaceMessageInChat(
            targetChatId,
            placeholderMessage.id,
            createMessage('assistant', nextAssistantText, 'normal', {
              id: placeholderMessage.id,
              kind: 'normal',
            }),
          );
          run.finalized = true;
          clearTimeout(productStateSaveTimer);
          productStateSaveTimer = null;
          await saveProductStateNow();
          return;
        }
      } catch (fallbackError) {
        error = fallbackError;
      }
    }

    replaceMessageInChat(
      targetChatId,
      placeholderMessage.id,
      createMessage('assistant', getStreamErrorMessage(error), 'error', {
        id: placeholderMessage.id,
        kind: 'error',
      }),
    );
    run.finalized = true;
    clearTimeout(productStateSaveTimer);
    productStateSaveTimer = null;
    await saveProductStateNow();
  } finally {
    if (activeChatCompletionRun?.runId === runId) {
      activeChatCompletionRun = null;
      updateChatCompletionControls();
      setBusy(false);
      composerInput.focus();
      renderMessages({ autoScroll: true });
    }
  }
}

async function retryChatCompletion(targetChatId, assistantMessageId) {
  if (activeChatCompletionRun) {
    return;
  }

  const chat = getChatById(targetChatId);
  if (!chat) {
    return;
  }

  const assistantMessage = chat.messages.find((message) => message.id === assistantMessageId);
  if (!assistantMessage || !isChatCompletionAssistantReply(assistantMessage)) {
    return;
  }

  const userMessage = getRetrySourceUserMessage(chat, assistantMessageId);
  if (!userMessage) {
    return;
  }

  await runChatCompletion(targetChatId, userMessage, { responseMessageId: assistantMessageId });
}

async function readJsonResponse(response) {
  const text = await response.text();

  if (!text.trim()) {
    return null;
  }

  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

async function getJson(pathname) {
  const response = await fetch(pathname, {
    method: 'GET',
    headers: { Accept: 'application/json' },
  });
  const data = await readJsonResponse(response);

  if (!response.ok) {
    throw new Error(data?.error ?? 'Request failed');
  }

  return data;
}

async function postJson(pathname, body) {
  const response = await fetch(pathname, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(body),
  });
  const data = await readJsonResponse(response);

  if (!response.ok) {
    throw new Error(data?.error ?? 'Request failed');
  }

  return data;
}

function buildHelpMessage() {
  const lines = commandCatalog.map((command) => `${command.usage} — ${command.description}`);
  return ['Available RufusChat commands:', ...lines].join('\n');
}

function parseSlashCommand(text) {
  const trimmed = text.trim();
  if (!trimmed.startsWith('/')) {
    return { type: 'plain' };
  }

  const [commandToken, ...restTokens] = trimmed.split(/\s+/);
  const command = commandToken.toLowerCase();

  switch (command) {
    case '/status':
      return { type: 'status' };
    case '/checkpoint': {
      const label = restTokens.join(' ').trim() || 'Untitled checkpoint';
      return { type: 'checkpoint', label };
    }
    case '/inject':
      return { type: 'inject' };
    case '/help':
      return { type: 'help' };
    case '/trace': {
      const mode = restTokens[0]?.toLowerCase();
      if (mode === 'link') {
        return { type: 'trace-link' };
      }

      return { type: 'trace' };
    }
    case '/hermes': {
      const mode = restTokens[0]?.toLowerCase();
      if (mode !== 'fake') {
        return { type: 'hermes-real' };
      }

      const prompt = restTokens.slice(1).join(' ').trim();
      return prompt ? { type: 'hermes-fake', prompt } : { type: 'hermes-fake-missing-prompt' };
    }
    default:
      return { type: 'unknown' };
  }
}

function openConfirm(message) {
  confirmDescription.textContent = message;
  confirmModal.hidden = false;
  confirmModal.classList.add('is-open');
  confirmCancelButton.focus();
  setBusy(true, 'Confirming...');

  return new Promise((resolve) => {
    confirmResolver = resolve;
  });
}

function closeConfirm(confirmed) {
  const resolve = confirmResolver;
  confirmResolver = null;
  confirmModal.classList.remove('is-open');
  confirmModal.hidden = true;
  setBusy(false);

  if (typeof resolve === 'function') {
    resolve(confirmed);
  }
}

async function confirmAction(message) {
  return openConfirm(message);
}

function getRuntimeStatusForCommand() {
  const fallbackRuntimeStatus = createFallbackRuntimeStatus();
  const candidate = normalizeRuntimeStatus(runtimeStatus) ?? fallbackRuntimeStatus;

  return {
    runtime: candidate.runtime ?? fallbackRuntimeStatus.runtime,
    memory: candidate.memory ?? fallbackRuntimeStatus.memory,
    context: candidate.context ?? fallbackRuntimeStatus.context,
    trace: candidate.trace ?? fallbackRuntimeStatus.trace,
  };
}

function formatRuntimeStatusMessage() {
  const fallbackRuntimeStatus = createFallbackRuntimeStatus();
  const status = getRuntimeStatusForCommand();
  const runtimeLabel = status.runtime?.label ?? fallbackRuntimeStatus.runtime.label;
  const memoryLabel = status.memory?.label ?? fallbackRuntimeStatus.memory.label;
  const contextLabel = status.context?.label ?? fallbackRuntimeStatus.context.label;
  const traceLabel = status.trace?.label ?? fallbackRuntimeStatus.trace.label;

  return formatCompactMetadata(['Status', memoryLabel, contextLabel, traceLabel, runtimeLabel]);
}

function runStatus(targetChatId) {
  appendMessageToChat(targetChatId, 'assistant', formatRuntimeStatusMessage(), 'normal', { kind: 'command-result' });
}

async function runCheckpoint(targetChatId, label, sourceMessage = null) {
  if (!(await confirmAction(`Create checkpoint "${label}"?`))) {
    appendMessageToChat(targetChatId, 'assistant', 'Checkpoint cancelled.');
    return;
  }

  const chat = getChatById(targetChatId);
  if (!chat) {
    return;
  }

  const checkpointId = makeCheckpointId();
  const sourceMessageId = typeof sourceMessage?.id === 'string' ? sourceMessage.id : null;
  const historyItem = upsertChatCheckpointHistoryItem(chat, checkpointId, {
    label,
    summary: 'Product checkpoint only. No internal anchor was created. No raw evidence stored.',
    sourceMessageId,
    sourceKind: 'product',
    safeMetadata: {
      productOnly: true,
      command: '/checkpoint',
    },
  });

  if (sourceMessageId) {
    sourceMessage.links = { ...(sourceMessage.links ?? {}), checkpointId };
  }

  const resultMessage = appendMessageToChat(targetChatId, 'assistant', createCheckpointResultContent(label), 'normal', {
    kind: 'command-result',
    links: { checkpointId },
  });

  historyItem.resultMessageId = resultMessage?.id ?? null;
  historyItem.updatedAt = nowIso();
  touchChat(chat);
  touchProject(getProjectByChatId(chat.id));
  markProductStateChanged();
  render();
}

function finalizeContextPackCandidate(targetChatId, contextPackId, decision) {
  void targetChatId;
  void contextPackId;
  void decision;
  return;
}

async function runInject(targetChatId) {
  void targetChatId;
  await openContextPackPreview();
}

async function runHermesFake(targetChatId, prompt) {
  if (!prompt) {
    appendMessageToChat(targetChatId, 'assistant', 'Hermes fake run requires a prompt.', 'normal', { kind: 'command-result' });
    return;
  }

  if (!(await confirmAction(`Run fake Hermes for: ${prompt}`))) {
    appendMessageToChat(targetChatId, 'assistant', 'Hermes fake run cancelled.');
    return;
  }

  setBusy(true);
  try {
    const data = await postJson('/api/hermes/fake', { prompt });
    appendMessageToChat(targetChatId, 'assistant', data.message ?? 'Hermes fake run completed.', 'normal', { kind: 'command-result' });
  } catch (error) {
    appendMessageToChat(
      targetChatId,
      'assistant',
      `Hermes fake request failed. ${error instanceof Error ? error.message : 'Unknown error'}.`,
      'error',
    );
  } finally {
    setBusy(false);
  }
}

function runTracePlaceholder(targetChatId, mode = 'trace') {
  appendMessageToChat(
    targetChatId,
    'assistant',
    mode === 'trace-link' ? traceLinkPlaceholderMessage : traceLinkedPlaceholderMessage,
    'normal',
    { kind: 'placeholder' },
  );
}

function showLocalFallback(targetChatId, text) {
  appendMessageToChat(
    targetChatId,
    'assistant',
    text.startsWith('/')
      ? 'Command recognized locally. This browser-local chat keeps the response in the active chat.'
      : 'I received your message locally. This browser session does not use LLM or semantic memory yet.',
    'normal',
    { kind: 'command-result' },
  );
}

async function handleUserSubmission(text) {
  const targetChatId = state.currentChatId;
  const chat = getChatById(targetChatId);
  if (!chat) {
    return;
  }

  const userMessage = appendMessageToChat(targetChatId, 'user', text);
  const command = parseSlashCommand(text);

  if (command.type === 'plain') {
    maybeRenameChatFromFirstUserMessage(chat, text);
  }

  renderSidebar();
  renderHeader();

  if (command.type === 'plain') {
    await runChatCompletion(targetChatId, userMessage);
    return;
  }

  if (command.type === 'unknown') {
    appendMessageToChat(targetChatId, 'assistant', 'Command not recognized. Available commands: /status, /checkpoint, /inject, /hermes fake <prompt>, /trace, /trace link, /help.', 'error', { kind: 'error' });
    return;
  }

  if (command.type === 'hermes-real') {
    appendMessageToChat(targetChatId, 'assistant', 'Hermes real is not connected in this UI. Use /hermes fake <prompt>.', 'error', { kind: 'error' });
    return;
  }

  if (command.type === 'help') {
    appendMessageToChat(targetChatId, 'assistant', buildHelpMessage(), 'normal', { kind: 'command-result' });
    return;
  }

  if (command.type === 'trace' || command.type === 'trace-link') {
    runTracePlaceholder(targetChatId, command.type);
    return;
  }

  if (command.type === 'hermes-fake-missing-prompt') {
    appendMessageToChat(targetChatId, 'assistant', 'Hermes fake run requires a prompt.');
    return;
  }

  switch (command.type) {
    case 'status':
      await runStatus(targetChatId);
      return;
    case 'checkpoint':
      await runCheckpoint(targetChatId, command.label, userMessage);
      return;
    case 'inject':
      await runInject(targetChatId);
      return;
    case 'hermes-fake':
      await runHermesFake(targetChatId, command.prompt);
      return;
    default:
      appendMessageToChat(targetChatId, 'assistant', 'Command not recognized. Available commands: /status, /checkpoint, /inject, /hermes fake <prompt>.', 'error', { kind: 'error' });
  }
}

composerForm.addEventListener('submit', (event) => {
  event.preventDefault();

  if (composerInput.disabled) {
    return;
  }

  const text = composerInput.value.trim();
  if (!text) {
    hideSlashMenu();
    syncComposerHeight();
    return;
  }

  composerInput.value = '';
  syncComposerHeight();
  hideSlashMenu();
  void handleUserSubmission(text);
  composerInput.focus();
});

messagesEl.addEventListener('click', (event) => {
  const button = event.target instanceof HTMLElement ? event.target.closest('button[data-action]') : null;
  if (!(button instanceof HTMLButtonElement)) {
    return;
  }

  if (button.dataset.action === 'retry-message' && button.dataset.messageId) {
    event.preventDefault();
    void retryChatCompletion(state.currentChatId, button.dataset.messageId);
  }
});

composerInput.addEventListener('input', () => {
  slashMenuSuppressed = false;
  syncComposerHeight();
  renderSlashMenu();
});

composerInput.addEventListener('keydown', (event) => {
  if (event.key === 'Escape' && !slashMenuEl.hidden) {
    event.preventDefault();
    hideSlashMenu({ suppress: true });
    return;
  }

  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault();
    composerForm.requestSubmit();
  }
});

confirmCancelButton.addEventListener('click', () => {
  closeConfirm(false);
});

confirmConfirmButton.addEventListener('click', () => {
  closeConfirm(true);
});

confirmModal.addEventListener('click', (event) => {
  if (event.target instanceof HTMLElement && event.target.hasAttribute('data-confirm-cancel')) {
    closeConfirm(false);
  }
});

if (createProjectModal) {
  createProjectModal.addEventListener('click', (event) => {
    if (event.target instanceof HTMLElement && event.target.hasAttribute('data-create-project-cancel')) {
      closeCreateProjectDialog(null);
    }
  });
}

if (createProjectCancelButton) {
  createProjectCancelButton.addEventListener('click', () => {
    closeCreateProjectDialog(null);
  });
}

if (createProjectForm) {
  createProjectForm.addEventListener('submit', (event) => {
    event.preventDefault();
    submitCreateProjectDialog();
  });
}

document.addEventListener('keydown', (event) => {
  if (confirmResolver) {
    if (event.key === 'Escape') {
      event.preventDefault();
      closeConfirm(false);
    }
    return;
  }

  if (createProjectDialogResolver && event.key === 'Escape') {
    event.preventDefault();
    closeCreateProjectDialog(null);
  }
});

projectTreeEl.addEventListener('click', (event) => {
  const button = event.target instanceof HTMLElement ? event.target.closest('button[data-action]') : null;
  if (!(button instanceof HTMLButtonElement)) {
    return;
  }

  const action = button.dataset.action;
  const projectId = button.dataset.projectId;
  const chatId = button.dataset.chatId;

  if (!projectId) {
    return;
  }

  if (action === 'select-project') {
    hideContextMenus();
    setCurrentSelection(projectId);
    return;
  }

  if (action === 'select-chat' && chatId) {
    hideContextMenus();
    setCurrentSelection(projectId, chatId);
    return;
  }

  if (action === 'open-project-menu') {
    openProjectContextMenu(projectId, button);
    return;
  }

  if (action === 'open-chat-menu' && chatId) {
    openChatContextMenu(projectId, chatId, button);
  }
});

if (newProjectButton) {
  newProjectButton.addEventListener('click', createNewProject);
}

if (productStateExportButton) {
  productStateExportButton.addEventListener('click', () => {
    void exportProductState();
  });
}

if (productStateImportButton && productStateImportInput) {
  productStateImportButton.addEventListener('click', () => {
    productStateImportInput.click();
  });

  productStateImportInput.addEventListener('change', () => {
    const file = productStateImportInput.files?.[0] ?? null;
    void importProductStateFromFile(file);
  });
}

if (productStateResetButton) {
  productStateResetButton.addEventListener('click', () => {
    void resetProductState();
  });
}

if (attachContextPackButton) {
  attachContextPackButton.addEventListener('click', () => {
    if (isContextSidePanelOpen) {
      closeContextPackPreview();
      return;
    }

    void openContextPackPreview();
  });
}

if (contextPackPreviewLoadButton) {
  contextPackPreviewLoadButton.addEventListener('click', () => {
    void loadLoadedContextPackPreviewFromJson();
  });
}

if (contextSidePanelCloseButton) {
  contextSidePanelCloseButton.addEventListener('click', () => {
    closeContextPackPreview();
  });
}

if (contextScopeSuggestionApproveButton) {
  contextScopeSuggestionApproveButton.addEventListener('click', () => {
    void approveContextScopeSuggestion();
  });
}

if (contextScopeSuggestionRejectButton) {
  contextScopeSuggestionRejectButton.addEventListener('click', () => {
    rejectContextScopeSuggestion();
  });
}

if (contextScopeSuggestionAdjustButton) {
  contextScopeSuggestionAdjustButton.addEventListener('click', () => {
    void adjustContextScopeSuggestion();
  });
}

if (contextPackPreviewCloseButton) {
  contextPackPreviewCloseButton.addEventListener('click', () => {
    closeContextPackPreview();
  });
}

if (contextPackPreviewConfirmButton) {
  contextPackPreviewConfirmButton.addEventListener('click', () => {
    closeContextPackPreview();
  });
}

if (projectContextMenuEl) {
  projectContextMenuEl.addEventListener('click', async (event) => {
    const button = event.target instanceof HTMLElement ? event.target.closest('button[data-context-action]') : null;
    if (!(button instanceof HTMLButtonElement)) {
      return;
    }

    const action = button.dataset.contextAction;
    const context = activeContextMenu;
    hideContextMenus();

    if (!context?.projectId) {
      return;
    }

    switch (action) {
      case 'rename-project':
        renameProject(context.projectId);
        break;
      case 'new-chat':
        createNewChatForProject(context.projectId);
        break;
      case 'delete-project':
        await deleteProject(context.projectId);
        break;
      default:
        break;
    }
  });
}

if (chatContextMenuEl) {
  chatContextMenuEl.addEventListener('click', async (event) => {
    const button = event.target instanceof HTMLElement ? event.target.closest('button[data-context-action]') : null;
    if (!(button instanceof HTMLButtonElement)) {
      return;
    }

    const action = button.dataset.contextAction;
    const context = activeContextMenu;
    hideContextMenus();

    if (!context?.projectId || !context?.chatId) {
      return;
    }

    switch (action) {
      case 'rename-chat':
        renameChat(context.chatId);
        break;
      case 'clear-chat':
        await clearChatMessages(context.projectId, context.chatId);
        break;
      case 'delete-chat':
        await deleteChat(context.projectId, context.chatId);
        break;
      default:
        break;
    }
  });
}

document.addEventListener('click', (event) => {
  const target = event.target instanceof HTMLElement ? event.target : null;
  if (!target) {
    return;
  }

  if (target.closest('.context-menu')) {
    return;
  }

  if (target.closest('button[data-action="open-project-menu"], button[data-action="open-chat-menu"]')) {
    return;
  }

  hideContextMenus();
});

document.addEventListener('keydown', (event) => {
  if (!confirmResolver && event.key === 'Escape' && activeContextMenu) {
    event.preventDefault();
    hideContextMenus();
    return;
  }

  if (!confirmResolver) {
    return;
  }

  if (event.key === 'Escape') {
    event.preventDefault();
    closeConfirm(false);
  }
});

if (slashMenuEl) {
  slashMenuEl.addEventListener('click', (event) => {
    const button = event.target instanceof HTMLElement ? event.target.closest('button[data-insert-text]') : null;
    if (!(button instanceof HTMLButtonElement)) {
      return;
    }

    const insertText = button.dataset.insertText ?? '';
    insertCommandText(insertText);
  });
}

async function bootstrapApp() {
  setBusy(false);
  render();
  syncComposerHeight();
  renderSlashMenu();
  await hydrateRuntimeStatus();
  composerInput.focus();
  await hydrateProductState();
}

void bootstrapApp();
