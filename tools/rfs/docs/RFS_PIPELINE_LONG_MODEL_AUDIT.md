# RFS Long Pipeline / Model Audit

## Scope

Auditoría documental del estado actual de RFS respecto de:

- pipeline long;
- modelos;
- agents;
- configuración de modelos;
- límites con RCK.

Este documento describe *qué existe hoy*, *qué está documentado*, *qué está implícito* y *qué no está definido todavía*.

No contiene propuestas de implementación ni roadmap.

## Fuentes inspeccionadas

- `tools/rfs/src/Rufus.Cli/Program.cs`
- `tools/rfs/src/Rufus.Cli/PiJsonEventRunner.cs`
- `tools/rfs/src/Rufus.Cli/PiRpcClient.cs`
- `tools/rfs/src/Rufus.Cli/TraceSlice/TraceSliceProposalLlmRunner.cs`
- `tools/rfs/src/Rufus.Cli/Tui/RfsCompleteModePipeline.cs`
- `tools/rfs/src/Rufus.Cli/Tui/RfsTuiSession.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckWorkspacePaths.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckWorkspaceModelConfigStore.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckWorkspaceStatus.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckWorkspaceContextPackReader.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckTraceSliceBuilder.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckTraceSliceContextPackBuilder.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckTraceSliceProposalValidator.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckInteractionRecord.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckInteractionRecorder.cs`
- `tools/rfs/src/Rufus.RCK.Workspace/RckAgentTaskRecorder.cs`
- `tools/rfs/src/Rufus.RCK.Core/Model/RckState.cs`
- `tools/rfs/src/Rufus.RCK.Core/Model/RckDelta.cs`
- `tools/rfs/src/Rufus.RCK.Core/Model/RckAnchor.cs`
- `tools/rfs/src/Rufus.Agenting/IAgent.cs`
- `tools/rfs/src/Rufus.Agenting/AgentExecutionModel.cs`
- `tools/rfs/src/Rufus.Agenting/AgentDescriptor.cs`
- `tools/rfs/src/Rufus.Agenting/Intent/IntentInferenceAgent.cs`
- `tools/rfs/src/Rufus.Agenting/TraceSlice/TraceSlicePlannerAgent.cs`
- `tools/rfs/docs/RFS_COMMAND_GOVERNANCE.md`
- `tools/rfs/docs/RFS_TUI_UX_CONTRACT.md`
- `tools/rfs/docs/RFS_FINAL_HANDOFF_STOP_POINT.md`

## 1. Pipeline actual

### `rfs ask`

**Estado:** implemented.

**Evidencia:** `Program.cs`, `PiJsonEventRunner.cs`, `RckInteractionRecorder.cs`

**Lectura actual:**

- Por defecto usa `PiJsonEventRunner.RunAskAsync(...)` en modo headless.
- Pasa el modelo del workspace mediante `RckWorkspaceModelConfigStore.TryReadDefaultModel(...)`.
- Desactiva sesión, tools, extensions y context files (`--no-session`, `--no-tools`, `--no-extensions`, `--no-context-files`).
- Tiene fallback legacy sólo si `RFS_USE_LEGACY_ASK_BRIDGE=1`, en cuyo caso usa `tools/rfs/bridge/rfs-ask.mjs`.
- `--record` no altera el motor; sólo agrega el registro RCK al final si la ejecución terminó bien.

### `rfs ask-json`

**Estado:** experimental / diagnostic, pero implementado.

**Evidencia:** `Program.cs`, `PiJsonEventRunner.cs`, `RFS_COMMAND_GOVERNANCE.md`

**Lectura actual:**

- Usa el mismo runner Pi JSON que `rfs ask`.
- También recibe el modelo del workspace.
- La documentación lo marca como diagnóstico/experimental.

### `rfs ask --record`

**Estado:** implemented.

**Evidencia:** `Program.cs`, `RckInteractionRecorder.cs`, `RckInteractionRecord.cs`

