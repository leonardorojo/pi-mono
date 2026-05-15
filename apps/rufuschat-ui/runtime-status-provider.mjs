import { normalizeRuntimeStatus } from './runtime-status-schema.mjs';
import {
  getContextProviderStatus,
  getLlmProviderStatus,
  getMemoryProviderStatus,
  getRuntimeProviderStatus,
  getTraceProviderStatus,
} from './runtime-status-providers.mjs';

export function createRuntimeStatus() {
  return normalizeRuntimeStatus({
    version: 1,
    runtime: getRuntimeProviderStatus(),
    memory: getMemoryProviderStatus(),
    context: getContextProviderStatus(),
    trace: getTraceProviderStatus(),
    llm: getLlmProviderStatus(),
  });
}
