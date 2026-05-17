import { createServer } from 'node:http';
import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { buildMockContextPackPreview } from './contextpack-preview-provider.mjs';
import { buildMockContextScopeSuggestion } from './context-scope-suggestion-provider.mjs';
import { completeChatCompletion, createChatCompletionStream } from './chat-completion-provider.mjs';
import {
  createChatCompletionErrorResponse,
  getChatCompletionErrorStatusCode,
} from './chat-completion-schema.mjs';
import { isProductStateValidationError, loadProductState, saveProductState } from './product-state-store.mjs';
import { createRuntimeStatus } from './runtime-status-provider.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repoRoot = path.resolve(__dirname, '..', '..');
const publicDir = path.join(__dirname, 'public');
const rckRoot = path.join(repoRoot, '.pi', 'rck');
const port = Number(process.env.PORT ?? process.argv[2] ?? 4173);

const mimeTypes = new Map([
  ['.html', 'text/html; charset=utf-8'],
  ['.css', 'text/css; charset=utf-8'],
  ['.js', 'application/javascript; charset=utf-8'],
  ['.mjs', 'application/javascript; charset=utf-8'],
  ['.json', 'application/json; charset=utf-8'],
  ['.svg', 'image/svg+xml; charset=utf-8'],
  ['.txt', 'text/plain; charset=utf-8'],
]);

const sessionState = {
  traceId: await resolveInitialTraceId(),
  safeContextAvailable: existsSync(path.join(rckRoot, 'indexes', 'latest-context-pack.json')),
  checkpointCount: 0,
  lastCheckpointLabel: null,
  hermesFakeRuns: 0,
};

async function resolveInitialTraceId() {
  const currentTrace = await readJsonIfExists(path.join(rckRoot, 'indexes', 'current-trace.json'));
  if (typeof currentTrace?.traceId === 'string' && currentTrace.traceId.trim()) {
    return currentTrace.traceId.trim();
  }

  return 'trace_local';
}

async function readJsonIfExists(absolutePath) {
  if (!existsSync(absolutePath)) {
    return null;
  }

  try {
    return JSON.parse(await readFile(absolutePath, 'utf8'));
  } catch {
    return null;
  }
}

function getContentType(filePath) {
  return mimeTypes.get(path.extname(filePath).toLowerCase()) ?? 'application/octet-stream';
}

function safeResolve(requestPath) {
  let decodedPath;

  try {
    decodedPath = decodeURIComponent(requestPath.replace(/\0/g, ''));
  } catch {
    return null;
  }

  const candidate = path.resolve(publicDir, `.${decodedPath}`);
  const relative = path.relative(publicDir, candidate);

  if (relative.startsWith('..') || path.isAbsolute(relative)) {
    return null;
  }

  return candidate;
}

async function serveFile(res, filePath) {
  try {
    const body = await readFile(filePath);
    res.writeHead(200, {
      'Content-Type': getContentType(filePath),
      'Cache-Control': 'no-store',
    });
    res.end(body);
  } catch {
    res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
    res.end('Not found');
  }
}

async function readRequestJson(req) {
  let body = '';

  for await (const chunk of req) {
    body += chunk;
  }

  if (!body.trim()) {
    return {};
  }

  try {
    return JSON.parse(body);
  } catch {
    throw new Error('Invalid JSON body');
  }
}

function sendJson(res, statusCode, payload) {
  res.writeHead(statusCode, {
    'Content-Type': 'application/json; charset=utf-8',
    'Cache-Control': 'no-store',
  });
  res.end(JSON.stringify(payload));
}

function sendSseEvent(res, event, data) {
  res.write(`event: ${event}\n`);
  res.write(`data: ${JSON.stringify(data)}\n\n`);
}

function extractAssistantText(message) {
  if (!message) {
    return '';
  }

  if (typeof message.content === 'string') {
    return message.content.trim();
  }

  if (Array.isArray(message.content)) {
    return message.content
      .map((part) => {
        if (!part || typeof part !== 'object') {
          return '';
        }

        if (typeof part.text === 'string') {
          return part.text;
        }

        if (typeof part.refusal === 'string') {
          return part.refusal;
        }

        return '';
      })
      .join('')
      .trim();
  }

  return '';
}

function createChatCompletionErrorPayload(error) {
  const payload = createChatCompletionErrorResponse(error);
  return {
    error: {
      message: payload.error.message,
      code: payload.error.code,
    },
  };
}

function buildStatusMessage() {
  return `Status checked. Health: OK. Current trace: ${sessionState.traceId}. Safe context: ${sessionState.safeContextAvailable ? 'available' : 'no'}.`;
}

function buildSafeContextSummary() {
  const parts = [];

  parts.push(`trace ${sessionState.traceId}`);
  parts.push(`checkpoints this session: ${sessionState.checkpointCount}`);
  parts.push('evidence refs: 0');

  if (sessionState.lastCheckpointLabel) {
    parts.push(`last checkpoint: ${sessionState.lastCheckpointLabel}`);
  }

  return parts.join('; ');
}