**Lectura actual:**

- Reusa la misma ruta de `rfs ask`.
- El recording se dispara sólo si la ejecución fue exitosa.
- El registro guarda prompt, answer, mode `ask`, summary de respuesta y el artefacto RCK resultante.
- No hay configuración de modelo propia para el recording; hereda la resolución del ask.

### `rfs agent`

**Estado:** legacy current / implemented.

**Evidencia:** `Program.cs`, `RFS_COMMAND_GOVERNANCE.md`

**Lectura actual:**

- No usa `PiJsonEventRunner`.
- Usa el bridge Node `tools/rfs/bridge/rfs-agent.mjs`.
- Pasa `RFS_REPO_ROOT` y el modelo del workspace vía `ApplyWorkspaceModelEnvironment(...)`.
- El output se lee desde stdout/stderr del helper.
- `--raw` está deshabilitado explícitamente.

### `rfs agent --record`

**Estado:** legacy current / implemented.

**Evidencia:** `Program.cs`, `RckInteractionRecorder.cs`, `RckInteractionRecord.cs`

**Lectura actual:**

- Reusa la misma ruta legacy bridge.
- Captura el answer final y los tool events visibles del bridge.
- Si el helper finaliza bien, escribe State + Delta vía `RckInteractionRecorder.RecordAgent(...)`.

### `rfs model get / set / list`

**Estado:** implemented.

**Evidencia:** `Program.cs`, `RckWorkspaceModelConfigStore.cs`, `RckWorkspacePaths.cs`, `PiRpcClient.cs`

**Lectura actual:**

- `model get` lee `.rfs/config.json` del workspace.
- `model set` escribe sólo `.rfs/config.json`.
- `model list` consulta Pi RPC para listar modelos disponibles y marca el modelo actual del workspace.
- No hay escritura RCK asociada.

### `status / log / context-pack / trace-slice / anchor` y comandos TUI relacionados

**Estado:** mixto; implementado para las rutas que existen, documentado para TUI, y algunas rutas son sólo de interfaz.

**Evidencia:** `Program.cs`, `RckWorkspaceStatus.cs`, `RFS_TUI_UX_CONTRACT.md`

**Lectura actual:**

- `rfs status` y `rfs log` existen como comandos top-level.
- `rfs context-pack` existe como export/projection de workspace.
- `rfs trace-slice`, `rfs trace-slice-proposal`, `rfs trace-slice-validate`, `rfs context-pack --trace-slice`, `rfs context-pack --trace-slice-validated` existen en el surface técnico.
- `/status`, `/log`, `/model`, `/context`, `/trace`, `/help`, `/exit` viven en el TUI.
- `/anchor` es un comando interno del TUI y escribe Anchor.
- `/model <model>` escribe sólo `.rfs/config.json`.

## 2. Modelos usados hoy

### ¿Dónde se configura el modelo actual?

**Estado:** definido y acotado a un único modelo por workspace.

**Evidencia:** `RckWorkspaceModelConfigStore.cs`, `RckWorkspacePaths.cs`, `Program.cs`

**Lectura actual:**

- La configuración vive en `.rfs/config.json`.
- La ruta se resuelve como `repoRoot/.rfs/config.json`.
- El valor persistido es `llm.defaultModel`.
- Si no existe configuración, el modelo queda “inherited” / implícito.

### ¿Qué comandos usan el modelo configurado?

**Estado:** implementado.

**Evidencia:** `Program.cs`, `PiJsonEventRunner.cs`, `TraceSliceProposalLlmRunner.cs`, `RfsCompleteModePipeline.cs`, `RfsTuiSession.cs`

**Lectura actual:**

- `rfs ask`
- `rfs ask-json`
- `rfs agent`
- `rfs agent-json`
- `rfs intent` no usa Pi, pero su pipeline de recording sí guarda metadatos de ejecución
- `trace-slice-proposal-llm`
- `trace-slice-validate-llm`
- el TUI en `Simple`, `Complete`, `Plan` y `Direct` también toma el mismo workspace model cuando dispara Pi

