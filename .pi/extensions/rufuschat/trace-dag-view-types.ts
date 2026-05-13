export type TraceDagNodeKind = "state" | "delta" | "anchor" | "context-pack" | "executor-run";

export type TraceDagEdgeKind = "transition" | "annotates" | "derived-context" | "evidence-produced" | "references";

export interface TraceDagNodeRef {
	readonly id: string;
	readonly kind: TraceDagNodeKind;
	readonly label?: string;
	readonly traceId?: string;
}

export interface TraceDagEdgeRef {
	readonly id: string;
	readonly kind: TraceDagEdgeKind;
	readonly from: string;
	readonly to: string;
	readonly traceId?: string;
}

export interface TraceDagProjectionDto {
	readonly traceId: string;
	readonly nodes: ReadonlyArray<TraceDagNodeRef>;
	readonly edges: ReadonlyArray<TraceDagEdgeRef>;
	readonly latestStateId?: string | null;
	readonly latestAnchorId?: string | null;
	readonly latestContextPackId?: string | null;
	readonly counts: {
		readonly states: number;
		readonly deltas: number;
		readonly anchors: number;
		readonly contextPacks: number;
		readonly evidenceRefs: number;
	};
	readonly generatedAt: string;
}

export interface TraceDagViewModel {
	readonly currentTraceId?: string | null;
	readonly projection?: TraceDagProjectionDto | null;
	readonly placeholderStatus: TraceDagPlaceholderStatus;
}

export interface TraceDagPlaceholderStatus {
	readonly status: "placeholder" | "ready-for-projection" | "projected";
	readonly currentView: "linear-cards-only" | "dag-projection";
	readonly future: "states-deltas-anchors-dag";
}