function truncatePrompt(prompt) {
  const compact = prompt.replace(/\s+/g, ' ').trim();
  if (compact.length <= 96) {
    return compact;
  }

  return `${compact.slice(0, 93)}...`;
}

function buildHermesFakeSummary(prompt) {
  return `Prompt accepted: "${truncatePrompt(prompt)}". Evidence refs: 0. Fake runs this session: ${sessionState.hermesFakeRuns}.`;
}

const server = createServer(async (req, res) => {
  if (!req.url) {
    res.writeHead(400, { 'Content-Type': 'text/plain; charset=utf-8' });
    res.end('Bad request');
    return;
  }

  const url = new URL(req.url, `http://${req.headers.host ?? 'localhost'}`);

  if (url.pathname === '/health') {
    sendJson(res, 200, { ok: true, app: 'rufuschat-ui', mode: 'skeleton' });
    return;
  }

  if (url.pathname === '/api/runtime-status') {
    if (req.method !== 'GET') {
      sendJson(res, 405, { error: 'Method not allowed' });
      return;
    }

    sendJson(res, 200, createRuntimeStatus());
    return;
  }

  if (url.pathname === '/api/chat/stream') {
    if (req.method !== 'POST') {
      sendJson(res, 405, { error: 'Method not allowed' });
      return;
    }

    let body;
    try {
      body = await readRequestJson(req);
    } catch (error) {
      sendJson(res, getChatCompletionErrorStatusCode(error), createChatCompletionErrorPayload(error));
      return;
    }

    const abortController = new AbortController();
    const onClose = () => abortController.abort();
    req.on('close', onClose);

    try {
      const { model, stream } = await createChatCompletionStream(body, { signal: abortController.signal });
      res.writeHead(200, {
        'Content-Type': 'text/event-stream; charset=utf-8',
        'Cache-Control': 'no-cache, no-transform',
        Connection: 'keep-alive',
        'X-Accel-Buffering': 'no',
      });
      res.write('\n');

      const metadata = {
        provider: `${model.provider}`,
        model: `${model.provider}/${model.id}`,
        createdAt: new Date().toISOString(),
      };

      sendSseEvent(res, 'start', { metadata });

      let finalMessage = null;
      for await (const event of stream) {
        if (event?.type === 'text_delta' && typeof event.delta === 'string' && event.delta) {
          sendSseEvent(res, 'delta', { text: event.delta });
          continue;
        }

        if (event?.type === 'done') {
          finalMessage = event.message;
          const text = extractAssistantText(finalMessage);
          sendSseEvent(res, 'done', {
            message: {
              role: 'assistant',
              content: text,
            },
            metadata,
          });
          res.end();
          return;
        }

        if (event?.type === 'error') {
          const errorPayload = createChatCompletionErrorPayload(event.error ?? event);
          sendSseEvent(res, 'error', errorPayload);
          res.end();
          return;
        }
      }

      sendSseEvent(res, 'error', { error: { message: 'The stream ended without a final assistant response.', code: 'llm_empty_response' } });
      res.end();
    } catch (error) {
      res.writeHead(200, {
        'Content-Type': 'text/event-stream; charset=utf-8',
        'Cache-Control': 'no-cache, no-transform',
        Connection: 'keep-alive',
        'X-Accel-Buffering': 'no',
      });
      sendSseEvent(res, 'error', createChatCompletionErrorPayload(error));
      res.end();
    }
    return;
  }

  if (url.pathname === '/api/chat/complete') {
    if (req.method !== 'POST') {
      sendJson(res, 405, { error: 'Method not allowed' });
      return;
    }

    try {
      const body = await readRequestJson(req);
      const response = await completeChatCompletion(body);
      sendJson(res, 200, response);
    } catch (error) {
      const statusCode = getChatCompletionErrorStatusCode(error);
      sendJson(res, statusCode, createChatCompletionErrorResponse(error));
    }
    return;
  }

  if (url.pathname === '/api/status') {
    if (req.method !== 'GET') {
      sendJson(res, 405, { ok: false, error: 'Method not allowed' });
      return;
    }

    sendJson(res, 200, {
      ok: true,
      traceId: sessionState.traceId,
      safeContextAvailable: sessionState.safeContextAvailable,
      message: buildStatusMessage(),
    });
    return;
  }

  if (url.pathname === '/api/product-state') {
    if (req.method === 'GET') {
      try {
        const state = await loadProductState();
        sendJson(res, 200, { ok: true, state });
      } catch {
        sendJson(res, 500, { ok: false, message: 'Unable to load product state.' });
      }

      return;
    }

    if (req.method === 'PUT') {
      try {
        const body = await readRequestJson(req);
        const state = await saveProductState(body);
        sendJson(res, 200, { ok: true, state });
      } catch (error) {
        if (error instanceof Error && error.message === 'Invalid JSON body') {
          sendJson(res, 400, {
            ok: false,
            message: 'Invalid product state.',
            issues: ['Request body must be valid JSON.'],
          });
          return;
        }

        if (isProductStateValidationError(error)) {
          sendJson(res, 400, {
            ok: false,
            message: 'Invalid product state.',
            issues: error.issues ?? [],
          });
          return;
        }

        sendJson(res, 500, { ok: false, message: 'Unable to save product state.' });
      }

      return;
    }

    sendJson(res, 405, { ok: false, error: 'Method not allowed' });
    return;
  }

  if (url.pathname === '/api/checkpoint') {
    if (req.method !== 'POST') {
      sendJson(res, 405, { ok: false, error: 'Method not allowed' });
      return;
    }

    try {
      const body = await readRequestJson(req);
      const label = typeof body.label === 'string' && body.label.trim() ? body.label.trim() : 'checkpoint-from-chat';

      sessionState.checkpointCount += 1;
      sessionState.lastCheckpointLabel = label;
      sessionState.safeContextAvailable = true;

      sendJson(res, 200, {
        ok: true,
        traceId: sessionState.traceId,
        safeContextAvailable: sessionState.safeContextAvailable,
        label,
        message: `Checkpoint created: ${label}. RCK recorded this point.`,
      });
    } catch (error) {
      sendJson(res, 400, {
        ok: false,
        error: error instanceof Error ? error.message : 'Checkpoint request failed',
      });
    }
    return;
  }

  if (url.pathname === '/api/inject') {
    if (req.method !== 'POST') {
      sendJson(res, 405, { ok: false, error: 'Method not allowed' });
      return;
    }

    sessionState.safeContextAvailable = true;

    sendJson(res, 200, {
      ok: true,
      traceId: sessionState.traceId,
      safeContextAvailable: sessionState.safeContextAvailable,
      summary: buildSafeContextSummary(),
      message: `Safe context injected. Summary: ${buildSafeContextSummary()}`,
    });
    return;
  }

  if (url.pathname === '/api/rck/context-scope/suggest-placeholder') {
    if (req.method !== 'POST') {
      sendJson(res, 405, { ok: false, error: 'Method not allowed' });
      return;
    }

    try {
      const body = await readRequestJson(req);
      const intent = typeof body.intent === 'string' ? body.intent.trim() : '';

      sendJson(res, 200, {
        ok: true,
        suggestion: buildMockContextScopeSuggestion({ userIntentText: intent }),
        message: 'Placeholder RCK scope suggestion returned without LLM, TraceSlice, or ContextPack generation.',
      });
    } catch (error) {
      sendJson(res, 400, {
        ok: false,
        error: error instanceof Error ? error.message : 'Scope suggestion request failed',
      });
    }
    return;
  }

  if (url.pathname === '/api/rck/context-pack/preview-placeholder') {
    if (req.method !== 'GET') {
      sendJson(res, 405, { ok: false, error: 'Method not allowed' });
      return;
    }

    sendJson(res, 200, {
      ok: true,
      preview: buildMockContextPackPreview(),
      message: 'Placeholder ContextPack preview returned without reading RCK Core.',
    });
    return;
  }

  if (url.pathname === '/api/hermes/fake') {
    if (req.method !== 'POST') {
      sendJson(res, 405, { ok: false, error: 'Method not allowed' });
      return;
    }

    try {
      const body = await readRequestJson(req);
      const prompt = typeof body.prompt === 'string' ? body.prompt.trim() : '';

      if (!prompt) {
        throw new Error('Prompt is required for fake Hermes runs');
      }

      sessionState.hermesFakeRuns += 1;

      sendJson(res, 200, {
        ok: true,
        traceId: sessionState.traceId,
        safeContextAvailable: sessionState.safeContextAvailable,
        promptLength: prompt.length,
        evidenceRefs: 0,
        message: `Hermes fake run completed. Summary: ${buildHermesFakeSummary(prompt)}`,
      });
    } catch (error) {
      sendJson(res, 400, {
        ok: false,
        error: error instanceof Error ? error.message : 'Hermes fake request failed',
      });
    }
    return;
  }

  const requestedPath = url.pathname === '/' ? '/index.html' : url.pathname;
  const filePath = safeResolve(requestedPath);

  if (!filePath) {
    res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
    res.end('Not found');
    return;
  }

  await serveFile(res, filePath);
});

server.listen(port, '127.0.0.1', () => {
  console.log(`RufusChat UI skeleton listening on http://127.0.0.1:${port}`);
});

function shutdown(signal) {
  server.close(() => {
    console.log(`RufusChat UI skeleton stopped (${signal})`);
    process.exit(0);
  });
}

process.on('SIGINT', () => shutdown('SIGINT'));
process.on('SIGTERM', () => shutdown('SIGTERM'));
