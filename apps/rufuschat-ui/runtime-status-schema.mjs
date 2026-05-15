const DEFAULT_RUNTIME_STATUS = Object.freeze({
  version: 1,
  runtime: Object.freeze({
    mode: 'local',
    label: 'Local session',
  }),
  memory: Object.freeze({
    status: 'off',
    label: 'Memory off',
  }),
  context: Object.freeze({
    status: 'off',
    label: 'Context off',
  }),
  trace: Object.freeze({
    status: 'not_linked',
    label: 'Trace not linked',
  }),
  llm: Object.freeze({
    status: 'off',
    label: 'LLM off',
  }),
});

function toSafeString(value, fallback) {
  return typeof value === 'string' && value.trim() ? value.trim() : fallback;
}

function normalizeCapabilitySection(input, defaults) {
  const source = input && typeof input === 'object' ? input : {};

  return {
    status: toSafeString(source.status, defaults.status),
    label: toSafeString(source.label, defaults.label),
  };
}

function normalizeRuntimeSection(input, defaults) {
  const source = input && typeof input === 'object' ? input : {};

  return {
    mode: toSafeString(source.mode, defaults.mode),
    label: toSafeString(source.label, defaults.label),
  };
}

export function createDefaultRuntimeStatus() {
  return {
    version: DEFAULT_RUNTIME_STATUS.version,
    runtime: { ...DEFAULT_RUNTIME_STATUS.runtime },
    memory: { ...DEFAULT_RUNTIME_STATUS.memory },
    context: { ...DEFAULT_RUNTIME_STATUS.context },
    trace: { ...DEFAULT_RUNTIME_STATUS.trace },
    llm: { ...DEFAULT_RUNTIME_STATUS.llm },
  };
}

export function normalizeRuntimeStatus(input) {
  const source = input && typeof input === 'object' ? input : {};
  const defaults = DEFAULT_RUNTIME_STATUS;

  return {
    version: defaults.version,
    runtime: normalizeRuntimeSection(source.runtime, defaults.runtime),
    memory: normalizeCapabilitySection(source.memory, defaults.memory),
    context: normalizeCapabilitySection(source.context, defaults.context),
    trace: normalizeCapabilitySection(source.trace, defaults.trace),
    llm: normalizeCapabilitySection(source.llm, defaults.llm),
  };
}