### ¿`rfs ask` usa el modelo del workspace?

**Estado:** sí, implementado.

**Evidencia:** `Program.cs`, `PiJsonEventRunner.cs`

**Lectura actual:**

- `rfs ask` toma `RckWorkspaceModelConfigStore.TryReadDefaultModel(...)`.
- `PiJsonEventRunner.ApplyWorkspaceModel(...)` aplica ese valor como `--model <id>` o como `RUFUSCHAT_LLM_MODEL` según el formato.

### ¿`rfs ask-json` usa el modelo del workspace o Pi directamente?

**Estado:** usa el modelo del workspace, no un modelo separado.

**Evidencia:** `Program.cs`, `PiJsonEventRunner.cs`

**Lectura actual:**

- `ask-json` llama a `RunAskAsync(..., TryReadDefaultModel(...))`.
- No hay una ruta propia de configuración por etapa.

### ¿`rfs agent` usa la misma configuración o un bridge distinto?

**Estado:** usa la misma configuración de workspace, pero a través de un bridge distinto.

**Evidencia:** `Program.cs`, `PiJsonEventRunner.cs`

**Lectura actual:**

- `rfs agent` usa `node tools/rfs/bridge/rfs-agent.mjs`.
- `ApplyWorkspaceModelEnvironment(...)` le inyecta el modelo del workspace.
- La mecánica de transporte es distinta a `ask`, pero la fuente del modelo es la misma.

### ¿Hay configuración por etapa?

**Estado:** no definida en el repo actual.

**Evidencia:** búsqueda de `ModelProfile` / `AgentProfile` sin resultados; `RckWorkspaceModelConfigStore.cs`; `Program.cs`; `PiJsonEventRunner.cs`; `TraceSliceProposalLlmRunner.cs`; `RfsCompleteModePipeline.cs`

**Lectura actual:**

- No hay `ModelProfile`.
- No hay `AgentProfile`.
- No hay estructura de config por etapa visible en código o docs.
- Lo que existe es un único default model del workspace reutilizado por varias rutas.

### ¿Hay fallback de modelo documentado o implementado?

**Estado:** sí, pero sólo como fallback global del workspace, no por etapa.

**Evidencia:** `RckWorkspaceModelConfigStore.cs`, `Program.cs`, `PiJsonEventRunner.cs`, `RFS_COMMAND_GOVERNANCE.md`

**Lectura actual:**

- Si no hay `llm.defaultModel`, las rutas usan el modelo implícito / heredado de Pi.
- `rfs model get` lo muestra como `default (Pi/RFS)` / `(inherited)`.
- No se documenta un fallback distinto por pipeline stage.

### Interpretación del “modelo actual del workspace”

**Estado:** es un *default global del workspace* y también un *fallback común* para las rutas que lo consumen.

**Evidencia:** `RckWorkspaceModelConfigStore.cs`, `Program.cs`, `PiJsonEventRunner.cs`, `RfsTuiSession.cs`

**Lectura actual:**

- No debe leerse como “modelo único global de todo el sistema” en sentido absoluto, porque `rfs model list` depende de Pi RPC y el TUI/bridges pueden resolver otras cosas internamente.
- Sí debe leerse como el default persistido que las rutas headless usan hoy cuando necesitan elegir modelo.
- No aparece como configuración por etapa.

## 3. Agents usados hoy

### ¿Qué significa `agent` hoy en el repo?

**Estado:** un bridge de ejecución, no un subagent autónomo del pipeline long.

**Evidencia:** `Program.cs`, `RFS_COMMAND_GOVERNANCE.md`, `RckInteractionRecorder.cs`

**Lectura actual:**

- `rfs agent` invoca un helper Node.
- El runtime real de la ejecución está fuera de `Rufus.Agenting`.
- El recording sólo captura lo que el bridge produce.

