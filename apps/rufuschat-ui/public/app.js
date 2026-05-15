const messagesEl = document.getElementById('messages');
const composerForm = document.getElementById('composer-form');
const composerInput = document.getElementById('composer-input');
const sendButton = composerForm.querySelector('button[type="submit"]');
const messagesInnerEl = document.getElementById('messages-inner');
const currentProjectEl = document.getElementById('current-project');
const currentChatEl = document.getElementById('current-chat');
const memoryStatusEl = document.getElementById('current-memory-status');
const summaryStatusEl = document.getElementById('current-summary-status');
const rckTraceStatusEl = document.getElementById('current-rck-trace-status');
const traceChipEl = document.getElementById('current-trace-chip');
const chatSessionShellTraceEl = document.querySelector('.chat-session-shell__trace');
const statusPill = document.querySelector('.chat-header__status');
const confirmModal = document.getElementById('confirm-modal');
const confirmDescription = document.getElementById('confirm-modal-description');
const confirmCancelButton = document.getElementById('confirm-modal-cancel');
const confirmConfirmButton = document.getElementById('confirm-modal-confirm');
const projectTreeEl = document.getElementById('project-tree');
const slashMenuEl = document.getElementById('slash-menu');
const newProjectButton = document.getElementById('new-project-button');
const projectContextMenuEl = document.getElementById('project-context-menu');
const chatContextMenuEl = document.getElementById('chat-context-menu');
const productStateExportButton = document.getElementById('product-state-export-button');
const productStateImportButton = document.getElementById('product-state-import-button');
const productStateResetButton = document.getElementById('product-state-reset-button');
const productStateImportInput = document.getElementById('product-state-import-input');

const productStateEndpoint = '/api/product-state';
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
const contextPackCandidateSummaryLines = [
  'Current chat context placeholder',
  'ProductState metadata placeholder',
  'No internal evidence included',
];
const contextPackCandidateSafetyLines = ['Placeholder only', 'No raw evidence included', 'No .pi/rck read'];
const contextPackInjectionHistoryTitle = 'Context pack candidate';
const contextPackInjectionHistorySourceKind = 'placeholder';
const contextPackInjectedMessage = 'Context pack injected.';
const contextPackCancelledMessage = 'Context pack candidate cancelled.';
const localIntroMessage =
  'This chat is local. LLM and semantic memory are not connected yet. Trace tracking happens only when you confirm slash actions.';
const newChatAssistantMessage =
  'New local chat created. LLM and semantic memory are not connected yet.';
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
    kind: 'mutating',
    description: 'Prepare a safe Context Pack candidate for this chat.',
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
  return typeof content === 'string' && content.startsWith('Context pack candidate prepared.');
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
    'Checkpoint created.',
    `Label: ${label}`,
    'Product checkpoint only. No internal anchor was created.',
    'No raw evidence stored.',
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

function createContextPackCandidateContent(contextPackId) {
  return [
    'Context pack candidate prepared.',
    'Summary:',
    ...contextPackCandidateSummaryLines.map((line) => `- ${line}`),
    '',
    'Placeholder only. No raw evidence included. No .pi/rck read.',
    `Context pack ID: ${contextPackId}`,
  ].join('\n');
}

function createContextPackResultContent(contextPackId, status) {
  const prefix = status === 'cancelled' ? contextPackCancelledMessage : contextPackInjectedMessage;
  const statusLine = status === 'cancelled' ? 'Status: cancelled.' : 'Status: injected.';

  return [prefix, statusLine, `Context pack ID: ${contextPackId}`, 'No raw evidence included.', 'No .pi/rck read.'].join('\n');
}

function isContextPackCandidateMessage(message) {
  return getMessageContextPackId(message) !== null && isContextPackCandidateContent(message?.content);
}

