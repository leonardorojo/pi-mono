export type RckProviderKind = "pi-rck-bridge" | "rck-core-kernel";

export interface RckProviderCapabilities {
	readonly canReadStatus: boolean;
	readonly canReadInventory: boolean;
	readonly canReadSupervision: boolean;
	readonly canReadSafeContext: boolean;
	readonly canReadCurrentTrace: boolean;
	readonly canListTraces: boolean;
	readonly canReadTrace: boolean;
	readonly canReadTraceTimeline: boolean;
	readonly canCreateState: boolean;
	readonly canCreateDelta: boolean;
	readonly canCreateAnchor: boolean;
	readonly canInjectContext: boolean;
	readonly canCreateTrace: boolean;
	readonly canSwitchTrace: boolean;
	readonly canCloseTrace: boolean;
	readonly canRunHermesFake: boolean;
	readonly canRunHermesRealGated: boolean;
	readonly canRunCodex: boolean;
}

export interface RckProviderInfo {
	readonly kind: RckProviderKind;
	readonly name: string;
	readonly displayName: string;
	readonly version?: string;
	readonly capabilities: RckProviderCapabilities;
}

export interface RckProviderRequestContext {
	readonly projectRepoPath: string;
	readonly chatSessionId?: string;
	readonly traceId?: string;
	readonly userDecisionId?: string;
}

export interface RckProviderError {
	readonly code: string;
	readonly message: string;
	readonly details?: unknown;
	readonly retryable?: boolean;
}

export interface RckProviderResult<T> {
	readonly ok: boolean;
	readonly value?: T;
	readonly error?: RckProviderError;
}

export interface RckProviderEvidenceRef {
	readonly evidenceId: string;
	readonly kind: string;
	readonly label?: string;
	readonly uri?: string;
}

export interface RckProviderTraceRef {
	readonly traceId: string;
	readonly providerKind: RckProviderKind;
	readonly status?: string;
}

export interface RckProviderStateRef {
	readonly stateId: string;
	readonly traceId?: string;
}

export interface RckProviderDeltaRef {
	readonly deltaId: string;
	readonly traceId?: string;
}

export interface RckProviderAnchorRef {
	readonly anchorId: string;
	readonly traceId?: string;
}

export interface RckProviderContextPackRef {
	readonly contextPackId: string;
	readonly traceId?: string;
	readonly approvedByUser?: boolean;
}

export interface RckProvider {
	getStatus(context: RckProviderRequestContext): Promise<RckProviderResult<unknown>>;
	getInventory(context: RckProviderRequestContext): Promise<RckProviderResult<unknown>>;
	getSupervision(context: RckProviderRequestContext): Promise<RckProviderResult<unknown>>;
	getSafeContext(context: RckProviderRequestContext): Promise<RckProviderResult<unknown>>;
	getCurrentTrace(context: RckProviderRequestContext): Promise<RckProviderResult<unknown>>;
	listTraces(context: RckProviderRequestContext): Promise<RckProviderResult<unknown>>;
	getTrace(context: RckProviderRequestContext, traceId: string): Promise<RckProviderResult<unknown>>;
	getTraceTimeline(context: RckProviderRequestContext, traceId: string): Promise<RckProviderResult<unknown>>;
	createState(context: RckProviderRequestContext, request: unknown): Promise<RckProviderResult<unknown>>;
	createDelta(context: RckProviderRequestContext, request: unknown): Promise<RckProviderResult<unknown>>;
	createAnchor(context: RckProviderRequestContext, request: unknown): Promise<RckProviderResult<unknown>>;
	injectContext(context: RckProviderRequestContext, request: unknown): Promise<RckProviderResult<unknown>>;
	createTrace(context: RckProviderRequestContext, request: unknown): Promise<RckProviderResult<unknown>>;
	switchTrace(context: RckProviderRequestContext, traceId: string): Promise<RckProviderResult<unknown>>;
	closeTrace(context: RckProviderRequestContext, traceId: string): Promise<RckProviderResult<unknown>>;
	runHermesFake(context: RckProviderRequestContext, request: unknown): Promise<RckProviderResult<unknown>>;
	runHermesRealGated(context: RckProviderRequestContext, request: unknown): Promise<RckProviderResult<unknown>>;
	runCodex(context: RckProviderRequestContext, request: unknown): Promise<RckProviderResult<unknown>>;
}
