# RCK trace initialization placeholder

Esta fase define el boundary placeholder para el nacimiento de un trace RCK cuando nace un chat o un proyecto en RufusChat.

Es documentación y contrato conceptual. No conecta RCK Core real, no escribe `.rck`, no escribe `.data`, no muta el DAG RCK real y no introduce un botón Anchor.

## Objetivo

Cuando RufusChat crea un nuevo chat/proyecto, debe existir un punto conceptual para preparar el futuro trace sin ejecutar todavía la integración real.

Secuencia conceptual:

```text
chat/project creation
→ TraceInitializationDraft
→ InitialStatePayload
→ InitialAnchorDraft
→ linkedRckTrace queda preparado para un future adapter/service
```

## Qué es `TraceInitializationDraft`

`TraceInitializationDraft` es el borrador estructural que describe el nacimiento del trace antes de que exista cualquier registro real de RCK.

Debe responder, como mínimo:

- qué chat/proyecto originó el nacimiento
- qué `projectId` y `chatId` lo identifican
- qué fuente disparó la creación
  - chat nuevo explícito
  - proyecto nuevo con chat inicial
  - fallback de selección
  - seed/reset/hidratación
- qué estado inicial del chat existe en ese momento
- qué metadatos seguros se pueden transportar al future adapter/service

No representa un nodo RCK real. Solo prepara el paquete de arranque.

## Qué es `InitialStatePayload`

`InitialStatePayload` es la carga de estado inicial que el future adapter/service podría traducir a un estado RCK real más adelante.

Debe ser estable, pequeño y product-safe.

Campos conceptuales sugeridos:

- `projectId`
- `chatId`
- `traceBirthId` o identificador equivalente del nacimiento del trace
- `chatTitle`
- `projectName`
- `source`
- `createdAt`
- `linkedRckTraceSnapshot`
- `initialMessagesSnapshot` cuando sea seguro y útil
- `runtimeOnly` flags que no deben persistirse

No debe incluir:

- secretos
- raw tool output
- `.rck`
- `.data`
- DAG real
- evidence dumps sin normalizar

## Qué es `InitialAnchorDraft`

`InitialAnchorDraft` es el borrador del primer anclaje estructural del trace.

Importante: no es el botón Anchor de usuario.

Representa el “nacimiento del trace” como estructura, no una decisión humana posterior.

Debe ser:

- único por trace inicial
- determinista respecto a `projectId`/`chatId`
- anterior a cualquier `ChatTurnStatePayload` o `ChatTurnDeltaPayload` posterior
- independiente de los turnos normales

## Cuándo se crean

La creación conceptual ocurre solo cuando nace un chat/proyecto nuevo o cuando un fallback realmente materializa un chat nuevo.

Candidatos de entrada:

- `createChat(...)`
- `createChatInProject(...)`
- `createProjectWithInitialChat(...)`
- `createNewProject()`
- `createNewChatForProject()`
- `applySelectionFallback(...)` solo si termina creando un chat real

No debe generarse en:

- `replaceStateFromProductState(...)`
- hidratación de estado persistido
- normalización de estado importado
- renderizado
- selección visual por sí sola

## Relación entre Initial Anchor y futuro User Anchor

`InitialAnchorDraft` y `User Anchor` son conceptos distintos.

### Initial Anchor

- nace con el trace
- es estructural
- no depende de intención humana explícita
- no se usa para señalar una decisión del usuario
- no se repite por turno, injection o delta manual

### User Anchor futuro

- será una fase posterior
- expresará una decisión, hito o marca semántica del usuario
- puede depender de contexto, aprobación o intervención explícita
- no debe confundirse con el anclaje inicial

En resumen: initial anchor = nacimiento; user anchor = intención semántica posterior.

## Relación con `projectId` y `chatId`

`projectId` y `chatId` ya son las identidades persistidas relevantes para el modelo actual.

La inicialización del trace debe anclarse a esas identidades porque:

- ya existen en el modelo persistido
- son estables a través de hidratación y guardado
- permiten derivar el futuro `sessionId` sin introducir otra entidad visible innecesaria

No se debe inventar una identidad separada si el propósito es solo preparar el nacimiento del trace.

## Relación con `linkedRckTrace`

El chat ya tiene `linkedRckTrace` como placeholder estructurado.

