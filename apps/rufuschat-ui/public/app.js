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
const slashMenuEl = document.getElementById('slash-menu');
const newProjectButton = document.getElementById('new-project-button');
const newChatButton = document.getElementById('new-chat-button');
const projectContextMenuEl = document.getElementById('project-context-menu');
const chatContextMenuEl = document.getElementById('chat-context-menu');

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
const commandCatalog = [
  {
    name: '/status',
    kind: 'read-only',
    description: 'Check safe project/RCK status.',
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
    description: 'Inject safe context into this chat.',
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

function promptForName(message, defaultValue = '') {
  const response = window.prompt(message, defaultValue);
  if (response === null) {
    return null;
  }

  const value = response.trim();
  return value || null;
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

function getProjectIndexById(projectId) {
  return state.projects.findIndex((project) => project.id === projectId);
}

function getProjectByChatId(chatId) {
  return state.projects.find((project) => project.chats.some((chat) => chat.id === chatId)) ?? null;
}

function createDefaultChat() {
  return createChat('New chat', [createMessage('assistant', newChatAssistantMessage)]);
}

function createDefaultProject() {
  return createProject('New project', [createDefaultChat()]);
}

function ensureProjectListHasItems() {
  if (state.projects.length === 0) {
    const project = createDefaultProject();
    state.projects.push(project);
    return project;
  }

  return null;
}

function ensureSelection() {
  if (state.projects.length === 0) {
    const project = createDefaultProject();
    state.projects.push(project);
    state.currentProjectId = project.id;
    state.currentChatId = project.chats[0]?.id ?? null;
    return;
  }

  const project = getProjectById(state.currentProjectId) ?? state.projects[0];
  if (!project) {
    return;
  }

  let chat = getChatById(state.currentChatId);
  if (!chat || !project.chats.some((item) => item.id === chat?.id)) {
    chat = project.chats[0] ?? null;
  }

  if (!chat) {
    chat = createDefaultChat();
    project.chats.push(chat);
  }

  state.currentProjectId = project.id;
  state.currentChatId = chat.id;
}

function getProjectByIdOrDefault(projectId) {
  return getProjectById(projectId) ?? state.projects[0] ?? null;
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
  if (newProjectButton) {
    newProjectButton.disabled = isBusy;
  }
  if (newChatButton) {
    newChatButton.disabled = isBusy;
  }
  statusPill.textContent = isBusy ? label : idleStatusText;

  if (isBusy) {
    hideSlashMenu();
  }
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

    const header = document.createElement('div');
    header.className = 'project-group__header';

    const titleButton = document.createElement('button');
    titleButton.type = 'button';
    titleButton.className = 'project-group__title';
    titleButton.dataset.action = 'select-project';
    titleButton.dataset.projectId = project.id;
    titleButton.textContent = project.name;

    const projectMenuButton = document.createElement('button');
    projectMenuButton.type = 'button';
    projectMenuButton.className = 'project-group__menu';
    projectMenuButton.dataset.action = 'open-project-menu';
    projectMenuButton.dataset.projectId = project.id;
    projectMenuButton.textContent = '…';
    projectMenuButton.setAttribute('aria-label', `Project actions for ${project.name}`);

    header.append(titleButton, projectMenuButton);
    section.appendChild(header);

    const children = document.createElement('div');
    children.className = 'project-group__children';

    for (const chat of project.chats) {
      const chatRow = document.createElement('div');
      chatRow.className = 'chat-item-row';

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

      const chatMenuButton = document.createElement('button');
      chatMenuButton.type = 'button';
      chatMenuButton.className = 'chat-item__menu';
      chatMenuButton.dataset.action = 'open-chat-menu';
      chatMenuButton.dataset.projectId = project.id;
      chatMenuButton.dataset.chatId = chat.id;
      chatMenuButton.textContent = '…';
      chatMenuButton.setAttribute('aria-label', `Chat actions for ${chat.title}`);

      chatRow.append(chatButton, chatMenuButton);
      children.appendChild(chatRow);
    }

    section.appendChild(children);
    projectTreeEl.appendChild(section);
  }
}

let activeContextMenu = null;

function hideContextMenus() {
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

  activeContextMenu = { type: context.type, projectId: context.projectId, chatId: context.chatId, menuEl };
  positionContextMenu(menuEl, anchorEl);
}

function openProjectContextMenu(projectId, anchorEl) {
  renderContextMenu(
    projectContextMenuEl,
    [
      { action: 'rename-project', label: 'Rename project' },
      { action: 'new-chat', label: 'New chat' },
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
      { action: 'delete-chat', label: 'Delete chat', destructive: true },
    ],
    anchorEl,
    { type: 'chat', projectId, chatId },
  );
}

function createChatInProject(project, title = 'New chat', messages = [createMessage('assistant', newChatAssistantMessage)]) {
  const chat = createChat(title, messages);
  project.chats.push(chat);
  return chat;
}

function createProjectWithInitialChat(name) {
  return createProject(name, [createDefaultChat()]);
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
  render();
}

function createNewProject() {
  const name = promptForName('New project', 'New project');
  if (!name) {
    return;
  }

  const project = createProjectWithInitialChat(name);
  state.projects.push(project);
  selectProjectAndChat(project.id, project.chats[0]?.id ?? null);
}

function createNewChatForProject(projectId) {
  const project = getProjectById(projectId);
  if (!project) {
    return;
  }

  const chat = createChatInProject(project);
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

  const wasActive = project.id === state.currentProjectId;
  state.projects.splice(projectIndex, 1);

  if (state.projects.length === 0) {
    const fallbackProject = createProjectWithInitialChat('New project');
    state.projects.push(fallbackProject);
    state.currentProjectId = fallbackProject.id;
    state.currentChatId = fallbackProject.chats[0]?.id ?? null;
  } else if (wasActive) {
    const nextProject = state.projects[projectIndex] ?? state.projects[projectIndex - 1] ?? state.projects[0];
    state.currentProjectId = nextProject.id;
    state.currentChatId = nextProject.chats[0]?.id ?? createChatInProject(nextProject).id;
  }

  ensureSelection();
  render();
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

  if (project.chats.length === 0) {
    const fallbackChat = createDefaultChat();
    project.chats.push(fallbackChat);
    if (project.id === state.currentProjectId) {
      state.currentChatId = fallbackChat.id;
    }
  } else if (wasActive) {
    const nextChat = project.chats[chatIndex] ?? project.chats[chatIndex - 1] ?? project.chats[0];
    state.currentProjectId = project.id;
    state.currentChatId = nextChat.id;
  }

  ensureSelection();
  render();
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
      const label = restTokens.join(' ').trim() || 'checkpoint-from-chat';
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
    appendMessageToChat(targetChatId, 'assistant', 'Command not recognized. Available commands: /status, /checkpoint, /inject, /hermes fake <prompt>, /trace, /trace link, /help.');
    return;
  }

  if (command.type === 'hermes-real') {
    appendMessageToChat(targetChatId, 'assistant', 'Hermes real is not connected in this UI. Use /hermes fake <prompt>.');
    return;
  }

  if (command.type === 'help') {
    appendMessageToChat(targetChatId, 'assistant', buildHelpMessage());
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
    const fallbackProject = createDefaultProject();
    state.projects.push(fallbackProject);
    selectProjectAndChat(fallbackProject.id, fallbackProject.chats[0]?.id ?? null);
    return;
  }

  const chat = createChatInProject(project);
  selectProjectAndChat(project.id, chat.id);
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

if (newChatButton) {
  newChatButton.addEventListener('click', createNewChat);
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

if (traceChipEl) {
  traceChipEl.addEventListener('click', () => {
    const chat = getCurrentChat();
    if (!chat) {
      return;
    }

    appendMessageToChat(chat.id, 'assistant', traceChipMessage);
  });
}

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

setBusy(false);
render();
renderSlashMenu();
composerInput.focus();
