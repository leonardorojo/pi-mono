const messagesEl = document.getElementById('messages');
const composerForm = document.getElementById('composer-form');
const composerInput = document.getElementById('composer-input');
const sendButton = composerForm.querySelector('button[type="submit"]');
const currentProjectEl = document.getElementById('current-project');
const currentChatEl = document.getElementById('current-chat');
const statusPill = document.querySelector('.chat-header__status');
const confirmModal = document.getElementById('confirm-modal');
const confirmDescription = document.getElementById('confirm-modal-description');
const confirmCancelButton = document.getElementById('confirm-modal-cancel');
const confirmConfirmButton = document.getElementById('confirm-modal-confirm');
const chatItems = Array.from(document.querySelectorAll('.chat-item'));
const projectGroupTitle = document.querySelector('.project-group__title');

const idleStatusText = 'Local-first skeleton';
const initialAssistantMessage =
  'I’m RufusChat. This chat is local-first. LLM integration is not connected yet. RCK tracking runs in the background when you explicitly use product actions like /checkpoint or /inject.';

let confirmResolver = null;

function createMessage(role, text, variant = 'normal') {
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

function scrollToBottom() {
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

function appendMessage(role, text, variant = 'normal') {
  messagesEl.appendChild(createMessage(role, text, variant));
  scrollToBottom();
}

function setBusy(isBusy, label = 'Running...') {
  composerInput.disabled = isBusy;
  sendButton.disabled = isBusy;
  statusPill.textContent = isBusy ? label : idleStatusText;
}

function setActiveChat(project, chat) {
  currentProjectEl.textContent = project;
  currentChatEl.textContent = chat;

  chatItems.forEach((item) => {
    item.classList.toggle('chat-item--active', item.dataset.project === project && item.dataset.chat === chat);
  });
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
  return await openConfirm(message);
}

async function runStatus() {
  setBusy(true);
  try {
    const data = await getJson('/api/status');
    appendMessage('assistant', data.message ?? 'Status checked. Health: OK.');
  } catch (error) {
    appendMessage('assistant', `Status request failed. ${error instanceof Error ? error.message : 'Unknown error'}.`, 'error');
  } finally {
    setBusy(false);
  }
}

async function runCheckpoint(label) {
  if (!(await confirmAction(`Create checkpoint "${label}"?`))) {
    appendMessage('assistant', 'Checkpoint cancelled.');
    return;
  }

  setBusy(true);
  try {
    const data = await postJson('/api/checkpoint', { label });
    appendMessage('assistant', data.message ?? `Checkpoint created: ${label}. RCK recorded this point.`);
  } catch (error) {
    appendMessage('assistant', `Checkpoint request failed. ${error instanceof Error ? error.message : 'Unknown error'}.`, 'error');
  } finally {
    setBusy(false);
  }
}

async function runInject() {
  if (!(await confirmAction('Inject safe context for this chat?'))) {
    appendMessage('assistant', 'Inject cancelled.');
    return;
  }

  setBusy(true);
  try {
    const data = await postJson('/api/inject', {});
    appendMessage('assistant', data.message ?? 'Safe context injected.');
  } catch (error) {
    appendMessage('assistant', `Inject request failed. ${error instanceof Error ? error.message : 'Unknown error'}.`, 'error');
  } finally {
    setBusy(false);
  }
}

async function runHermesFake(prompt) {
  if (!prompt) {
    appendMessage('assistant', 'Hermes fake run requires a prompt.');
    return;
  }

  if (!(await confirmAction(`Run fake Hermes for: ${prompt}`))) {
    appendMessage('assistant', 'Hermes fake run cancelled.');
    return;
  }

  setBusy(true);
  try {
    const data = await postJson('/api/hermes/fake', { prompt });
    appendMessage('assistant', data.message ?? 'Hermes fake run completed.');
  } catch (error) {
    appendMessage('assistant', `Hermes fake request failed. ${error instanceof Error ? error.message : 'Unknown error'}.`, 'error');
  } finally {
    setBusy(false);
  }
}

function showLocalFallback(text) {
  appendMessage(
    'assistant',
    text.startsWith('/') ? 'Command recognized locally. This skeleton only supports placeholder responses in 10A.' : 'LLM is not connected yet. I received your message locally.',
  );
}

async function handleUserSubmission(text) {
  appendMessage('user', text);

  const command = parseSlashCommand(text);

  if (command.type === 'plain') {
    showLocalFallback(text);
    return;
  }

  if (command.type === 'unknown') {
    appendMessage('assistant', 'Command not recognized. Available commands: /status, /checkpoint, /inject, /hermes fake <prompt>.');
    return;
  }

  if (command.type === 'hermes-real') {
    appendMessage('assistant', 'Hermes real is not connected in this UI. Use /hermes fake <prompt>.');
    return;
  }

  if (command.type === 'hermes-fake-missing-prompt') {
    appendMessage('assistant', 'Hermes fake run requires a prompt.');
    return;
  }

  switch (command.type) {
    case 'status':
      await runStatus();
      return;
    case 'checkpoint':
      await runCheckpoint(command.label);
      return;
    case 'inject':
      await runInject();
      return;
    case 'hermes-fake':
      await runHermesFake(command.prompt);
      return;
    default:
      appendMessage('assistant', 'Command not recognized. Available commands: /status, /checkpoint, /inject, /hermes fake <prompt>.');
  }
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

chatItems.forEach((item) => {
  item.addEventListener('click', () => {
    const project = item.dataset.project;
    const chat = item.dataset.chat;

    if (project && chat) {
      setActiveChat(project, chat);
    }
  });
});

projectGroupTitle?.addEventListener('click', () => {
  const expanded = projectGroupTitle.getAttribute('aria-expanded') === 'true';
  projectGroupTitle.setAttribute('aria-expanded', String(!expanded));
});

appendMessage('assistant', initialAssistantMessage);
setActiveChat('PI Agent', 'Branch · RufusChat Fase 10');
composerInput.focus();
