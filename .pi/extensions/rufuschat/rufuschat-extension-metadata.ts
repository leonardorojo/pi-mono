export const rufuschatExtensionMetadata = {
	name: "rufuschat",
	displayName: "RufusChat",
	status: "foundation",
	currentPrototype: "scripts/rufuschat-ui-server.mjs",
	prototype: "scripts/rufuschat-ui-server.mjs",
	rckProvider: "pi-rck-bridge",
	futureRckProvider: "rck-core-kernel",
	safety: {
		noRawEvidenceByDefault: true,
		mutatingActionsRequireConfirmation: true,
		hermesRealGated: true,
	},
} as const;

export type RufusChatExtensionMetadata = typeof rufuschatExtensionMetadata;
