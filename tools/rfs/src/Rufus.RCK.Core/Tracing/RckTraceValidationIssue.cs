namespace Rufus.RCK.Core.Tracing;

public sealed record RckTraceValidationIssue
{
    public string Code { get; }

    public string Message { get; }

    public string? SubjectId { get; }

    public RckTraceValidationIssue(string code, string message, string? subjectId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
        SubjectId = subjectId;
    }
}
