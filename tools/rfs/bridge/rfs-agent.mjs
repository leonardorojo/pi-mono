import { readFileSync, readdirSync, statSync } from 'node:fs';
import { homedir } from 'node:os';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import { Type } from 'typebox';
import { Agent } from '@earendil-works/pi-agent-core';
import { getEnvApiKey, getModel } from '@earendil-works/pi-ai';
import { getOAuthApiKey, getOAuthProvider } from '@earendil-works/pi-ai/oauth';

const helperDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(helperDir, '..', '..', '..');

const DEFAULT_SYSTEM_PROMPT = `You are Rufus CLI's headless agent for repository inspection.
Use only the provided read-only tools.
Do not modify files.
Do not open the Pi TUI.
Stay within the repository root.`;
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

function inferPreferredPath(task) {
  const match = task.match(/\b(?:inspect|list|read|open|show)\s+([A-Za-z0-9_.\/-]+)\b/i);
  return match?.[1] ?? '';
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

function formatRelativePath(absolutePath) {
  const relativePath = path.relative(repoRoot, absolutePath).split(path.sep).join('/');
  return relativePath || '.';
}

function resolveRepoPath(rawPath) {
  const resolved = path.resolve(repoRoot, rawPath || '.');
  const relativePath = path.relative(repoRoot, resolved);
  if (relativePath.startsWith('..') || path.isAbsolute(relativePath)) {
    throw new Error(`Path escapes repository root: ${rawPath}`);
  }

  return resolved;
}

function normalizeLineRange(offset, limit) {
  const startLine = Number.isFinite(offset) && offset > 0 ? Math.floor(offset) : 1;
  const lineLimit = Number.isFinite(limit) && limit > 0 ? Math.floor(limit) : 200;
  return { startLine, lineLimit };
}

function formatFileExcerpt(text, offset, limit) {
  const lines = text.split(/\r?\n/);
  const { startLine, lineLimit } = normalizeLineRange(offset, limit);
  const startIndex = Math.max(0, startLine - 1);
  const slice = lines.slice(startIndex, startIndex + lineLimit);
  const output = slice.map((line, index) => `${startLine + index}|${line}`).join('\n');
  const shown = slice.length;
  const remaining = lines.length - (startIndex + slice.length);
  if (remaining > 0) {
    return `${output}\n[Truncated: showing ${shown} of ${lines.length - startIndex} lines]`;
  }

  return output || '(empty file)';
}

function summarizeLines(text, maxLines = 4, maxChars = 240) {
  const lines = text.split(/\r?\n/).filter((line) => line.length > 0);
  if (lines.length === 0) {
    return '(empty)';
  }

  const preview = lines.slice(0, maxLines).join(' | ');
  const suffix = lines.length > maxLines ? ` (+${lines.length - maxLines} lines)` : '';
  const summary = `${preview}${suffix}`;
  return summary.length > maxChars ? `${summary.slice(0, maxChars - 1)}…` : summary;
}

function extractTextContent(result) {
  const content = result?.content;
  if (!Array.isArray(content)) {
    return '';
  }

  return content
    .map((block) => (block && block.type === 'text' && typeof block.text === 'string' ? block.text : ''))
    .join('\n')
    .trim();
}

function formatToolArgs(toolName, args, preferredPath) {
  if (toolName === 'list_directory') {
    const rawPath = coerceString(args?.path) || preferredPath || '.';
    const limit = Number.isFinite(args?.limit) ? ` limit=${Math.floor(args.limit)}` : '';
    return `path=${rawPath}${limit}`;
  }

  if (toolName === 'read_file') {
    const rawPath = coerceString(args?.path) || '(missing path)';
    const offset = Number.isFinite(args?.offset) ? ` offset=${Math.floor(args.offset)}` : '';
    const limit = Number.isFinite(args?.limit) ? ` limit=${Math.floor(args.limit)}` : '';
    return `path=${rawPath}${offset}${limit}`;
  }

  try {
    return JSON.stringify(args);
  } catch {
    return String(args);
  }
}

function formatToolResult(toolName, result, isError) {
  const text = extractTextContent(result);
  const fallback = typeof result?.message === 'string' ? result.message : String(result ?? '');
  const summary = text ? summarizeLines(text) : summarizeLines(fallback);
  return `${isError ? '(error) ' : ''}${toolName} => ${summary}`;
}

async function main() {
  const prompt = process.argv.slice(2).join(' ').trim();
  if (!prompt) {
    console.error('Missing task.');
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

  const preferredPath = inferPreferredPath(prompt);
  const systemPrompt = preferredPath
    ? `${DEFAULT_SYSTEM_PROMPT}\nPreferred inspection path: ${preferredPath}.`
    : DEFAULT_SYSTEM_PROMPT;

  const repoPathSchema = Type.String({ description: 'Path relative to the repository root' });
  const listDirectoryTool = {
    name: 'list_directory',
    label: 'list_directory',
    description: 'List a directory inside the repository root. Read-only.',
    parameters: Type.Object({
      path: Type.Optional(repoPathSchema),
      limit: Type.Optional(Type.Number({ description: 'Maximum number of entries to return' })),
    }),
    async execute(_toolCallId, params) {
      const dirPath = resolveRepoPath(params.path ?? preferredPath ?? '.');
      const stat = statSync(dirPath);
      if (!stat.isDirectory()) {
        throw new Error(`Not a directory: ${formatRelativePath(dirPath)}`);
      }

      const limit = Number.isFinite(params.limit) && params.limit > 0 ? Math.floor(params.limit) : 200;
      const entries = readdirSync(dirPath, { withFileTypes: true })
        .slice()
        .sort((a, b) => a.name.localeCompare(b.name, 'en', { sensitivity: 'base' }));
      const visibleEntries = entries.slice(0, limit).map((entry) => `${entry.name}${entry.isDirectory() ? '/' : ''}`);
      const output = visibleEntries.join('\n') || '(empty directory)';
      const remaining = entries.length - visibleEntries.length;
      const finalOutput = remaining > 0 ? `${output}\n[Truncated: showing ${visibleEntries.length} of ${entries.length} entries]` : output;

      return {
        content: [{ type: 'text', text: finalOutput }],
        details: {
          path: formatRelativePath(dirPath),
          entryCount: entries.length,
          limit,
          truncated: remaining > 0,
        },
      };
    },
  };

  const readFileTool = {
    name: 'read_file',
    label: 'read_file',
    description: 'Read a file inside the repository root. Read-only.',
    parameters: Type.Object({
      path: repoPathSchema,
      offset: Type.Optional(Type.Number({ description: '1-indexed line number to start from' })),
      limit: Type.Optional(Type.Number({ description: 'Maximum number of lines to return' })),
    }),
    async execute(_toolCallId, params) {
      const filePath = resolveRepoPath(params.path);
      const stat = statSync(filePath);
      if (!stat.isFile()) {
        throw new Error(`Not a file: ${formatRelativePath(filePath)}`);
      }

      const text = readFileSync(filePath, 'utf8');
      const output = formatFileExcerpt(text, params.offset, params.limit);
      return {
        content: [{ type: 'text', text: output }],
        details: {
          path: formatRelativePath(filePath),
          lines: text.split(/\r?\n/).length,
          offset: Number.isFinite(params.offset) && params.offset > 0 ? Math.floor(params.offset) : 1,
          limit: Number.isFinite(params.limit) && params.limit > 0 ? Math.floor(params.limit) : 200,
        },
      };
    },
  };

  const agent = new Agent({
    initialState: {
      systemPrompt,
      model,
      tools: [listDirectoryTool, readFileTool],
    },
    getApiKey: resolveApiKey,
    sessionId: 'rfs-agent',
    toolExecution: 'sequential',
  });

  let assistantTextPrinted = false;

  agent.subscribe((event) => {
    if (event.type === 'agent_start') {
      console.log(`[agent:start] ${summarizeLines(prompt, 1, 120)}`);
      return;
    }

    if (event.type === 'tool_execution_start') {
      console.log(`[tool:start] id=${event.toolCallId} name=${event.toolName} ${formatToolArgs(event.toolName, event.args, preferredPath)}`);
      return;
    }

    if (event.type === 'tool_execution_end') {
      console.log(`[tool:end] id=${event.toolCallId} name=${event.toolName} ${formatToolResult(event.toolName, event.result, event.isError)}`);
      return;
    }

    if (event.type === 'message_update' && event.message.role === 'assistant') {
      if (event.assistantMessageEvent.type === 'text_delta' && event.assistantMessageEvent.delta) {
        assistantTextPrinted = true;
        console.log(`[assistant] ${event.assistantMessageEvent.delta}`);
      }
      return;
    }

    if (event.type === 'message_end' && event.message.role === 'assistant') {
      if (assistantTextPrinted) {
        return;
      }

      const text = extractTextContent(event.message);
      if (!text) {
        return;
      }

      console.log(`[assistant] ${text}`);
      assistantTextPrinted = true;
      return;
    }

    if (event.type === 'agent_end') {
      console.log('[agent:end]');
    }
  });

  try {
    await agent.prompt(prompt);
    return 0;
  } catch (error) {
    if (assistantLineOpen) {
      process.stdout.write('\n');
    }
    console.error(error instanceof Error ? error.message : String(error));
    return 1;
  }
}

const exitCode = await main();
process.exitCode = exitCode;
