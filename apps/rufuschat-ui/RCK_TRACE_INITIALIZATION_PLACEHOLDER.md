# RCK trace / branch / anchor initialization placeholder

Esta fase corrige el modelo conceptual de RufusChat para que quede alineado así:

```text
Project → Trace
Chat → Branch inside that Trace
Chat turn → State + Delta inside the current Branch
```

Es documentación y contrato conceptual. No conecta RCK Core real, no escribe `.rck`, no escribe `.data`, no muta el DAG RCK real y no introduce botones nuevos.

## Modelo correcto

### Project = Trace

Crear un Project crea un Trace nuevo.

El Project es el contenedor de producto; el Trace es la estructura RCK de nivel superior asociada a ese Project.

### Chat = Branch

Crear un Chat no crea un Trace nuevo.

Crear un Chat crea una Branch dentro del Trace del Project.

### First Chat = Main Branch

El primer Chat del Project es la Main Branch del Trace.

### Additional Chats = Cognitive Branches

Cada Chat adicional del mismo Project crea una Cognitive Branch dentro del mismo Trace.

### Chat turns = State + Delta within the Branch

Cada interacción dentro de un Chat crea State + Delta dentro de esa Branch.

## Secuencia conceptual correcta

```text
Project creation
→ TraceInitializationDraft
→ InitialTraceAnchorDraft
→ Trace birth
→ first Chat
→ Main Branch birth
→ BranchReferenceAnchorDraft

Additional Chat creation
→ BranchInitializationDraft
→ Branch birth inside existing Trace
→ BranchReferenceAnchorDraft

Chat turn complete
→ ChatTurnStatePayload
→ ChatTurnDeltaPayload
→ State + Delta inside current Branch
```

## Qué es `TraceInitializationDraft`

`TraceInitializationDraft` es el borrador estructural del nacimiento del Trace.

Nace con el Project.

Debe responder, como mínimo:

- qué `projectId` originó el Trace
- qué nombre o identidad de Project lo disparó
- qué fuente disparó la creación
  - project nuevo explícito
  - reset/seed que materializa un Project nuevo
- qué metadatos seguros se pueden transportar al future adapter/service
- qué Branch inicial deberá existir dentro del Trace

No representa un nodo RCK real. Solo prepara el arranque del Trace.

## Qué es `InitialTraceAnchorDraft`

`InitialTraceAnchorDraft` es el anclaje estructural de nacimiento del Trace.

No es un botón Anchor de usuario.

Representa la referencia estructural que dice: “aquí nació este Trace”.

Debe ser:

- único por Trace inicial
- determinista respecto a `projectId`
- anterior a cualquier Branch o ChatTurn posterior
- independiente de los turnos normales

## Qué es `BranchInitializationDraft`

`BranchInitializationDraft` es el borrador estructural del nacimiento de una Branch.

Nace con el Chat.

Debe responder, como mínimo:

- qué `chatId` originó la Branch
- a qué `projectId` pertenece
- si es `main` o `cognitive`
- qué Trace padre la contiene
- qué metadatos seguros se pueden transportar al future adapter/service

No representa un nodo RCK real. Solo prepara el arranque de la Branch.

## Qué es `BranchReferenceAnchorDraft`

`BranchReferenceAnchorDraft` es el anclaje estructural de nacimiento de la Branch.

No es un botón Anchor de usuario.

Representa la referencia estructural que dice: “aquí nació esta Branch dentro del Trace”.

Debe ser:

- único por Branch inicial
- determinista respecto a `projectId` + `chatId`
- anterior a cualquier ChatTurn posterior dentro de esa Branch
- independiente de los turnos normales

## Relación entre Trace Anchor, Branch Anchor, User Anchor y Merge Anchor

### Trace Anchor

- anclaje de nacimiento del Trace
- existe al crear el Project
- es estructural
- no expresa intención humana
- no depende de un turno

### Branch Anchor