Estado actual esperado:

- `status: 'not-linked'`
- `traceId: null`
- `provider: 'pi-rck-bridge'`
- `futureProvider: 'rck-core-kernel'`
- `mode: 'placeholder'`

Esta fase no cambia esa semántica. Solo prepara el terreno para que, en el futuro, `linkedRckTrace` pueda pasar de placeholder a una relación real con el trace inicial.

## Relación con `ChatTurnStatePayload` y `ChatTurnDeltaPayload`

Esta fase es anterior al flujo de turnos cerrados.

Orden conceptual completo:

```text
chat/project creation
→ TraceInitializationDraft
→ InitialStatePayload
→ InitialAnchorDraft
→ later: ChatTurnStatePayload
→ later: ChatTurnDeltaPayload
```

Por tanto:

- el trace inicial no reemplaza al placeholder por turno cerrado
- el trace inicial no depende de un turno completado
- los turnos posteriores siguen usando el boundary ya estabilizado por chat turn complete

## Qué persiste y qué es runtime-only

### Runtime-only

- `TraceInitializationDraft`
- `InitialStatePayload`
- `InitialAnchorDraft`
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
- forzar un trace por cada turn, injection o checkpoint

## Riesgos detectados

### 1. `createChat(...)` es muy canónica

Hoy `createChat(...)` construye el chat base y también se usa indirectamente en seed/default/reset/fallback.

Riesgo: si se engancha trace init de forma ingenua ahí, el seed inicial o un reset podrían crear trazas cuando no toca.

### 2. `createProjectWithInitialChat(...)` y `createNewProject()` son rutas visibles de nacimiento

Son puntos naturales para la lógica de nacimiento del chat/proyecto, pero deben delegar al mismo helper conceptual para no duplicar reglas.

### 3. `applySelectionFallback(...)` puede fabricar chats implícitos

Si se usa para recuperar una selección vacía, también puede terminar materializando un chat nuevo.

Riesgo: crear un initial trace durante un fallback visual puede parecer un nacimiento real aunque solo se estaba reparando estado UI.

### 4. Hidratación y reset deben ser explícitos

La hidratación reconstruye estado ya existente.

Riesgo: no distinguir entre “reconstruir” y “crear” puede producir trazas duplicadas.

### 5. `chatTurnWritebackResultsByChatId` es runtime-only

Ese Map no sobrevive a recargas.

Riesgo: no debe usarse como fuente duradera del nacimiento del trace.

## Hook point recomendado

Recomendación conceptual:

1. mantener `createChat(...)` como fábrica canónica de objeto chat
2. introducir un helper de nacimiento de trace separado y puro, por ejemplo:

```js
buildTraceInitializationDraft(...)
buildInitialTraceStatePayload(...)
buildInitialAnchorDraft(...)
```

3. invocar ese helper solo en flujos de “chat/proyecto realmente nuevo”
4. no invocarlo durante `replaceStateFromProductState(...)`
5. no invocarlo durante hidratación/normalización/importación

En otras palabras: el mejor lugar no es el render ni la selección visual, sino la capa donde se materializa un chat nuevo como entidad de producto.

Si en el futuro hay que elegir una sola zona de integración, el orden de preferencia sería:

- primero: helper llamado desde los flujos de creación reales
- segundo: `createChat(...)` con una vía explícita de opt-in para nacimiento de trace
- no recomendado: fallback automático sin contexto

## Camino futuro hacia adapter/service real

Cuando llegue la integración real, el adapter/service debería recibir el draft y traducirlo a RCK internals fuera del UI.

Camino futuro esperado:

```text
TraceInitializationDraft
→ adapter/service
→ real RCK State/Anchor initialization
→ linkedRckTrace actualizado
→ turn write-back sigue por su boundary propio
```

Eso mantiene separados:

- nacimiento del trace
- write-back de turnos cerrados
- futura selección de anchors de usuario

## Resumen corto

- `TraceInitializationDraft` = borrador de nacimiento
- `InitialStatePayload` = estado inicial preparado para el future adapter/service
- `InitialAnchorDraft` = primer anclaje estructural, no botón Anchor
- todo esto es placeholder / contract-only
- no hay mutación RCK real
- no hay `.rck`
- no hay `.data`
- no hay Manual Delta
- no hay Anchor de usuario todavía
