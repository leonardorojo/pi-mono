const allowedMessageRoles = new Set(['user', 'assistant', 'system', 'tool']);
const allowedChatKinds = new Set(['normal', 'phase', 'decision', 'debug']);
const allowedMessageKinds = new Set(['normal', 'command', 'command-result', 'error', 'placeholder']);
const allowedLinkedTraceStatuses = new Set(['not-linked', 'linked', 'placeholder']);
const allowedInjectionStatuses = new Set(['candidate', 'injected', 'cancelled', 'expired']);
const allowedInjectionSourceKinds = new Set(['placeholder', 'manual', 'rck', 'future']);
const allowedCheckpointStatuses = new Set(['created', 'superseded', 'archived']);
const allowedCheckpointSourceKinds = new Set(['product', 'manual', 'future-rck']);

export class ProductStateError extends Error {
  constructor(message, { code = 'PRODUCT_STATE_ERROR', issues = [], cause = undefined } = {}) {
    super(message);
    this.name = 'ProductStateError';
    this.code = code;
    this.issues = issues;

    if (cause !== undefined) {
      this.cause = cause;
    }
  }
}

export function nowIsoString() {
  return new Date().toISOString();
}

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function isNonEmptyString(value) {
  return typeof value === 'string' && value.trim().length > 0;
}

function normalizeTimestamp(value, fallback) {
  return typeof value === 'string' && value.trim() ? value.trim() : fallback;
}

function normalizeRepositoryPath(value) {
  return typeof value === 'string' ? value.trim() : '';
}

function defaultLinkedRckTrace() {
  return {
    status: 'not-linked',
    traceId: null,
    provider: 'pi-rck-bridge',
    futureProvider: 'rck-core-kernel',
    mode: 'placeholder',
  };
}

function normalizeLinkedRckTrace(input, pathLabel, now, issues) {
  if (input === undefined || input === null) {
    return defaultLinkedRckTrace();
  }

  if (!isPlainObject(input)) {
    issues.push(`${pathLabel} must be an object.`);
    return defaultLinkedRckTrace();
  }

  const trace = { ...input };

  if (!allowedLinkedTraceStatuses.has(trace.status)) {
    issues.push(`${pathLabel}.status must be one of: not-linked, linked, placeholder.`);
    trace.status = 'not-linked';
  }

  if (trace.traceId !== null && typeof trace.traceId !== 'string') {
    issues.push(`${pathLabel}.traceId must be a string or null.`);
    trace.traceId = null;
  }

  if (trace.provider !== 'pi-rck-bridge') {
    issues.push(`${pathLabel}.provider must be "pi-rck-bridge".`);
    trace.provider = 'pi-rck-bridge';
  }

  if (trace.futureProvider !== 'rck-core-kernel') {
    issues.push(`${pathLabel}.futureProvider must be "rck-core-kernel".`);
    trace.futureProvider = 'rck-core-kernel';
  }

  if (trace.mode !== 'placeholder') {
    issues.push(`${pathLabel}.mode must be "placeholder".`);
    trace.mode = 'placeholder';
  }

  void now;
  return trace;
}

function normalizeLinks(input, pathLabel, issues) {
  if (input === undefined || input === null) {
    return null;
  }

  if (!isPlainObject(input)) {
    issues.push(`${pathLabel} must be an object.`);
    return null;
  }

  const links = { ...input };
  const fields = ['rckTraceId', 'contextPackId', 'checkpointId'];

  for (const field of fields) {
    if (links[field] === undefined) {
      continue;
    }

    if (links[field] !== null && typeof links[field] !== 'string') {
      issues.push(`${pathLabel}.${field} must be a string or null.`);
      links[field] = null;
    }
  }

  return links;
}

