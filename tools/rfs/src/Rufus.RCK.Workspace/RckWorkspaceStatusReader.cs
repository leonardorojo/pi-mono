namespace Rufus.RCK.Workspace;

public static class RckWorkspaceStatusReader
{
    public static RckWorkspaceStatus Read(string? startingDirectory = null)
    {
        var repoRoot = FindRepoRoot(startingDirectory ?? Directory.GetCurrentDirectory());
        if (repoRoot is null)
        {
            throw new InvalidOperationException("rfs status: repository root not found.");
        }

        var paths = new RckWorkspacePaths(repoRoot);
        var workspaceExists = Directory.Exists(paths.WorkspaceDirectory);
        var configExists = File.Exists(paths.ConfigPath);
        var rckExists = Directory.Exists(paths.RckDirectory);
        var headExists = File.Exists(paths.HeadPath);
        var head = headExists ? ReadHeadStateId(paths.HeadPath) : null;
        var stateCount = CountJsonFiles(paths.StatesDirectory);
        var deltaCount = CountJsonFiles(paths.DeltasDirectory);
        var anchorCount = CountJsonFiles(paths.AnchorsDirectory);
        var gitContext = GitWorkspaceContext.Capture(repoRoot);

        return new RckWorkspaceStatus(
            repoRoot,
            workspaceExists,
            configExists,
            rckExists,
            headExists,
            head,
            stateCount,
            deltaCount,
            anchorCount,
            gitContext);
    }

    private static int CountJsonFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Count();
    }

    private static string? ReadHeadStateId(string headPath)
    {
        try
        {
            var headContent = File.ReadAllText(headPath).Trim();
            return string.IsNullOrWhiteSpace(headContent) ? null : headContent;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? FindRepoRoot(string startingDirectory)
    {
        var current = new DirectoryInfo(startingDirectory);

        while (current is not null)
        {
            var gitEntry = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitEntry) || File.Exists(gitEntry))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
