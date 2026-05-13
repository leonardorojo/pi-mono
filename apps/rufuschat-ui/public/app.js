const messagesEl = document.getElementById('messages');
const composerForm = document.getElementById('composer-form');
const composerInput = document.getElementById('composer-input');
const sendButton = composerForm.querySelector('button[type="submit"]');
const currentProjectEl = document.getElementById('current-project');
const currentChatEl = document.getElementById('current-chat');
const memoryStatusEl = document.getElementById('current-memory-status');
const summaryStatusEl = document.getElementById('current-summary-status');
const rckTraceStatusEl = document.getElementById('current-rck-trace-status');
const traceChipEl = document.getElementById('current-trace-chip');
const statusPill = document.querySelector('.chat-header__status');
const confirmModal = document.getElementById('confirm-modal');
const confirmDescription = document.getElementById('confirm-modal-description');
const confirmCancelButton = document.getElementById('confirm-modal-cancel');
const confirmConfirmButton = document.getElementById('confirm-modal-confirm');
const projectTreeEl = document.getElementById('project-tree');
const newChatButton = document.getElementById('new-chat-button');

const idleStatusText = 'Browser-local session';
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
const localIntroMessage =
  'This chat is local. LLM and semantic memory are not connected yet. RCK tracking happens only when you confirm slash actions.';
const newChatAssistantMessage =
  'New local chat created. LLM and semantic memory are not connected yet.';
const traceLinkedPlaceholderMessage =
  'Trace linking is not connected yet. This chat is currently not linked to an RCK Trace. Future versions will link chats to RCK Core Trace DAGs.';
const traceLinkPlaceholderMessage =
  'Trace linking is a placeholder in 10E. No RCK trace was created or linked.';
const traceChipMessage = 'Trace linking is not connected yet.';

let confirmResolver = null;

