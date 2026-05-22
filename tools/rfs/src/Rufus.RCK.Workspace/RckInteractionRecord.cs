using System.Text.RegularExpressions;

namespace Rufus.RCK.Workspace;

public sealed record RckInteractionRecord
{
    public string Mode { get; }

    public string Prompt { get; }

    public string Answer { get; }

    public string AnswerSummary { get; }

    public IReadOnlyList<RckInteractionTool> Tools { get; }

    private RckInteractionRecord(
        string mode,
        string prompt,
        string answer,
        string answerSummary,
        IReadOnlyList<RckInteractionTool> tools)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        Mode = mode;
        Prompt = prompt;
        Answer = answer ?? string.Empty;
        AnswerSummary = answerSummary;
        Tools = tools;
    }

    public static RckInteractionRecord CreateAsk(string prompt, string answer)
    {
        var summary = CreateAnswerSummary(answer);
        return new RckInteractionRecord("ask", prompt, answer, summary, Array.Empty<RckInteractionTool>());
    }

    public static RckInteractionRecord CreateAgent(string prompt, string answer, IEnumerable<RckInteractionTool>? tools = null)
    {
        var summary = CreateAnswerSummary(answer);
        var recordedTools = tools?.ToArray() ?? Array.Empty<RckInteractionTool>();
        return new RckInteractionRecord("agent", prompt, answer, summary, recordedTools);
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
