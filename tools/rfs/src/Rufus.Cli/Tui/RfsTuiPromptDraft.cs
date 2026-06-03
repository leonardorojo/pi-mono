using System.Globalization;
using System.Linq;

namespace Rufus.Cli.Tui;

internal sealed record RfsTuiPromptDraft(
    string Source,
    string Text,
    string? AttachmentPath,
    int LineCount,
    int CharCount,
    int EstimatedTokens)
{
    internal static RfsTuiPromptDraft CreateTyped(string text)
        => new("typed", text, null, CountLines(text), text.Length, EstimateTokens(text));

    internal static RfsTuiPromptDraft CreatePaste(string text, string attachmentPath)
        => new("paste", text, attachmentPath, CountLines(text), text.Length, EstimateTokens(text));

    internal string ResolveText()
        => AttachmentPath is null ? Text : File.ReadAllText(AttachmentPath);

    internal string ReferenceLabel()
        => AttachmentPath is null ? "inline prompt" : Path.GetFileName(AttachmentPath);

    private static int CountLines(string text)
        => string.IsNullOrEmpty(text) ? 0 : text.Split('\n').Length;

    private static int EstimateTokens(string text)
        => string.IsNullOrWhiteSpace(text)
            ? 0
            : Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
}

internal static class RfsTuiPasteCapture
{
    private const string PasteCapturePrompt = "paste> ";

    internal static RfsTuiPromptDraft? CaptureInteractive(string repoRoot)
    {
        RfsTuiRenderer.WritePasteCaptureIntro();

        var lines = new List<string>();
        while (true)
        {
            RfsTuiRenderer.WritePasteCapturePrompt();
            var line = Console.ReadLine();
            if (line is null)
            {
                return null;
            }

            var trimmed = line.Trim();
            if (string.Equals(trimmed, "/cancel", StringComparison.Ordinal))
            {
                Console.WriteLine("Paste discarded.");
                return null;
            }

            if (string.Equals(trimmed, "/end", StringComparison.Ordinal))
            {
                if (lines.Count == 0)
                {
                    RfsTuiRenderer.WriteWarningLine("Paste capture is empty.");
                    continue;
                }

                return FinalizeCapture(repoRoot, lines);
            }

            lines.Add(line);
        }
    }

    private static RfsTuiPromptDraft? FinalizeCapture(string repoRoot, IReadOnlyList<string> lines)
    {
        var content = string.Join(Environment.NewLine, lines);
        var attachmentPath = SavePasteContent(repoRoot, content);
        if (attachmentPath is null)
        {
            return null;
        }

        Console.WriteLine();
        return RfsTuiPromptDraft.CreatePaste(content, attachmentPath);
    }

    private static string? SavePasteContent(string repoRoot, string content)
    {
        try
        {
            var pasteDirectory = Path.Combine(repoRoot, ".rfs", "tmp", "pastes");
            Directory.CreateDirectory(pasteDirectory);

            var baseName = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + "_paste.md";
            var attachmentPath = Path.Combine(pasteDirectory, baseName);
            var attempt = 0;
            while (File.Exists(attachmentPath))
            {
                attempt++;
                attachmentPath = Path.Combine(pasteDirectory, DateTimeOffset.UtcNow.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + $"_{attempt}_paste.md");
            }

            File.WriteAllText(attachmentPath, content);
            return attachmentPath;
        }
        catch (Exception ex)
        {
            RfsTuiRenderer.WriteWarningLine($"Failed to save paste capture: {ex.Message}");
            return null;
        }
    }
}