- anclaje de nacimiento de la Branch
- existe al crear el Chat
- es estructural
- no expresa intención humana
- no se repite por turno

### User Anchor futuro

- fase posterior
- expresará una decisión, hito o marca semántica del usuario
- puede depender de contexto, aprobación o intervención explícita
- no debe confundirse con el anclaje estructural de Trace o Branch

### Merge Anchor futuro

- fase posterior
- representará convergencia o unión de ramas
- solo aplica si existe una semántica futura de merge entre Branches
- no debe existir todavía en esta fase

En resumen:

- Trace Anchor = nacimiento del Trace
- Branch Anchor = nacimiento de la Branch
- User Anchor = semántica explícita del usuario
- Merge Anchor = futura convergencia entre ramas

## Cuándo se crean

### Se crean al materializar un Project nuevo

- `createProject(...)`
- `createNewProject()`
- `createResetProductStatePayload()` cuando reconstruye un Project nuevo
- cualquier seed inicial que realmente cree un Project nuevo

### Se crean al materializar un Chat nuevo

- `createChat(...)`
- `createChatInProject(...)`
- `createProjectWithInitialChat(...)`
- `createNewChatForProject()`
- `applySelectionFallback(...)` solo si termina creando un Chat real

### No deben generarse en

- `replaceStateFromProductState(...)`
- hidratación de estado persistido
- normalización de estado importado
- renderizado
- selección visual por sí sola

## Relación con `projectId` y `chatId`

`projectId` y `chatId` ya son las identidades persistidas relevantes para el modelo actual.

La alineación correcta es:

- `projectId` → Trace
- `chatId` → Branch dentro del Trace

No se debe inventar una identidad separada si el propósito es solo preparar el nacimiento del Trace o de la Branch.

Contract shapes for the next runtime step are documented in [`RCK_TRACE_BRANCH_CONTRACT_SHAPES.md`](./RCK_TRACE_BRANCH_CONTRACT_SHAPES.md).

## Relación con `linkedRckTrace`

El chat ya tiene `linkedRckTrace` como placeholder estructurado.

Estado actual esperado:

- `status: 'not-linked'`
- `traceId: null`
- `provider: 'pi-rck-bridge'`
- `futureProvider: 'rck-core-kernel'`
- `mode: 'placeholder'`

Esta fase no cambia esa semántica. Solo prepara el terreno para que, en el futuro, `linkedRckTrace` pueda pasar de placeholder a una relación real con el Trace/Branch naciente.

## Relación con `ChatTurnStatePayload` y `ChatTurnDeltaPayload`

Esta fase es anterior al flujo de turnos cerrados.

Orden conceptual completo:

```text
Project creation
→ TraceInitializationDraft
→ InitialTraceAnchorDraft
→ Trace
→ first Chat
→ BranchInitializationDraft
→ BranchReferenceAnchorDraft
→ Branch
→ later: ChatTurnStatePayload
→ later: ChatTurnDeltaPayload
```

Por tanto:

- el Trace inicial no reemplaza al placeholder por turno cerrado
- la Branch inicial no depende de un turno completado
- los turnos posteriores siguen usando el boundary ya estabilizado por chat turn complete

## Qué persiste y qué es runtime-only

### Runtime-only

- `TraceInitializationDraft`
- `BranchInitializationDraft`
- `InitialTraceAnchorDraft`
- `BranchReferenceAnchorDraft`
- draft/result previews
- cualquier objeto temporal de preparación
- `chatTurnWritebackResultsByChatId`

### Persistido hoy

- `projectId`
- `chatId`
- chats y proyectos del ProductState
- `linkedRckTrace` placeholder
- mensajes, injections y checkpoints del estado de producto

### No persistir todavía

- DAG RCK real
- anchor real
- `.rck`
- `.data`
- materialización de trace real
- materialización de branch real

## Garantías de no mutación real

Esta fase no debe:

- escribir `.rck`
- leer `.rck`
- escribir `.data`
- mutar RCK Core
- registrar anchors reales
- crear manual delta
- cambiar el flujo de chat completion
- introducir un botón Anchor
- forzar un Trace o Branch por cada turn, injection o checkpoint

## Riesgos detectados

### 1. `createProject(...)` es la verdadera fábrica del Trace

Si se engancha la lógica de Trace de forma ingenua en otro sitio, se puede mezclar la creación de Project con meros cambios visuales.

### 2. `createChat(...)` es la verdadera fábrica de Branches

Hoy `createChat(...)` construye el chat base y también se usa indirectamente en seed/default/reset/fallback.

Riesgo: si se engancha branch init de forma ingenua ahí, el seed inicial o un reset podrían crear Branches cuando no toca.

### 3. `createProjectWithInitialChat(...)` y `createNewProject()` crean Trace + Main Branch juntos

Son puntos naturales para la lógica de nacimiento del Project/Trace, pero deben delegar al mismo helper conceptual para no duplicar reglas.

### 4. `applySelectionFallback(...)` puede fabricar chats implícitos

Si se usa para recuperar una selección vacía, también puede terminar materializando un Chat nuevo.

Riesgo: crear un Branch anchor durante un fallback visual puede parecer un nacimiento real aunque solo se estaba reparando estado UI.

### 5. Hidratación y reset deben ser explícitos

La hidratación reconstruye estado ya existente.

Riesgo: no distinguir entre “reconstruir” y “crear” puede producir Trace o Branch duplicados.

### 6. `chatTurnWritebackResultsByChatId` es runtime-only

Ese Map no sobrevive a recargas.

Riesgo: no debe usarse como fuente duradera del nacimiento del Trace o de la Branch.

## Hook point recomendado

Recomendación conceptual:

1. mantener `createProject(...)` como fábrica canónica del Trace
2. mantener `createChat(...)` como fábrica canónica de la Branch
3. introducir helpers de nacimiento separados y puros, por ejemplo:

```js
buildTraceInitializationDraft(...)
buildInitialTraceStatePayload(...)
buildInitialTraceAnchorDraft(...)
buildBranchInitializationDraft(...)
buildBranchReferenceAnchorDraft(...)
```

4. invocar cada helper solo en el flujo que realmente materializa ese nivel
5. no invocarlos durante `replaceStateFromProductState(...)`
6. no invocarlos durante hidratación/normalización/importación

En otras palabras: el mejor lugar no es el render ni la selección visual, sino la capa donde se materializa un Project o un Chat nuevo como entidad de producto.

## Camino futuro hacia adapter/service real

Cuando llegue la integración real, el adapter/service debería recibir el draft y traducirlo a RCK internals fuera del UI.

Camino futuro esperado:

```text
Project creation
→ TraceInitializationDraft
→ InitialTraceAnchorDraft
→ adapter/service
→ real Trace initialization

Chat creation
→ BranchInitializationDraft
→ BranchReferenceAnchorDraft
→ adapter/service
→ real Branch initialization inside Trace

Chat turn complete
→ ChatTurnStatePayload
→ ChatTurnDeltaPayload
→ State + Delta inside current Branch
```

Eso mantiene separados:

- nacimiento del Trace
- nacimiento de la Branch
- write-back de turnos cerrados
- futura selección de anchors de usuario
- futura convergencia entre ramas

## Resumen corto

- `TraceInitializationDraft` = borrador de nacimiento del Trace, nace con Project
- `BranchInitializationDraft` = borrador de nacimiento de la Branch, nace con Chat
- `InitialTraceAnchorDraft` = primer anclaje estructural del Trace
- `BranchReferenceAnchorDraft` = primer anclaje estructural de la Branch
- `UserAnchorDraft` = futuro botón/acción explícita del usuario
- `MergeAnchorDraft` = futuro caso de convergencia entre ramas
- todo esto es placeholder / contract-only
- no hay mutación RCK real
- no hay `.rck`
- no hay `.data`
- no hay Manual Delta
- no hay Anchor de usuario todavía
