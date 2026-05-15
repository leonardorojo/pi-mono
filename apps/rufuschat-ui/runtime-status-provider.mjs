export function createRuntimeStatus() {
  return {
    version: 1,
    runtime: {
      mode: 'local',
      label: 'Local session',
    },
    memory: {
      status: 'off',
      label: 'Memory off',
    },
    context: {
      status: 'off',
      label: 'Context off',
    },
    trace: {
      status: 'not_linked',
      label: 'Trace not linked',
    },
    llm: {
      status: 'off',
      label: 'LLM off',
    },
  };
}
