using System.Diagnostics;

namespace Rufus.RCK.Workspace;

public sealed record GitWorkspaceContext(string? Branch, string? Commit, bool Dirty)
{
    public static GitWorkspaceContext Capture(string repoRoot)
    {
        var branch = RunGit(repoRoot, "rev-parse", "--abbrev-ref", "HEAD");
        if (string.Equals(branch, "HEAD", StringComparison.Ordinal))
        {
            branch = null;
        }

        var commit = RunGit(repoRoot, "rev-parse", "HEAD");
        var dirtyStatus = RunGit(repoRoot, "status", "--porcelain") ?? string.Empty;
        var dirty = !string.IsNullOrWhiteSpace(dirtyStatus);

        return new GitWorkspaceContext(branch, commit, dirty);
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

        return output.Trim();
    }
}
