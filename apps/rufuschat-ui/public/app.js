const messagesEl = document.getElementById('messages');
const composerForm = document.getElementById('composer-form');
const composerInput = document.getElementById('composer-input');
const currentProjectEl = document.getElementById('current-project');
const currentChatEl = document.getElementById('current-chat');
const chatItems = Array.from(document.querySelectorAll('.chat-item'));
const projectGroupTitle = document.querySelector('.project-group__title');

const initialAssistantMessage =
  "I’m RufusChat. This chat is local-first. LLM integration is not connected yet. RCK tracking runs in the background when you explicitly use product actions like /checkpoint or /inject.";

const commandResponses = new Map([
  ['/checkpoint', 'Checkpoint command recognized. RCK checkpoint execution is not wired in 10A.'],
  ['/inject', 'Inject command recognized. Safe context injection is not wired in 10A.'],
  ['/status', 'Status command recognized. RCK status is not wired in this skeleton yet.'],
  ['/hermes', 'Hermes command recognized. Hermes execution is not wired in 10A.'],
]);

function createMessage(role, text) {
  const message = document.createElement('article');
  message.className = `message message--${role}`;

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

function appendMessage(role, text) {
  messagesEl.appendChild(createMessage(role, text));
  scrollToBottom();
}

function normalizeCommand(input) {
  return input.trim().split(/\s+/)[0].toLowerCase();
}

function handleCommand(text) {
  const command = normalizeCommand(text);
  return commandResponses.get(command) ?? 'Command recognized locally. This skeleton only supports placeholder responses in 10A.';
}

function setActiveChat(project, chat) {
  currentProjectEl.textContent = project;
  currentChatEl.textContent = chat;

  chatItems.forEach((item) => {
    item.classList.toggle('chat-item--active', item.dataset.project === project && item.dataset.chat === chat);
  });
}

composerForm.addEventListener('submit', (event) => {
  event.preventDefault();

  const text = composerInput.value.trim();
  if (!text) {
    return;
  }

  appendMessage('user', text);
  appendMessage('assistant', text.startsWith('/') ? handleCommand(text) : 'LLM is not connected yet. I received your message locally.');

  composerInput.value = '';
  composerInput.focus();
});

composerInput.addEventListener('keydown', (event) => {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault();
    composerForm.requestSubmit();
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
