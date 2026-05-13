import { existsSync } from 'node:fs';
import { mkdir, readFile, rename, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { createProductStateSeed, normalizeProductState, nowIsoString, ProductStateError } from './product-state-schema.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const dataDir = path.resolve(__dirname, '.data');
const productStatePath = path.join(dataDir, 'rufuschat-product-state.json');

function buildTempPath() {
  return `${productStatePath}.${process.pid}.${Date.now()}.tmp`;
}

function isSyntaxErrorLike(error) {
  return error instanceof SyntaxError || error?.name === 'SyntaxError';
}

async function writeJsonAtomically(absolutePath, payload) {
  await mkdir(path.dirname(absolutePath), { recursive: true });

  const tempPath = buildTempPath();
  const body = `${JSON.stringify(payload, null, 2)}\n`;

  try {
    await writeFile(tempPath, body, 'utf8');
    await rename(tempPath, absolutePath);
  } catch (error) {
    try {
      if (existsSync(tempPath)) {
        await import('node:fs/promises').then(({ unlink }) => unlink(tempPath));
      }
    } catch {
      // Best-effort cleanup only.
    }

    throw error;
  }
}

export function getProductStatePath() {
  return productStatePath;
}

export async function loadProductState() {
  if (!existsSync(productStatePath)) {
    const seed = createProductStateSeed(nowIsoString());
    await writeJsonAtomically(productStatePath, seed);
    return seed;
  }

  let rawText;

  try {
    rawText = await readFile(productStatePath, 'utf8');
  } catch (error) {
    throw new ProductStateError('Unable to load product state.', {
      code: 'PRODUCT_STATE_LOAD_FAILED',
      cause: error,
    });
  }

  let parsed;

  try {
    parsed = JSON.parse(rawText);
  } catch (error) {
    throw new ProductStateError('Unable to load product state.', {
      code: 'PRODUCT_STATE_CORRUPT',
      cause: error,
    });
  }

  try {
    return normalizeProductState(parsed, { now: nowIsoString() });
  } catch (error) {
    if (error instanceof ProductStateError) {
      throw new ProductStateError('Unable to load product state.', {
        code: 'PRODUCT_STATE_INVALID',
        issues: error.issues,
        cause: error,
      });
    }

    throw new ProductStateError('Unable to load product state.', {
      code: 'PRODUCT_STATE_INVALID',
      cause: error,
    });
  }
}

export async function saveProductState(input) {
  const normalized = normalizeProductState(input, { now: nowIsoString() });
  const savedAt = nowIsoString();
  const state = {
    ...normalized,
    updatedAt: savedAt,
  };

  await writeJsonAtomically(productStatePath, state);
  return state;
}

export function isProductStateValidationError(error) {
  return error instanceof ProductStateError && error.code === 'INVALID_PRODUCT_STATE';
}

export function isProductStateLoadError(error) {
  return error instanceof ProductStateError && error.code !== 'INVALID_PRODUCT_STATE';
}