function createContextPackCandidateControls(chatId, contextPackId, status) {
  const container = document.createElement('div');
  container.className = 'context-pack-card__actions';

  if (status === 'injected' || status === 'cancelled') {
    const statusChip = document.createElement('span');
    statusChip.className = `context-pack-card__status context-pack-card__status--${status}`;
    statusChip.textContent = status === 'injected' ? 'Injected' : 'Cancelled';
    container.appendChild(statusChip);
    return container;
  }

  const injectButton = document.createElement('button');
  injectButton.type = 'button';
  injectButton.className = 'button button--primary button--compact context-pack-card__action';
  injectButton.textContent = 'Inject';
  injectButton.addEventListener('click', () => {
    finalizeContextPackCandidate(chatId, contextPackId, 'inject');
  });

  const cancelButton = document.createElement('button');
  cancelButton.type = 'button';
  cancelButton.className = 'button button--ghost button--compact context-pack-card__action';
  cancelButton.textContent = 'Cancel';
  cancelButton.addEventListener('click', () => {
    finalizeContextPackCandidate(chatId, contextPackId, 'cancel');
  });

  container.append(injectButton, cancelButton);
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

  return {
    id: projectId,
    name,
    repoPath: overrides.repoPath ?? null,
    chats: chats.map((chat) =>
      createChat(chat.title ?? 'New chat', chat.messages ?? [], {
        ...chat,
        id: chat.id,
        projectId,
      }),
    ),
    createdAt: timestamp,
    updatedAt: overrides.updatedAt ?? timestamp,
    ...overrides,
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

  return createProject(project?.name ?? 'New project', Array.isArray(project?.chats) ? project.chats.map((chat) => uiChatFromProductChat(chat, projectId)) : [], {
    id: projectId,
    repoPath: project?.repoPath ?? null,
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
      repoPath: project.repoPath ?? null,
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

function createProjectWithInitialChat(name) {
  return createProject(name, [createEmptyChat('New chat')]);
}

function selectProjectAndChat(projectId, chatId = null) {
  setCurrentSelection(projectId, chatId ?? undefined);
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

function createNewProject() {
  const name = getUniqueProjectName();
  const project = createProjectWithInitialChat(name);
  state.projects.push(project);
  selectProjectAndChat(project.id, project.chats[0]?.id ?? null);
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
  eyebrow.textContent = 'Context Pack candidate';

  const title = document.createElement('div');
  title.className = 'context-pack-card__title';
  title.textContent = status === 'candidate' ? 'Placeholder only' : status === 'injected' ? 'Injected' : 'Cancelled';

  const id = document.createElement('code');
  id.className = 'context-pack-card__id';
  id.textContent = contextPackId;

  const statusChip = createContextPackStatusChip(status);

  header.append(eyebrow, title, id, statusChip);

  const summary = document.createElement('p');
  summary.className = 'context-pack-card__summary';
  summary.textContent = contextPackCandidateSummaryLines.join('. ') + '.';

  const safety = document.createElement('ul');
  safety.className = 'context-pack-card__safety';
  for (const line of contextPackCandidateSafetyLines) {
    const item = document.createElement('li');
    item.textContent = line;
    safety.appendChild(item);
  }

  const footer = document.createElement('div');
  footer.className = 'context-pack-card__footer';
  footer.textContent = 'User approval required before injection.';

  card.append(header, summary, safety, footer, createContextPackCandidateControls(chatId, contextPackId, status));
  return card;
}

function createMessageElement(message, contextPackLifecycleById = new Map(), chat = null) {
  const role = message.role;
  const text = message.content ?? message.text ?? '';
  const variant = message.variant ?? 'normal';
  const messageEl = document.createElement('article');
  messageEl.className = `message message--${role}${variant === 'error' ? ' message--error' : ''}`;

  const roleLabel = document.createElement('span');
  roleLabel.className = 'message__role';
  roleLabel.textContent = role === 'assistant' ? 'Assistant' : 'You';

  const body = document.createElement('div');
  body.className = 'message__body';

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
  } else {
    if (checkpointId && role === 'assistant' && message.kind === 'command-result') {
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
    messagesInnerEl.appendChild(createMessageElement(message, contextPackLifecycleById, chat));
  }

  if (autoScroll) {
    scrollChatToBottom();
  }
}

function render() {
  renderHeader();
  renderSidebar();
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
  const lines = commandCatalog.map((command) => `- ${command.usage} — ${command.kind} — ${command.description}`);
  return `Available RufusChat commands:\n${lines.join('\n')}`;
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

  return [
    'Status',
    `Memory: ${memoryLabel}`,
    `Context: ${contextLabel}`,
    `Trace: ${traceLabel}`,
    `Session: ${runtimeLabel}`,
  ].join('\n');
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
  const chat = getChatById(targetChatId);
  if (!chat) {
    return;
  }

  const historyItem = upsertChatInjectionHistoryItem(chat, contextPackId, {
    status: decision === 'inject' ? 'injected' : 'cancelled',
    injectedAt: decision === 'inject' ? nowIso() : null,
    cancelledAt: decision === 'cancel' ? nowIso() : null,
  });

  if (decision === 'inject') {
    const message = appendMessageToChat(targetChatId, 'assistant', createContextPackResultContent(contextPackId, 'injected'), 'normal', {
      kind: 'command-result',
      links: { contextPackId },
    });
    historyItem.resultMessageId = message?.id ?? null;
    historyItem.updatedAt = nowIso();
    touchChat(chat);
    touchProject(getProjectByChatId(chat.id));
    markProductStateChanged();
    render();
    return;
  }

  const message = appendMessageToChat(targetChatId, 'assistant', createContextPackResultContent(contextPackId, 'cancelled'), 'normal', {
    kind: 'command-result',
    links: { contextPackId },
  });
  historyItem.resultMessageId = message?.id ?? null;
  historyItem.updatedAt = nowIso();
  touchChat(chat);
  touchProject(getProjectByChatId(chat.id));
  markProductStateChanged();
  render();
}

async function runInject(targetChatId) {
  const contextPackId = makeContextPackId();
  const chat = getChatById(targetChatId);
  if (!chat) {
    return;
  }

  const historyItem = upsertChatInjectionHistoryItem(chat, contextPackId, {
    status: 'candidate',
    injectedAt: null,
    cancelledAt: null,
    resultMessageId: null,
  });

  const candidateMessage = appendMessageToChat(targetChatId, 'assistant', createContextPackCandidateContent(contextPackId), 'normal', {
    kind: 'placeholder',
    links: { contextPackId },
  });

  historyItem.candidateMessageId = candidateMessage?.id ?? null;
  historyItem.updatedAt = nowIso();
  touchChat(chat);
  touchProject(getProjectByChatId(chat.id));
  markProductStateChanged();
  render();
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
    showLocalFallback(targetChatId, text);
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
    return;
  }

  composerInput.value = '';
  hideSlashMenu();
  void handleUserSubmission(text);
  composerInput.focus();
});

composerInput.addEventListener('input', () => {
  slashMenuSuppressed = false;
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

document.addEventListener('keydown', (event) => {
  if (!confirmResolver) {
    return;
  }

  if (event.key === 'Escape') {
    event.preventDefault();
    closeConfirm(false);
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
  renderSlashMenu();
  await hydrateRuntimeStatus();
  composerInput.focus();
  await hydrateProductState();
}

void bootstrapApp();
