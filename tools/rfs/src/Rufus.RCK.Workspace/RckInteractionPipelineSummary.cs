namespace Rufus.RCK.Workspace;

public sealed record RckInteractionPipelineSummary
{
    public string Kind { get; }

    public bool UsesRckContext { get; }

    public bool UsesTraceSlice { get; }

    public bool UsesContextPack { get; }

    public string? ValidationStatus { get; }

    public int? RecentInteractionCount { get; }

    public IReadOnlyList<string>? SelectedStateIds { get; }

    public IReadOnlyList<string>? SelectedDeltaIds { get; }

    public IReadOnlyList<string>? SelectedAnchorIds { get; }

    public int? ArtifactRefCount { get; }

    public int? EstimatedChars { get; }

    public int? EstimatedTokens { get; }

    public bool? Truncated { get; }

    public IReadOnlyList<string>? Omissions { get; }

    public RckInteractionPipelineSummary(
        string kind,
        bool usesRckContext,
        bool usesTraceSlice,
        bool usesContextPack,
        string? validationStatus,
        int? recentInteractionCount = null,
        IReadOnlyList<string>? selectedStateIds = null,
        IReadOnlyList<string>? selectedDeltaIds = null,
        IReadOnlyList<string>? selectedAnchorIds = null,
        int? artifactRefCount = null,
        int? estimatedChars = null,
        int? estimatedTokens = null,
        bool? truncated = null,
        IReadOnlyList<string>? omissions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        Kind = kind;
        UsesRckContext = usesRckContext;
        UsesTraceSlice = usesTraceSlice;
        UsesContextPack = usesContextPack;
        ValidationStatus = validationStatus;
        RecentInteractionCount = recentInteractionCount;
        SelectedStateIds = selectedStateIds;
        SelectedDeltaIds = selectedDeltaIds;
        SelectedAnchorIds = selectedAnchorIds;
        ArtifactRefCount = artifactRefCount;
        EstimatedChars = estimatedChars;
        EstimatedTokens = estimatedTokens;
        Truncated = truncated;
        Omissions = omissions;
    }
}
