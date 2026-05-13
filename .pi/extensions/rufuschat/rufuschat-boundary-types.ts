export interface RufusChatProjectRef {
	kind: "project";
	repoPath: string;
	name?: string;
}

export interface RufusChatChatSessionRef {
	kind: "chat-session";
	sessionId: string;
	projectRepoPath: string;
}

export interface RufusChatConversationMemorySummary {
	kind: "conversation-memory-summary";
	scope: "project" | "chat-session";
	summary: string;
}

export interface RufusChatRckTraceRef {
	kind: "rck-trace";
	traceId: string;
	provider: "pi-rck-bridge" | "rck-core-kernel" | string;
}

export interface RufusChatContextPackRef {
	kind: "context-pack";
	contextPackId: string;
	origin: "rck-trace" | "chat-session" | "project";
}

export interface RufusChatBoundaryModel {
	project: RufusChatProjectRef;
	chatSession?: RufusChatChatSessionRef;
	conversationMemory?: RufusChatConversationMemorySummary;
	currentRckTrace?: RufusChatRckTraceRef;
	currentContextPack?: RufusChatContextPackRef;
}