function makeId(prefix) {
  const random = globalThis.crypto?.randomUUID?.();
  return `${prefix}-${random ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`}`;
}

function nowIso() {
  return new Date().toISOString();
}

function createMessage(role, text, variant = 'normal') {
  return {
    role,
    text,
    variant,
  };
}

function createChat(title, messages = [], overrides = {}) {
  const timestamp = nowIso();
  return {
    id: makeId('chat'),
    title,
    messages: messages.map((message) => createMessage(message.role, message.text, message.variant ?? 'normal')),
    createdAt: timestamp,
    updatedAt: timestamp,
    memoryStatus: memoryPlaceholder.memoryStatus,
    semanticSummaryStatus: memoryPlaceholder.semanticSummaryStatus,
    linkedRckTraceStatus: memoryPlaceholder.linkedRckTraceStatus,
    semanticSummaryPreview: memoryPlaceholder.semanticSummaryPreview,
    linkedRckTraceId: memoryPlaceholder.linkedRckTraceId,
    linkedRckTrace: { ...tracePlaceholder },
    ...overrides,
  };
}

function createProject(name, chats) {
  return {
    id: makeId('project'),
    name,
    chats,
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

const state = {
  projects: createInitialProjects(),
  currentProjectId: null,
  currentChatId: null,
};

state.currentProjectId = state.projects[0]?.id ?? null;
state.currentChatId = state.projects[0]?.chats[0]?.id ?? null;

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

function formatStatusLabel(status) {
  return (status ?? '').replace(/-/g, ' ');
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

function scrollToBottom() {
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

function setBusy(isBusy, label = 'Running...') {
  composerInput.disabled = isBusy;
  sendButton.disabled = isBusy;
  newChatButton.disabled = isBusy;
  statusPill.textContent = isBusy ? label : idleStatusText;
}

function renderHeader() {
  const project = getCurrentProject();
  const chat = getCurrentChat();
  const linkedRckTrace = getLinkedRckTrace(chat);

  currentProjectEl.textContent = project?.name ?? '—';
  currentChatEl.textContent = chat?.title ?? '—';
  memoryStatusEl.textContent = formatStatusLabel(chat?.memoryStatus ?? memoryPlaceholder.memoryStatus);
  summaryStatusEl.textContent = formatStatusLabel(chat?.semanticSummaryStatus ?? memoryPlaceholder.semanticSummaryStatus);
  rckTraceStatusEl.textContent = formatStatusLabel(linkedRckTrace.status ?? memoryPlaceholder.linkedRckTraceStatus);

  if (traceChipEl) {
    traceChipEl.textContent = `Trace: ${formatStatusLabel(linkedRckTrace.status ?? tracePlaceholder.status)}`;
    traceChipEl.title = `Provider: ${linkedRckTrace.provider} · Future: ${linkedRckTrace.futureProvider} · Mode: ${linkedRckTrace.mode}`;
    traceChipEl.setAttribute(
      'aria-label',
      `Trace: ${formatStatusLabel(linkedRckTrace.status ?? tracePlaceholder.status)}. Provider: ${linkedRckTrace.provider}. Future: ${linkedRckTrace.futureProvider}. Mode: ${linkedRckTrace.mode}.`,
    );
  }
}

function renderSidebar() {
  projectTreeEl.replaceChildren();

  for (const project of state.projects) {
    const section = document.createElement('section');
    section.className = 'project-group';
    if (project.id === state.currentProjectId) {
      section.classList.add('project-group--active');
    }

    const titleButton = document.createElement('button');
    titleButton.type = 'button';
    titleButton.className = 'project-group__title';
    titleButton.dataset.action = 'select-project';
    titleButton.dataset.projectId = project.id;
    titleButton.textContent = project.name;
    section.appendChild(titleButton);

    const children = document.createElement('div');
    children.className = 'project-group__children';

    for (const chat of project.chats) {
      const chatButton = document.createElement('button');
      chatButton.type = 'button';
      chatButton.className = 'chat-item';
      chatButton.dataset.action = 'select-chat';
      chatButton.dataset.projectId = project.id;
      chatButton.dataset.chatId = chat.id;
      chatButton.textContent = chat.title;

      if (project.id === state.currentProjectId && chat.id === state.currentChatId) {
        chatButton.classList.add('chat-item--active');
      }

      children.appendChild(chatButton);
    }

    section.appendChild(children);
    projectTreeEl.appendChild(section);
  }
}

function createMessageElement(role, text, variant = 'normal') {
  const message = document.createElement('article');
  message.className = `message message--${role}${variant === 'error' ? ' message--error' : ''}`;

  const roleLabel = document.createElement('span');
  roleLabel.className = 'message__role';
  roleLabel.textContent = role === 'assistant' ? 'Assistant' : 'You';

  const body = document.createElement('div');
  body.className = 'message__body';
  body.textContent = text;

  message.append(roleLabel, body);
  return message;
}

function renderMessages() {
  const chat = getCurrentChat();
  messagesEl.replaceChildren();

  if (!chat) {
    const emptyState = document.createElement('div');
    emptyState.className = 'empty-state';
    emptyState.textContent = 'Select a chat to start.';
    messagesEl.appendChild(emptyState);
    return;
  }

  if (chat.messages.length === 0) {
    const emptyState = document.createElement('div');
    emptyState.className = 'empty-state';
    emptyState.textContent = 'No messages yet. Start the conversation here.';
    messagesEl.appendChild(emptyState);
    scrollToBottom();
    return;
  }

  for (const message of chat.messages) {
    messagesEl.appendChild(createMessageElement(message.role, message.text, message.variant));
  }

  scrollToBottom();
}

function render() {
  renderHeader();
  renderSidebar();
  renderMessages();
}

function setCurrentSelection(projectId, chatId) {
  const project = getProjectById(projectId);
  if (!project) {
    return;
  }

  const nextChat = chatId ? project.chats.find((item) => item.id === chatId) : project.chats[0];
  if (!nextChat) {
    return;
  }

  state.currentProjectId = project.id;
  state.currentChatId = nextChat.id;
  render();
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
}

function appendMessageToChat(chatId, role, text, variant = 'normal') {
  const chat = getChatById(chatId);
  if (!chat) {
    return;
  }

  chat.messages.push(createMessage(role, text, variant));
  chat.updatedAt = nowIso();

  if (chat.id === state.currentChatId) {
    renderMessages();
  }
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
      const label = restTokens.join(' ').trim() || 'checkpoint-from-chat';
      return { type: 'checkpoint', label };
    }
    case '/inject':
      return { type: 'inject' };
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
  if (!confirmResolver) {
    return;
  }

  const resolve = confirmResolver;
  confirmResolver = null;
  confirmModal.classList.remove('is-open');
  confirmModal.hidden = true;
  setBusy(false);
  resolve(confirmed);
}

async function confirmAction(message) {
  return openConfirm(message);
}

async function runStatus(targetChatId) {
  setBusy(true);
  try {
    const data = await getJson('/api/status');
    appendMessageToChat(targetChatId, 'assistant', data.message ?? 'Status checked. Health: OK.');
  } catch (error) {
    appendMessageToChat(
      targetChatId,
      'assistant',
      `Status request failed. ${error instanceof Error ? error.message : 'Unknown error'}.`,
      'error',
    );
  } finally {
    setBusy(false);
  }
}

async function runCheckpoint(targetChatId, label) {
  if (!(await confirmAction(`Create checkpoint "${label}"?`))) {
    appendMessageToChat(targetChatId, 'assistant', 'Checkpoint cancelled.');
    return;
  }

  setBusy(true);
  try {
    const data = await postJson('/api/checkpoint', { label });
    appendMessageToChat(targetChatId, 'assistant', data.message ?? `Checkpoint created: ${label}.`);
  } catch (error) {
    appendMessageToChat(
      targetChatId,
      'assistant',
      `Checkpoint request failed. ${error instanceof Error ? error.message : 'Unknown error'}.`,
      'error',
    );
  } finally {
    setBusy(false);
  }
}

async function runInject(targetChatId) {
  if (!(await confirmAction('Inject safe context for this chat?'))) {
    appendMessageToChat(targetChatId, 'assistant', 'Inject cancelled.');
    return;
  }

  setBusy(true);
  try {
    const data = await postJson('/api/inject', {});
    appendMessageToChat(targetChatId, 'assistant', data.message ?? 'Safe context injected.');
  } catch (error) {
    appendMessageToChat(
      targetChatId,
      'assistant',
      `Inject request failed. ${error instanceof Error ? error.message : 'Unknown error'}.`,
      'error',
    );
  } finally {
    setBusy(false);
  }
}

async function runHermesFake(targetChatId, prompt) {
  if (!prompt) {
    appendMessageToChat(targetChatId, 'assistant', 'Hermes fake run requires a prompt.');
    return;
  }

  if (!(await confirmAction(`Run fake Hermes for: ${prompt}`))) {
    appendMessageToChat(targetChatId, 'assistant', 'Hermes fake run cancelled.');
    return;
  }

  setBusy(true);
  try {
    const data = await postJson('/api/hermes/fake', { prompt });
    appendMessageToChat(targetChatId, 'assistant', data.message ?? 'Hermes fake run completed.');
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
  );
}

function showLocalFallback(targetChatId, text) {
  appendMessageToChat(
    targetChatId,
    'assistant',
    text.startsWith('/')
      ? 'Command recognized locally. This browser-local chat keeps the response in the active chat.'
      : 'I received your message locally. This browser session does not use LLM or semantic memory yet.',
  );
}

async function handleUserSubmission(text) {
  const targetChatId = state.currentChatId;
  const chat = getChatById(targetChatId);
  if (!chat) {
    return;
  }

  appendMessageToChat(targetChatId, 'user', text);
  maybeRenameChatFromFirstUserMessage(chat, text);
  renderSidebar();
  renderHeader();

  const command = parseSlashCommand(text);

  if (command.type === 'plain') {
    showLocalFallback(targetChatId, text);
    return;
  }

  if (command.type === 'unknown') {
    appendMessageToChat(targetChatId, 'assistant', 'Command not recognized. Available commands: /status, /checkpoint, /inject, /hermes fake <prompt>.');
    return;
  }

  if (command.type === 'hermes-real') {
    appendMessageToChat(targetChatId, 'assistant', 'Hermes real is not connected in this UI. Use /hermes fake <prompt>.');
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
      await runCheckpoint(targetChatId, command.label);
      return;
    case 'inject':
      await runInject(targetChatId);
      return;
    case 'hermes-fake':
      await runHermesFake(targetChatId, command.prompt);
      return;
    default:
      appendMessageToChat(targetChatId, 'assistant', 'Command not recognized. Available commands: /status, /checkpoint, /inject, /hermes fake <prompt>.');
  }
}

function createNewChat() {
  const project = getCurrentProject();
  if (!project) {
    return;
  }

  const chat = createChat('New chat', [createMessage('assistant', newChatAssistantMessage)]);
  project.chats.push(chat);
  state.currentProjectId = project.id;
  state.currentChatId = chat.id;
  render();
  composerInput.focus();
}

composerForm.addEventListener('submit', (event) => {
  event.preventDefault();

  if (composerInput.disabled) {
    return;
  }

  const text = composerInput.value.trim();
  if (!text) {
    return;
  }

  composerInput.value = '';
  void handleUserSubmission(text);
  composerInput.focus();
});

composerInput.addEventListener('keydown', (event) => {
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
    setCurrentSelection(projectId);
    return;
  }

  if (action === 'select-chat' && chatId) {
    setCurrentSelection(projectId, chatId);
  }
});

newChatButton.addEventListener('click', createNewChat);

if (traceChipEl) {
  traceChipEl.addEventListener('click', () => {
    const chat = getCurrentChat();
    if (!chat) {
      return;
    }

    appendMessageToChat(chat.id, 'assistant', traceChipMessage);
  });
}

setBusy(false);
render();
composerInput.focus();
