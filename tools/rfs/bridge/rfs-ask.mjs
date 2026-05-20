import { readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import jiti from 'jiti';

const helperDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(helperDir, '..', '..', '..');
const load = jiti(import.meta.url);

const { getEnvApiKey } = load(path.join(repoRoot, 'packages/ai/src/env-api-keys.ts'));
const { getModel } = load(path.join(repoRoot, 'packages/ai/src/models.ts'));
const { streamSimple } = load(path.join(repoRoot, 'packages/ai/src/stream.ts'));
const { getOAuthApiKey, getOAuthProvider } = load(path.join(repoRoot, 'packages/ai/src/oauth.ts'));

const DEFAULT_SYSTEM_PROMPT = 'You are Rufus CLI, a concise assistant running through Pi\'s LLM provider.';
const DEFAULT_PROVIDER = 'github-copilot';
const DEFAULT_MODEL_ID = 'gpt-5.4-mini';
const DEFAULT_SETTINGS_PATH = path.join(homedir(), '.pi', 'agent', 'settings.json');
const DEFAULT_AUTH_PATH = path.join(homedir(), '.pi', 'agent', 'auth.json');

function readJsonFile(filePath) {
  try {
    return JSON.parse(readFileSync(filePath, 'utf-8'));
  } catch {
    return null;
  }
}

function coerceString(value) {
  return typeof value === 'string' ? value.trim() : '';
}

function getConfiguredDefaults() {
  const settings = readJsonFile(DEFAULT_SETTINGS_PATH) ?? {};
  const settingsProvider = coerceString(settings.defaultProvider);
  const settingsModel = coerceString(settings.defaultModel);

  return {
    provider: coerceString(process.env.RUFUSCHAT_LLM_PROVIDER) || settingsProvider || DEFAULT_PROVIDER,
    modelId: coerceString(process.env.RUFUSCHAT_LLM_MODEL) || settingsModel || DEFAULT_MODEL_ID,
  };
}

function getMissingAuthMessage(provider, modelId) {
  if (provider === 'github-copilot') {
    return 'LLM provider is not configured. Check the Pi Agent GitHub Copilot authentication.';
  }

  return `LLM provider is not configured for ${provider}/${modelId}.`;
}

async function resolveStoredApiKey(provider) {
  const auth = readJsonFile(DEFAULT_AUTH_PATH);
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

  return resolveStoredApiKey(provider);
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

function formatError(error) {
  if (error instanceof Error) {
    return error.message;
  }

  return String(error);
}

function buildContext(prompt) {
  return {
    systemPrompt: DEFAULT_SYSTEM_PROMPT,
    messages: [
      {
        role: 'user',
        content: prompt,
        timestamp: Date.now(),
      },
    ],
  };
}

async function main() {
  const prompt = process.argv.slice(2).join(' ').trim();
  if (!prompt) {
    console.error('Missing prompt.');
    return 1;
  }

  const defaults = getConfiguredDefaults();
  const model = getModel(defaults.provider, defaults.modelId);

  if (!model) {
    console.error(`Configured model not found: ${defaults.provider}/${defaults.modelId}`);
    return 1;
  }

  const apiKey = await resolveApiKey(model.provider);
  if (!apiKey) {
    console.error(getMissingAuthMessage(model.provider, model.id));
    return 1;
  }

  const stream = streamSimple(model, buildContext(prompt), {
    apiKey,
    sessionId: 'rfs-ask',
  });

  let streamedText = '';
  let finalText = '';

  try {
    for await (const event of stream) {
      if (event.type === 'text_delta' && typeof event.delta === 'string' && event.delta) {
        process.stdout.write(event.delta);
        streamedText += event.delta;
        continue;
      }

      if (event.type === 'done') {
        finalText = extractAssistantText(event.message);
        if (!streamedText && finalText) {
          process.stdout.write(finalText);
        }
        continue;
      }

      if (event.type === 'error') {
        throw new Error(event.error?.errorMessage ?? 'The LLM request failed.');
      }
    }

    if (!streamedText && !finalText) {
      throw new Error('The language model returned no reply.');
    }

    if (streamedText || finalText) {
      process.stdout.write('\n');
    }

    return 0;
  } catch (error) {
    if (streamedText || finalText) {
      process.stdout.write('\n');
    }
    console.error(formatError(error));
    return 1;
  }
}

const exitCode = await main();
process.exitCode = exitCode;
