import { readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';

import { completeSimple } from '../../packages/ai/dist/stream.js';
import { getEnvApiKey } from '../../packages/ai/dist/env-api-keys.js';
import { getModel } from '../../packages/ai/dist/models.js';
import { getOAuthApiKey, getOAuthProvider } from '../../packages/ai/dist/oauth.js';

import {
  ChatCompletionError,
  normalizeChatCompletionRequest,
  normalizeChatCompletionResponse,
} from './chat-completion-schema.mjs';

const DEFAULT_SYSTEM_PROMPT = 'You are RufusChat, a helpful assistant inside a local-first project chat.';
const PI_AGENT_DIR = join(homedir(), '.pi', 'agent');
const PI_AUTH_PATH = join(PI_AGENT_DIR, 'auth.json');

function readJsonFile(path) {
  try {
    const text = readFileSync(path, 'utf-8');
    return JSON.parse(text);
  } catch {
    return null;
  }
}

function resolveSelection(request) {
  const provider = request.provider;
  const modelId = request.modelId;
  const model = getModel(provider, modelId);

  if (!model) {
    throw new ChatCompletionError(`Configured model not found: ${provider}/${modelId}`, {
      code: 'llm_unavailable',
      statusCode: 503,
    });
  }

  return model;
}

function getSessionId(request) {
  const pieces = [request.projectId, request.chatId].filter((value) => typeof value === 'string' && value.trim());

  if (pieces.length === 0) {
    return undefined;
  }

  return pieces.join(':');
}

function toLlmMessages(messages) {
  return messages.map((message) => ({
    role: message.role,
    content: [
      {
        type: 'text',
        text: message.content,
      },
    ],
    timestamp: message.timestamp,
  }));
}

function extractAssistantText(message) {
  if (!message || !Array.isArray(message.content)) {
    return '';
  }

  return message.content
    .map((block) => {
      if (!block || block.type !== 'text' || typeof block.text !== 'string') {
        return '';
      }

      return block.text;
    })
    .join('')
    .trim();
}

async function resolveStoredApiKey(provider) {
  const auth = readJsonFile(PI_AUTH_PATH);
  const credential = auth?.[provider];

  if (!credential || typeof credential !== 'object') {
    return '';
  }

  try {
    if (credential.type === 'api_key' && typeof credential.key === 'string') {
      return credential.key.trim();
    }

    if (credential.type === 'oauth' && getOAuthProvider(provider)) {
      const result = await getOAuthApiKey(provider, { [provider]: credential });
      return result?.apiKey ?? '';
    }
  } catch {
    return '';
  }

  return '';
}

async function resolveApiKey(provider) {
  const envKey = getEnvApiKey(provider);
  if (envKey) {
    return envKey;
  }

  const storedKey = await resolveStoredApiKey(provider);
  if (storedKey) {
    return storedKey;
  }

  return '';
}

function getMissingAuthMessage(provider, modelId) {
  if (provider === 'github-copilot') {
    return 'LLM provider is not configured. Check the Pi Agent GitHub Copilot authentication.';
  }

  return `LLM provider is not configured for ${provider}/${modelId}.`;
}

export async function completeChatCompletion(requestInput) {
  const request = normalizeChatCompletionRequest(requestInput);

  if (request.issues.length > 0) {
    throw new ChatCompletionError('Invalid RufusChat chat completion request.', {
      code: 'invalid_request',
      statusCode: 400,
    });
  }

  const model = resolveSelection(request);
  const apiKey = await resolveApiKey(model.provider);

  if (!apiKey) {
    throw new ChatCompletionError(getMissingAuthMessage(model.provider, model.id), {
      code: 'llm_unavailable',
      statusCode: 503,
    });
  }

  const assistantMessage = await completeSimple(
    model,
    {
      systemPrompt: DEFAULT_SYSTEM_PROMPT,
      messages: toLlmMessages(request.messages),
    },
    {
      apiKey,
      sessionId: getSessionId(request),
      temperature: request.options.temperature,
      maxTokens: request.options.maxTokens,
    },
  );

  if (!assistantMessage || assistantMessage.stopReason === 'error') {
    throw new ChatCompletionError(assistantMessage?.errorMessage || 'The LLM provider returned an error.', {
      code: 'llm_unavailable',
      statusCode: 503,
    });
  }

  const content = extractAssistantText(assistantMessage);
  if (!content) {
    throw new ChatCompletionError('LLM response did not contain assistant text.', {
      code: 'llm_empty_response',
      statusCode: 502,
    });
  }

  return normalizeChatCompletionResponse({
    message: {
      role: 'assistant',
      content,
    },
    metadata: {
      provider: model.provider,
      model: `${model.provider}/${model.id}`,
      createdAt: new Date().toISOString(),
    },
  });
}
