namespace Rufus.Cli.Json;

public static class LlmJsonOutputNormalizer
{
    public static string Normalize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var trimmed = json.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var lines = trimmed.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        if (lines.Length < 3)
        {
            return trimmed;
        }

        if (!lines[0].StartsWith("```", StringComparison.Ordinal) || !string.Equals(lines[^1].Trim(), "```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return string.Join(Environment.NewLine, lines[1..^1]).Trim();
    }
}
