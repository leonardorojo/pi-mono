namespace Rufus.RCK.Workspace;

public sealed record RckInteractionPipelineSummary
{
    public string Kind { get; }

    public bool UsesRckContext { get; }

    public bool UsesTraceSlice { get; }

    public bool UsesContextPack { get; }

    public string? ValidationStatus { get; }

    public RckInteractionPipelineSummary(
        string kind,
        bool usesRckContext,
        bool usesTraceSlice,
        bool usesContextPack,
        string? validationStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        Kind = kind;
        UsesRckContext = usesRckContext;
        UsesTraceSlice = usesTraceSlice;
        UsesContextPack = usesContextPack;
        ValidationStatus = validationStatus;
    }
}