### ¿Qué hace `rfs agent --record`?

**Estado:** implementado.

**Evidencia:** `Program.cs`, `RckInteractionRecorder.cs`

**Lectura actual:**

- Ejecuta el bridge.
- Captura el answer final.
- Guarda el conjunto de tool events observados.
- Persiste State + Delta en RCK.

### ¿Qué bridges existen?

**Estado:** documentados e implementados.

**Evidencia:** `Program.cs`, `RFS_COMMAND_GOVERNANCE.md`

**Lectura actual:**

- `tools/rfs/bridge/rfs-ask.mjs`
- `tools/rfs/bridge/rfs-agent.mjs`

### ¿Qué runtime usan?

**Estado:** bridge Node para `ask` legacy / `agent`; Pi JSON runner para la ruta headless principal.

**Evidencia:** `Program.cs`, `PiJsonEventRunner.cs`

**Lectura actual:**

- `rfs ask` normal: `pi --mode json ...`
- `rfs agent`: `node .../rfs-agent.mjs`
- `ask-json` / `agent-json`: `pi --mode json ...`

### ¿Hay subagents reales o no?

**Estado:** no hay subagent runtime autónomo; hay agentes locales in-process.

**Evidencia:** `IAgent.cs`, `IntentInferenceAgent.cs`, `TraceSlicePlannerAgent.cs`, `RfsCompleteModePipeline.cs`

**Lectura actual:**

- Hay una interfaz `IAgent` y dos implementaciones visibles:
  - `IntentInferenceAgent`
  - `TraceSlicePlannerAgent`
- Ambas se ejecutan en-process.
- No hay orchestration de subagents persistentes ni worker pool de subagents en el código auditado.

### ¿Hay intent agent real o no?

**Estado:** sí existe, pero es determinístico/mock.

**Evidencia:** `IntentInferenceAgent.cs`, `Program.cs`, `RfsCompleteModePipeline.cs`, `TraceSliceProposalLlmRunner.cs`

**Lectura actual:**

- `IntentInferenceAgent` implementa inferencia determinística de intent.
- Su `executionModel` es `mock/deterministic-v1`.
- No usa LLM remoto.

### ¿Hay TraceSlice agent real o no?

**Estado:** sí existe `TraceSlicePlannerAgent`, pero también es determinístico/mock.

**Evidencia:** `TraceSlicePlannerAgent.cs`, `RfsCompleteModePipeline.cs`

**Lectura actual:**

- Existe una clase real `TraceSlicePlannerAgent`.
- Su `executionModel` también es `mock/deterministic-v1`.
- No hay un TraceSlice agent LLM-backed separado; la ruta LLM experimental usa Pi directamente, no un agent LLM dedicado.

## 4. Pipeline long conceptual

### Intent inference

**Clasificación:** implemented.

**Evidencia:** `IntentInferenceAgent.cs`, `RfsCompleteModePipeline.cs`, `TraceSliceProposalLlmRunner.cs`, `Program.cs`

**Lectura actual:**

- Existe como agente local determinístico.
- Se usa en la pipeline de Complete mode.
- También se usa en la propuesta LLM de TraceSlice como primer paso de normalización del intent.

### TraceSlice generation

**Clasificación:** implemented en baseline determinístico; experimental en la ruta LLM; no existe subagent LLM dedicado.

**Evidencia:** `TraceSlicePlannerAgent.cs`, `RckTraceSliceBuilder.cs`, `RfsCompleteModePipeline.cs`, `TraceSliceProposalLlmRunner.cs`, `RckTraceSliceProposalValidator.cs`

**Lectura actual:**

- Hay un baseline determinístico de TraceSlice.
- Hay un planner local determinístico para propuestas.
- Hay una ruta experimental `trace-slice-proposal-llm` que usa Pi para proponer JSON.
- La validación final sigue siendo de RFS.

### ContextPack generation

**Clasificación:** implemented.

