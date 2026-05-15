const DEFAULT_ERROR_CODE = 'llm_unavailable';
const DEFAULT_ERROR_MESSAGE = 'The RufusChat LLM is temporarily unavailable.';
const DEFAULT_PROVIDER = 'openai';
const DEFAULT_MODEL_ID = 'gpt-4.1-mini';

class ChatCompletionError extends Error {
  constructor(message, { code = DEFAULT_ERROR_CODE, statusCode = 503, cause = undefined } = {}) {
    super(message);
    this.name = 'ChatCompletionError';
    this.code = code;
    this.statusCode = statusCode;

    if (cause !== undefined) {
      this.cause = cause;
    }
  }
}

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function coerceString(value) {
  if (typeof value !== 'string') {
    return '';
  }

  return value.trim();
}

function coerceContent(value) {
  if (typeof value === 'string') {
    return value.trim();
  }

  if (!Array.isArray(value)) {
    return '';
  }

  return value
    .map((part) => {
      if (!isPlainObject(part) || part.type !== 'text' || typeof part.text !== 'string') {
        return '';
      }

      return part.text;
    })
    .join('')
    .trim();
}

function normalizeModelSpecifier(value) {
  const text = coerceString(value);

  if (!text) {
    return { provider: DEFAULT_PROVIDER, modelId: DEFAULT_MODEL_ID };
  }

  if (text.includes('/')) {
    const [provider, ...rest] = text.split('/');
    const modelId = rest.join('/').trim();
    const normalizedProvider = coerceString(provider) || DEFAULT_PROVIDER;

    return {
      provider: normalizedProvider,
      modelId: modelId || DEFAULT_MODEL_ID,
    };
  }

  return {
    provider: DEFAULT_PROVIDER,
    modelId: text,
  };
}

function normalizeMessage(input, index, issues) {
  const pathLabel = `messages[${index}]`;

  if (!isPlainObject(input)) {
    issues.push(`${pathLabel} must be an object.`);
    return null;
  }

  const role = coerceString(input.role);
  if (role !== 'user' && role !== 'assistant') {
    issues.push(`${pathLabel}.role must be "user" or "assistant".`);
    return null;
  }

  const content = coerceContent(input.content);
  if (!content) {
    issues.push(`${pathLabel}.content must be a non-empty string.`);
    return null;
  }

  return {
    role,
    content,
    timestamp: typeof input.timestamp === 'number' && Number.isFinite(input.timestamp) ? input.timestamp : Date.now(),
  };
}

function normalizeOptions(input) {
  if (!isPlainObject(input)) {
    return {};
  }

  const options = {};

  if (typeof input.model === 'string' && input.model.trim()) {
    options.model = input.model.trim();
  }

  if (typeof input.provider === 'string' && input.provider.trim()) {
    options.provider = input.provider.trim();
  }

  if (typeof input.temperature === 'number' && Number.isFinite(input.temperature)) {
    options.temperature = input.temperature;
  }

  if (typeof input.maxTokens === 'number' && Number.isFinite(input.maxTokens)) {
    options.maxTokens = input.maxTokens;
  }

  return options;
}

export function normalizeChatCompletionRequest(input) {
  const issues = [];
  const body = isPlainObject(input) ? input : {};
  const projectId = coerceString(body.projectId) || null;
  const chatId = coerceString(body.chatId) || null;
  const options = normalizeOptions(body.options);
  const modelSelection = normalizeModelSpecifier(options.model ?? process.env.RUFUSCHAT_LLM_MODEL);
  const provider = coerceString(options.provider) || coerceString(process.env.RUFUSCHAT_LLM_PROVIDER) || modelSelection.provider;
  const modelId = modelSelection.modelId;

  const messages = Array.isArray(body.messages)
    ? body.messages
        .map((message, index) => normalizeMessage(message, index, issues))
        .filter(Boolean)
    : [];

  if (!messages.some((message) => message.role === 'user')) {
    issues.push('messages must include at least one user message.');
  }

  return {
    projectId,
    chatId,
    provider,
    modelId,
    messages,
    options: {
      model: `${provider}/${modelId}`,
      temperature: options.temperature,
      maxTokens: options.maxTokens,
    },
    issues,
  };
}

function normalizeMetadata(input) {
  const metadata = isPlainObject(input) ? input : {};
  const createdAt = typeof metadata.createdAt === 'string' && metadata.createdAt.trim() ? metadata.createdAt.trim() : new Date().toISOString();
  const provider = typeof metadata.provider === 'string' && metadata.provider.trim() ? metadata.provider.trim() : 'pi-ai';
  const model = typeof metadata.model === 'string' && metadata.model.trim() ? metadata.model.trim() : 'unknown';

  return {
    provider,
    model,
    createdAt,
  };
}

export function normalizeChatCompletionResponse(input) {
  const body = isPlainObject(input) ? input : {};
  const message = isPlainObject(body.message) ? body.message : {};
  const content = coerceString(message.content);

  if (!content) {
    throw new ChatCompletionError('LLM response did not contain assistant text.', {
      code: 'llm_empty_response',
      statusCode: 502,
    });
  }

  return {
    message: {
      role: 'assistant',
      content,
    },
    metadata: normalizeMetadata(body.metadata),
  };
}

export function createChatCompletionErrorResponse(error) {
  const code = error instanceof ChatCompletionError && typeof error.code === 'string'
    ? error.code
    : error instanceof Error && error.message === 'Invalid JSON body'
      ? 'invalid_request'
      : DEFAULT_ERROR_CODE;
  const message = error instanceof ChatCompletionError
    ? error.message.trim()
    : error instanceof Error && error.message === 'Invalid JSON body'
      ? 'Request body must be valid JSON.'
      : DEFAULT_ERROR_MESSAGE;

  return {
    error: {
      message,
      code,
    },
  };
}

export function getChatCompletionErrorStatusCode(error) {
  if (error instanceof ChatCompletionError && Number.isInteger(error.statusCode)) {
    return error.statusCode;
  }

  if (error instanceof Error && error.message === 'Invalid JSON body') {
    return 400;
  }

  if (error instanceof Error && error.message === 'Invalid RufusChat chat completion request.') {
    return 400;
  }

  return 503;
}

export { ChatCompletionError, DEFAULT_ERROR_CODE, DEFAULT_ERROR_MESSAGE, DEFAULT_MODEL_ID, DEFAULT_PROVIDER };
