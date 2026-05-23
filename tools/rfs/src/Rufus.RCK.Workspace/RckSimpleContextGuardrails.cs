namespace Rufus.RCK.Workspace;

public sealed record RckSimpleContextGuardrails(
    bool IncludeFileContents,
    bool IncludeGitDiffs,
    bool IncludeJsonl,
    bool IncludeStdoutStderr,
    bool IncludeToolOutputs,
    bool IncludeFullContextPack,
    bool IncludeFullTraceSlice,
    bool IncludePayloadCanonicalJson);
