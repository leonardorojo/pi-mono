import { completeSimple } from '../../packages/ai/dist/stream.js';
import { getEnvApiKey } from '../../packages/ai/dist/env-api-keys.js';
import { getModel } from '../../packages/ai/dist/models.js';

import {
  ChatCompletionError,
  normalizeChatCompletionRequest,
  normalizeChatCompletionResponse,
} from './chat-completion-schema.mjs';

const DEFAULT_SYSTEM_PROMPT = 'You are RufusChat, a helpful assistant inside a local-first project chat.';

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
    content: message.content,
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

function resolveApiKey(provider) {
  return getEnvApiKey(provider) ?? '';
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
  const apiKey = resolveApiKey(model.provider);

  if (!apiKey) {
    throw new ChatCompletionError(`Missing API key for provider: ${model.provider}`, {
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
      provider: 'pi-ai',
      model: `${model.provider}/${model.id}`,
      createdAt: new Date().toISOString(),
    },
  });
}