function normalizeInjectionHistoryItem(input, projectIndex, chatIndex, itemIndex, now, issues) {
  const pathLabel = `projects[${projectIndex}].chats[${chatIndex}].injections[${itemIndex}]`;

  if (!isPlainObject(input)) {
    issues.push(`${pathLabel} must be an object.`);
    return null;
  }

  const item = { ...input };

  if (!isNonEmptyString(item.contextPackId)) {
    issues.push(`${pathLabel}.contextPackId must be a non-empty string.`);
    return null;
  }

  item.contextPackId = item.contextPackId.trim();

  if (!allowedInjectionStatuses.has(item.status)) {
    issues.push(`${pathLabel}.status must be one of: candidate, injected, cancelled, expired.`);
    item.status = 'candidate';
  }

  if (!isNonEmptyString(item.title)) {
    issues.push(`${pathLabel}.title must be a non-empty string.`);
    item.title = 'Context pack candidate';
  } else {
    item.title = item.title.trim();
  }

  if (typeof item.summary !== 'string') {
    issues.push(`${pathLabel}.summary must be a string.`);
    item.summary = 'Current chat context placeholder. ProductState metadata placeholder. No RCK evidence included.';
  }

  item.createdAt = normalizeTimestamp(item.createdAt, now);
  item.updatedAt = normalizeTimestamp(item.updatedAt, item.createdAt);

  if (item.injectedAt !== undefined && item.injectedAt !== null && typeof item.injectedAt !== 'string') {
    issues.push(`${pathLabel}.injectedAt must be a string or null.`);
    item.injectedAt = null;
  }

  if (item.cancelledAt !== undefined && item.cancelledAt !== null && typeof item.cancelledAt !== 'string') {
    issues.push(`${pathLabel}.cancelledAt must be a string or null.`);
    item.cancelledAt = null;
  }

  if (item.candidateMessageId !== undefined && item.candidateMessageId !== null && typeof item.candidateMessageId !== 'string') {
    issues.push(`${pathLabel}.candidateMessageId must be a string or null.`);
    item.candidateMessageId = null;
  }

  if (item.resultMessageId !== undefined && item.resultMessageId !== null && typeof item.resultMessageId !== 'string') {
    issues.push(`${pathLabel}.resultMessageId must be a string or null.`);
    item.resultMessageId = null;
  }

  if (!allowedInjectionSourceKinds.has(item.sourceKind)) {
    issues.push(`${pathLabel}.sourceKind must be one of: placeholder, manual, rck, future.`);
    item.sourceKind = 'placeholder';
  }

  if (item.safeMetadata !== undefined && item.safeMetadata !== null && !isPlainObject(item.safeMetadata)) {
    issues.push(`${pathLabel}.safeMetadata must be an object or null.`);
    item.safeMetadata = null;
  }

  return item;
}

function normalizeCheckpointHistoryItem(input, projectIndex, chatIndex, itemIndex, now, issues) {
  const pathLabel = `projects[${projectIndex}].chats[${chatIndex}].checkpoints[${itemIndex}]`;

  if (!isPlainObject(input)) {
    issues.push(`${pathLabel} must be an object.`);
    return null;
  }

  const item = { ...input };

  if (!isNonEmptyString(item.checkpointId)) {
    issues.push(`${pathLabel}.checkpointId must be a non-empty string.`);
    return null;
  }

  item.checkpointId = item.checkpointId.trim();

  if (!allowedCheckpointStatuses.has(item.status)) {
    issues.push(`${pathLabel}.status must be one of: created, superseded, archived.`);
    item.status = 'created';
  }

  if (!isNonEmptyString(item.label)) {
    issues.push(`${pathLabel}.label must be a non-empty string.`);
    item.label = 'Untitled checkpoint';
  } else {
    item.label = item.label.trim();
  }

  if (typeof item.summary !== 'string') {
    issues.push(`${pathLabel}.summary must be a string.`);
    item.summary = 'Product checkpoint only. No RCK anchor was created. No raw evidence stored.';
  }

  item.createdAt = normalizeTimestamp(item.createdAt, now);
  item.updatedAt = normalizeTimestamp(item.updatedAt, item.createdAt);

  if (item.sourceMessageId !== undefined && item.sourceMessageId !== null && typeof item.sourceMessageId !== 'string') {
    issues.push(`${pathLabel}.sourceMessageId must be a string or null.`);
    item.sourceMessageId = null;
  }

  if (item.resultMessageId !== undefined && item.resultMessageId !== null && typeof item.resultMessageId !== 'string') {
    issues.push(`${pathLabel}.resultMessageId must be a string or null.`);
    item.resultMessageId = null;
  }

  if (!allowedCheckpointSourceKinds.has(item.sourceKind)) {
    issues.push(`${pathLabel}.sourceKind must be one of: product, manual, future-rck.`);
    item.sourceKind = 'product';
  }

  if (item.safeMetadata !== undefined && item.safeMetadata !== null && !isPlainObject(item.safeMetadata)) {
    issues.push(`${pathLabel}.safeMetadata must be an object or null.`);
    item.safeMetadata = null;
  }

  return item;
}

