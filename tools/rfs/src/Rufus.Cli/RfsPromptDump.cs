namespace Rufus.Cli;

public static class RfsPromptDump
{
    private const string EnvVarName = "RFS_TRACE_SLICE_DUMP_PROMPTS";

    /// <summary>
    /// Returns true when the opt-in prompt dump environment variable is active.
    /// </summary>
    internal static bool IsEnabled => string.Equals(
        Environment.GetEnvironmentVariable(EnvVarName),
        "1",
        StringComparison.Ordinal);

    /// <summary>
    /// Attempts to dump the given LLM prompt to a timestamped file under /tmp.
    /// Returns the absolute path of the dumped file, or null when the env var is not set.
    /// A commented header with stage, model, source, promptLen, and workingDirectory
    /// metadata is prepended to the prompt body.
    /// </summary>
    internal static string? TryDump(
        string stage,
        string prompt,
        string model,
        string source,
        string workingDirectory)
    {
        if (!IsEnabled) return null;

        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var fileName = $"/tmp/rfs-{stage}-{timestamp}.prompt.txt";

        var header = new System.Text.StringBuilder();
        header.AppendLine($"# stage={stage}");
        header.AppendLine($"# model={model}");
        header.AppendLine($"# source={source}");
        header.AppendLine($"# promptLen={prompt.Length}");
        header.AppendLine($"# workingDirectory={workingDirectory}");
        header.AppendLine();

        System.IO.File.WriteAllText(fileName, header + prompt);

        return fileName;
    }
}
