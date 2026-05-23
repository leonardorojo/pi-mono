using System.Text.RegularExpressions;

namespace Rufus.RCK.Workspace;

public sealed record RckInteractionRecord
{
    public string Mode { get; }

    public string Prompt { get; }

    public string Answer { get; }

    public string AnswerSummary { get; }

    public RckInteractionPipelineSummary? PipelineSummary { get; }

    public string? Provider { get; }

    public string? Model { get; }

    public IReadOnlyList<RckInteractionTool> Tools { get; }

    private RckInteractionRecord(
        string mode,
        string prompt,
        string answer,
        string answerSummary,
        RckInteractionPipelineSummary? pipelineSummary,
        string? provider,
        string? model,
        IReadOnlyList<RckInteractionTool> tools)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        Mode = mode;
        Prompt = prompt;
        Answer = answer ?? string.Empty;
        AnswerSummary = answerSummary;
        PipelineSummary = pipelineSummary;
        Provider = provider;
        Model = model;
        Tools = tools;
    }

    public static RckInteractionRecord CreateAsk(string prompt, string answer)
    {
        var summary = CreateAnswerSummary(answer);
        return new RckInteractionRecord("ask", prompt, answer, summary, null, null, null, Array.Empty<RckInteractionTool>());
    }

    public static RckInteractionRecord CreateAgent(string prompt, string answer, IEnumerable<RckInteractionTool>? tools = null)
    {
        var summary = CreateAnswerSummary(answer);
        var recordedTools = tools?.ToArray() ?? Array.Empty<RckInteractionTool>();
        return new RckInteractionRecord("agent", prompt, answer, summary, null, null, null, recordedTools);
    }

    public static RckInteractionRecord CreateTuiDirect(string prompt, string answer, string? provider = null, string? model = null, RckInteractionPipelineSummary? pipelineSummary = null)
    {
        var summary = CreateAnswerSummary(answer);
        var effectivePipelineSummary = pipelineSummary ?? new RckInteractionPipelineSummary("direct", usesRckContext: false, usesTraceSlice: false, usesContextPack: false, validationStatus: null);
        return new RckInteractionRecord("tui-direct", prompt, answer, summary, effectivePipelineSummary, provider, model, Array.Empty<RckInteractionTool>());
    }

    public static RckInteractionRecord CreateTuiSimple(
        string prompt,
        string answer,
        RckInteractionPipelineSummary pipelineSummary,
        string? provider = null,
        string? model = null)
    {
        var summary = CreateAnswerSummary(answer);
        return new RckInteractionRecord("tui-simple", prompt, answer, summary, pipelineSummary, provider, model, Array.Empty<RckInteractionTool>());
    }

    public static RckInteractionRecord CreateTuiComplete(
        string prompt,
        string answer,
        RckInteractionPipelineSummary pipelineSummary,
        string? provider = null,
        string? model = null)
    {
        var summary = CreateAnswerSummary(answer);
        return new RckInteractionRecord("tui-complete", prompt, answer, summary, pipelineSummary, provider, model, Array.Empty<RckInteractionTool>());
    }

    private static string CreateAnswerSummary(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return string.Empty;
        }

        var normalized = answer.ReplaceLineEndings(" ").Trim();
        var redacted = Regex.Replace(normalized, "`[^`]*`", "`…`");
        if (redacted.Length <= 240)
        {
            return redacted;
        }

        return redacted[..240] + "…";
    }
}