function normalizeMessage(input, projectIndex, chatIndex, messageIndex, now, issues) {
  const pathLabel = `projects[${projectIndex}].chats[${chatIndex}].messages[${messageIndex}]`;

  if (!isPlainObject(input)) {
    issues.push(`${pathLabel} must be an object.`);
    return {
      id: `message-${projectIndex}-${chatIndex}-${messageIndex}`,
      role: 'user',
      content: '',
      createdAt: now,
      kind: 'normal',
      command: null,
      safeMetadata: null,
      links: null,
    };
  }

  const message = { ...input };

  if (!isNonEmptyString(message.id)) {
    issues.push(`${pathLabel}.id must be a non-empty string.`);
    message.id = `message-${projectIndex}-${chatIndex}-${messageIndex}`;
  } else {
    message.id = message.id.trim();
  }

  if (!allowedMessageRoles.has(message.role)) {
    issues.push(`${pathLabel}.role must be one of: user, assistant, system, tool.`);
    message.role = 'user';
  }

  if (typeof message.content !== 'string') {
    issues.push(`${pathLabel}.content must be a string.`);
    message.content = '';
  }

  message.createdAt = normalizeTimestamp(message.createdAt, now);

  if (message.kind !== undefined && message.kind !== null && !allowedMessageKinds.has(message.kind)) {
    issues.push(`${pathLabel}.kind must be one of: normal, command, command-result, error, placeholder.`);
    message.kind = 'normal';
  }

  if (message.command !== undefined && message.command !== null && typeof message.command !== 'string') {
    issues.push(`${pathLabel}.command must be a string or null.`);
    message.command = null;
  }

  if (message.safeMetadata !== undefined && message.safeMetadata !== null && !isPlainObject(message.safeMetadata)) {
    issues.push(`${pathLabel}.safeMetadata must be an object or null.`);
    message.safeMetadata = null;
  }

  message.links = normalizeLinks(message.links, `${pathLabel}.links`, issues);
  return message;
}

function normalizeChat(input, projectIndex, chatIndex, now, issues, projectIdFallback) {
  const pathLabel = `projects[${projectIndex}].chats[${chatIndex}]`;

  if (!isPlainObject(input)) {
    issues.push(`${pathLabel} must be an object.`);
    return {
      id: `chat-${projectIndex}-${chatIndex}`,
      projectId: projectIdFallback,
      title: 'New chat',
      kind: 'normal',
      messages: [],
      createdAt: now,
      updatedAt: now,
      memoryStatus: 'not-linked',
      semanticSummaryStatus: 'not-generated',
      semanticSummaryPreview: null,
      linkedRckTraceStatus: 'not-linked',
      linkedRckTrace: defaultLinkedRckTrace(),
      injections: [],
      checkpoints: [],
    };
  }

  const chat = { ...input };

  if (!isNonEmptyString(chat.id)) {
    issues.push(`${pathLabel}.id must be a non-empty string.`);
    chat.id = `chat-${projectIndex}-${chatIndex}`;
  } else {
    chat.id = chat.id.trim();
  }

  if (!isNonEmptyString(chat.projectId)) {
    issues.push(`${pathLabel}.projectId must be a non-empty string.`);
    chat.projectId = projectIdFallback;
  } else {
    chat.projectId = chat.projectId.trim();
  }

  if (!isNonEmptyString(chat.title)) {
    issues.push(`${pathLabel}.title must be a non-empty string.`);
    chat.title = 'New chat';
  } else {
    chat.title = chat.title.trim();
  }

  if (!allowedChatKinds.has(chat.kind)) {
    issues.push(`${pathLabel}.kind must be one of: normal, phase, decision, debug.`);
    chat.kind = 'normal';
  }

  if (!Array.isArray(chat.messages)) {
    issues.push(`${pathLabel}.messages must be an array.`);
    chat.messages = [];
  }

  chat.createdAt = normalizeTimestamp(chat.createdAt, now);
  chat.updatedAt = normalizeTimestamp(chat.updatedAt, now);

  chat.memoryStatus = typeof chat.memoryStatus === 'string' ? chat.memoryStatus : 'not-linked';
  chat.semanticSummaryStatus = typeof chat.semanticSummaryStatus === 'string' ? chat.semanticSummaryStatus : 'not-generated';

  if (chat.semanticSummaryPreview !== undefined && chat.semanticSummaryPreview !== null && typeof chat.semanticSummaryPreview !== 'string') {
    issues.push(`${pathLabel}.semanticSummaryPreview must be a string or null.`);
    chat.semanticSummaryPreview = null;
  }

  chat.linkedRckTraceStatus = typeof chat.linkedRckTraceStatus === 'string' ? chat.linkedRckTraceStatus : 'not-linked';
  chat.linkedRckTrace = normalizeLinkedRckTrace(chat.linkedRckTrace, `${pathLabel}.linkedRckTrace`, now, issues);

  if (!Array.isArray(chat.injections)) {
    if (chat.injections !== undefined) {
      issues.push(`${pathLabel}.injections must be an array.`);
    }

    chat.injections = [];
  }

  chat.injections = chat.injections
    .map((item, itemIndex) => normalizeInjectionHistoryItem(item, projectIndex, chatIndex, itemIndex, now, issues))
    .filter(Boolean);

  if (!Array.isArray(chat.checkpoints)) {
    if (chat.checkpoints !== undefined) {
      issues.push(`${pathLabel}.checkpoints must be an array.`);
    }

    chat.checkpoints = [];
  }

  chat.checkpoints = chat.checkpoints
    .map((item, itemIndex) => normalizeCheckpointHistoryItem(item, projectIndex, chatIndex, itemIndex, now, issues))
    .filter(Boolean);

  chat.messages = chat.messages.map((message, messageIndex) => normalizeMessage(message, projectIndex, chatIndex, messageIndex, now, issues));
  return chat;
}

