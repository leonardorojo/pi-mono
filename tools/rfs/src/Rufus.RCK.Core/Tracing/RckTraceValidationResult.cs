namespace Rufus.RCK.Core.Tracing;

public sealed record RckTraceValidationResult
{
    public IReadOnlyList<RckTraceValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;

    public RckTraceValidationResult(IEnumerable<RckTraceValidationIssue>? issues = null)
    {
        Issues = (issues ?? Array.Empty<RckTraceValidationIssue>()).ToArray();
    }
}