**Evidencia:** `RckTraceSliceContextPackBuilder.cs`, `RckWorkspaceContextPackReader.cs`, `RckTraceSliceBuilder.cs`

**Lectura actual:**

- `ContextPack` existe como proyección del workspace/RCK.
- Se materializa desde el TraceSlice determinístico o desde el TraceSlice validado.
- No depende de una etapa LLM propia.

### Main LLM execution

**Clasificación:** implemented.

**Evidencia:** `PiJsonEventRunner.cs`, `RfsCompleteModePipeline.cs`, `Program.cs`, `RfsTuiSession.cs`

**Lectura actual:**

- El LLM principal vive detrás de Pi.
- RFS construye el prompt, inyecta contexto validado y luego llama a Pi.
- No hay un “main LLM” como clase propia dentro de `Rufus.Agenting`.

### Verification / review

**Clasificación:** implemented.

**Evidencia:** `RckTraceSliceProposalValidator.cs`, `RfsCompleteModePipeline.cs`, `TraceSliceProposalLlmRunner.cs`

**Lectura actual:**

- La verificación está en RFS, no delegada como agent autónomo.
- Valida IDs, políticas, anchors y materiales permitidos.
- La etapa LLM sólo propone; RFS decide el resultado final.

### State + Delta recording

**Clasificación:** implemented.

**Evidencia:** `RckInteractionRecorder.cs`, `RckAgentTaskRecorder.cs`, `RckInteractionRecord.cs`, `RckAgentTaskRecordInput.cs`, `RckState.cs`, `RckDelta.cs`

**Lectura actual:**

- El recording es determinístico y local.
- `State` y `Delta` en RCK Core no incluyen lógica de modelos.
- La metadata de modelo/provider sólo aparece en las envolturas de interaction/agent recording, no en el core schema base.

## 5. RCK boundaries

### ¿`Rufus.RCK.Core` depende de modelos?

**Estado:** no.

**Evidencia:** `RckState.cs`, `RckDelta.cs`, `RckAnchor.cs`

**Lectura actual:**

- Los tipos core son payload/ref/anchor/delta/state.
- No hay campos `model`, `provider`, `agent`, `profile` o similares en el core schema base.

### ¿`Rufus.RCK.Core` depende de agents?

**Estado:** no.

**Evidencia:** `RckState.cs`, `RckDelta.cs`, `RckAnchor.cs`

**Lectura actual:**

- Core no conoce `IAgent` ni las implementaciones de `Rufus.Agenting`.

### ¿State/Delta incluye información de modelo?

**Estado:** no en el core; sí en las envolturas de interacción/recording.

**Evidencia:** `RckState.cs`, `RckDelta.cs`, `RckInteractionRecorder.cs`, `RckInteractionRecord.cs`, `RckAgentTaskRecordInput.cs`

**Lectura actual:**

- `RckState` y `RckDelta` sólo guardan payload, refs, ids y meta.
- `RckInteractionRecorder` agrega `provider` y `model` dentro del payload de interacción.
- `RckAgentTaskRecordInput` incluye provider/model de ejecución.

### ¿Recording depende de LLM?

**Estado:** no como requisito del core de recording; sí depende de que exista una ejecución que produjo output para registrar.

**Evidencia:** `RckInteractionRecorder.cs`, `RckAgentTaskRecorder.cs`

**Lectura actual:**

- Recording es una operación local sobre artefactos ya generados.
- No invoca el LLM para persistir State + Delta.

### ¿TraceSlice existe en schema o docs?

**Estado:** sí, en ambos.

**Evidencia:** `RckTraceSliceBuilder.cs`, `RckTraceSliceProposalValidator.cs`, `RFS_TRACE_SLICE_LLM_PROPOSAL.md`, `RFS_COMMAND_GOVERNANCE.md`, `RFS_TUI_UX_CONTRACT.md`

**Lectura actual:**

- Existe `TraceSlice` determinístico.
- Existe `TraceSliceProposal` y su validación.
- La documentación distingue proposal, validation y materialization.

