using System.Diagnostics;

namespace Rufus.RCK.Workspace;

public sealed record GitWorkspaceContext(
    string? Branch,
    string? Commit,
    bool Dirty,
    IReadOnlyList<GitWorkspaceArtifactChange> ChangedArtifacts)
{
    public static GitWorkspaceContext Capture(string repoRoot)
    {
        var branch = RunGit(repoRoot, "rev-parse", "--abbrev-ref", "HEAD")?.Trim();
        if (string.Equals(branch, "HEAD", StringComparison.Ordinal))
        {
            branch = null;
        }

        var commit = RunGit(repoRoot, "rev-parse", "HEAD")?.Trim();
        var dirtyStatus = RunGit(repoRoot, "status", "--porcelain") ?? string.Empty;
        var changedArtifacts = ParseChangedArtifacts(dirtyStatus);
        var dirty = !string.IsNullOrWhiteSpace(dirtyStatus);

        return new GitWorkspaceContext(branch, commit, dirty, changedArtifacts);
    }

    private static IReadOnlyList<GitWorkspaceArtifactChange> ParseChangedArtifacts(string dirtyStatus)
    {
        if (string.IsNullOrWhiteSpace(dirtyStatus))
        {
            return Array.Empty<GitWorkspaceArtifactChange>();
        }

        var artifacts = new List<GitWorkspaceArtifactChange>();
        using var reader = new StringReader(dirtyStatus);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Length < 3)
            {
                continue;
            }

            var statusCode = line[..2];
            if (statusCode == "!!")
            {
                continue;
            }

            var rawPath = line[3..];
            var path = ExtractPath(rawPath, statusCode);
            if (string.IsNullOrWhiteSpace(path) || ShouldExcludePath(path))
            {
                continue;
            }

            artifacts.Add(new GitWorkspaceArtifactChange(
                Kind: "file",
                Path: path,
                ChangeType: MapChangeType(statusCode),
                GitStatus: statusCode,
                Source: "git-status"));
        }

        return artifacts;
    }

    private static string ExtractPath(string rawPath, string statusCode)
    {
        var path = rawPath.Trim();
        if (statusCode.Contains('R') || statusCode.Contains('C'))
        {
            var separator = " -> ";
            var separatorIndex = path.LastIndexOf(separator, StringComparison.Ordinal);
            if (separatorIndex >= 0)
            {
                path = path[(separatorIndex + separator.Length)..].Trim();
            }
        }

        return path;
    }

    private static string MapChangeType(string statusCode)
    {
        if (statusCode == "??")
        {
            return "untracked";
        }

        if (statusCode.Contains('R'))
        {
            return "renamed";
        }

        if (statusCode.Contains('A'))
        {
            return "added";
        }

        if (statusCode.Contains('D'))
        {
            return "deleted";
        }

        if (statusCode.Contains('M'))
        {
            return "modified";
        }

        return "unknown";
    }

    private static bool ShouldExcludePath(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (string.Equals(segment, ".rfs", StringComparison.Ordinal)
                || string.Equals(segment, "bin", StringComparison.Ordinal)
                || string.Equals(segment, "obj", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? RunGit(string workingDirectory, params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
        };

        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi);
        if (process is null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            return null;
        }

        return output;
    }
}
