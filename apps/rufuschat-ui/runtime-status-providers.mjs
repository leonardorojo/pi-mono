export function getRuntimeProviderStatus() {
  return {
    mode: 'local',
    label: 'Local session',
  };
}

export function getMemoryProviderStatus() {
  return {
    status: 'off',
    label: 'Memory off',
  };
}

export function getContextProviderStatus() {
  return {
    status: 'off',
    label: 'Context off',
  };
}

export function getTraceProviderStatus() {
  return {
    status: 'not_linked',
    label: 'Trace not linked',
  };
}

export function getLlmProviderStatus() {
  return {
    status: 'off',
    label: 'LLM off',
  };
}