### ¿ContextPack existe y cómo se genera?

**Estado:** sí, existe y se genera como proyección del workspace/RCK.

**Evidencia:** `RckWorkspaceContextPackReader.cs`, `RckTraceSliceContextPackBuilder.cs`, `RckTraceSliceBuilder.cs`

**Lectura actual:**

- `ContextPack` no es un modelo de storage, sino una proyección.
- Se construye desde el estado activo del DAG, los anchors y los artefactos cambiados.
- La ruta validated proyecta el resultado de un TraceSlice validado.

## 6. Hallazgos

### Finding 1

**Evidence:** `RckWorkspaceModelConfigStore.cs`, `Program.cs`, `PiJsonEventRunner.cs`

**Status:** implemented / global default only

El repositorio tiene un único default model persistido por workspace en `.rfs/config.json` (`llm.defaultModel`). Ese valor se reutiliza en varias rutas headless y TUI. No hay configuración por etapa.

### Finding 2

**Evidence:** `Program.cs`, `PiJsonEventRunner.cs`, `RFS_COMMAND_GOVERNANCE.md`

**Status:** implemented / mixed transport

`rfs ask` y `rfs agent` no comparten el mismo transporte, aunque sí comparten la resolución del modelo del workspace. `ask` va por Pi JSON; `agent` va por bridge Node.

### Finding 3

**Evidence:** `IntentInferenceAgent.cs`, `TraceSlicePlannerAgent.cs`

**Status:** implemented / deterministic agents

Hay agentes reales en `Rufus.Agenting`, pero su ejecución visible hoy es determinística y mock (`mock/deterministic-v1`). No aparecen como subagents LLM autónomos.

### Finding 4

**Evidence:** `RfsCompleteModePipeline.cs`, `TraceSliceProposalLlmRunner.cs`, `RckTraceSliceProposalValidator.cs`

**Status:** implemented / partially LLM-backed

La pipeline long conceptual existe sólo de forma parcial y compuesta: intent local, TraceSlice determinístico o proposal LLM experimental, validación RFS, ContextPack materializado y finalmente LLM principal. No hay una única abstracción de pipeline long como entidad de runtime.

### Finding 5

**Evidence:** `RckState.cs`, `RckDelta.cs`, `RckInteractionRecorder.cs`

**Status:** clear boundary

`Rufus.RCK.Core` no depende de modelos ni agents. La información de modelo/provider aparece en los registros de interacción, no en el core schema base.

### Finding 6

**Evidence:** búsqueda de `ModelProfile` / `AgentProfile` sin resultados; `RckWorkspaceModelConfigStore.cs`

**Status:** missing / undefined

No existe una capa de `ModelProfile` o `AgentProfile` en el repositorio auditado. Tampoco existe una configuración por etapa para pipeline long.

### Finding 7

**Evidence:** `RFS_TUI_UX_CONTRACT.md`, `RFS_COMMAND_GOVERNANCE.md`, `Program.cs`

**Status:** documented and implemented

La documentación separa correctamente proposal, validation, materialization, recording y rutas experimentales. Esa separación aparece reflejada en los comandos, pero no en una jerarquía de perfiles de modelo.

### Finding 8

**Evidence:** `RckWorkspaceContextPackReader.cs`, `RckTraceSliceBuilder.cs`, `RckTraceSliceContextPackBuilder.cs`

**Status:** implemented

`TraceSlice` y `ContextPack` existen como proyecciones determinísticas del workspace/RCK. Su generación es read-only y no depende de un LLM dedicado.

## 7. Conclusión sintética

Hoy RFS tiene:

- un *default model* de workspace compartido por las rutas que hablan con Pi;
- agentes locales determinísticos para intent y trace-slice planning;
- una ruta de bridge legacy para `rfs agent`;
- una pipeline long conceptual que ya está descompuesta en pasos, pero sin configuración de modelos por etapa ni perfiles explícitos de modelo/agente.

