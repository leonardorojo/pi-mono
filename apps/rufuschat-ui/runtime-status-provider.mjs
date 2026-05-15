import { createDefaultRuntimeStatus, normalizeRuntimeStatus } from './runtime-status-schema.mjs';

export function createRuntimeStatus() {
  return normalizeRuntimeStatus(createDefaultRuntimeStatus());
}
