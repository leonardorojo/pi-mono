using System.Text.RegularExpressions;

namespace Rufus.RCK.Workspace;

public sealed record RckInteractionRecord
{
    public string Mode { get; }

    public string Prompt { get; }

    public string Answer { get; }

    public string AnswerSummary { get; }

    private RckInteractionRecord(string mode, string prompt, string answer, string answerSummary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        Mode = mode;
        Prompt = prompt;
        Answer = answer ?? string.Empty;
        AnswerSummary = answerSummary;
    }

    public static RckInteractionRecord CreateAsk(string prompt, string answer)
    {
        var summary = CreateAnswerSummary(answer);
        return new RckInteractionRecord("ask", prompt, answer, summary);
    }

    private static string CreateAnswerSummary(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return string.Empty;
        }

        var normalized = answer.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= 240)
        {
            return normalized;
        }

        return normalized[..240] + "…";
    }
}