function normalizeProject(input, projectIndex, now, issues) {
  const pathLabel = `projects[${projectIndex}]`;

  if (!isPlainObject(input)) {
    issues.push(`${pathLabel} must be an object.`);
    return {
      id: `project-${projectIndex}`,
      name: 'New project',
      repositoryPath: '',
      repoPath: '',
      chats: [],
      createdAt: now,
      updatedAt: now,
    };
  }

  const project = { ...input };

  if (!isNonEmptyString(project.id)) {
    issues.push(`${pathLabel}.id must be a non-empty string.`);
    project.id = `project-${projectIndex}`;
  } else {
    project.id = project.id.trim();
  }

  if (!isNonEmptyString(project.name)) {
    issues.push(`${pathLabel}.name must be a non-empty string.`);
    project.name = 'New project';
  } else {
    project.name = project.name.trim();
  }

  const rawRepositoryPath = project.repositoryPath !== undefined ? project.repositoryPath : project.repoPath;
  if (rawRepositoryPath !== undefined && rawRepositoryPath !== null && typeof rawRepositoryPath !== 'string') {
    issues.push(`${pathLabel}.repositoryPath must be a string or null.`);
    project.repositoryPath = '';
  }

  if (project.repoPath !== undefined && project.repoPath !== null && typeof project.repoPath !== 'string') {
    issues.push(`${pathLabel}.repoPath must be a string or null.`);
    project.repoPath = '';
  }

  if (!Array.isArray(project.chats)) {
    issues.push(`${pathLabel}.chats must be an array.`);
    project.chats = [];
  }

  project.createdAt = normalizeTimestamp(project.createdAt, now);
  project.updatedAt = normalizeTimestamp(project.updatedAt, now);
  project.repositoryPath = normalizeRepositoryPath(rawRepositoryPath);
  project.repoPath = project.repositoryPath;
  project.chats = project.chats.map((chat, chatIndex) => normalizeChat(chat, projectIndex, chatIndex, now, issues, project.id));
  return project;
}

export function normalizeProductState(input, { now = nowIsoString() } = {}) {
  if (!isPlainObject(input)) {
    throw new ProductStateError('Invalid product state.', {
      code: 'INVALID_PRODUCT_STATE',
      issues: ['State payload must be an object.'],
    });
  }

  const issues = [];
  const state = { ...input };

  if (typeof state.version !== 'string') {
    issues.push('version must be a string.');
    state.version = '0';
  } else {
    state.version = state.version.trim();
  }

  if (!Array.isArray(state.projects)) {
    issues.push('projects must be an array.');
    state.projects = [];
  }

  state.createdAt = normalizeTimestamp(state.createdAt, now);
  state.updatedAt = normalizeTimestamp(state.updatedAt, now);

  if (state.currentProjectId !== undefined && state.currentProjectId !== null && typeof state.currentProjectId !== 'string') {
    issues.push('currentProjectId must be a string or null.');
    state.currentProjectId = null;
  }

  if (state.currentChatId !== undefined && state.currentChatId !== null && typeof state.currentChatId !== 'string') {
    issues.push('currentChatId must be a string or null.');
    state.currentChatId = null;
  }

  state.projects = state.projects.map((project, projectIndex) => normalizeProject(project, projectIndex, now, issues));

  if (issues.length > 0) {
    throw new ProductStateError('Invalid product state.', {
      code: 'INVALID_PRODUCT_STATE',
      issues,
    });
  }

  return state;
}

export function createProductStateSeed(now = nowIsoString()) {
  const projectId = 'project-root';
  const chatId = 'chat-root';

  return {
    version: '0',
    projects: [
      {
        id: projectId,
        name: 'RufusChat',
        repositoryPath: '',
        repoPath: '',
        chats: [
          {
            id: chatId,
            projectId,
            title: 'Welcome',
            kind: 'normal',
            messages: [],
            createdAt: now,
            updatedAt: now,
            memoryStatus: 'not-linked',
            semanticSummaryStatus: 'not-generated',
            semanticSummaryPreview: null,
            linkedRckTraceStatus: 'not-linked',
            linkedRckTrace: defaultLinkedRckTrace(),
            checkpoints: [],
          },
        ],
        createdAt: now,
        updatedAt: now,
      },
    ],
    currentProjectId: projectId,
    currentChatId: chatId,
    createdAt: now,
    updatedAt: now,
  };
}
