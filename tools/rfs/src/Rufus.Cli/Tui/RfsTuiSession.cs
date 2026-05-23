using Rufus.RCK.Workspace;

namespace Rufus.Cli.Tui;

internal static class RfsTuiSession
{
    public static int Run(string? startingDirectory = null)
    {
        var inputDirectory = startingDirectory ?? Directory.GetCurrentDirectory();

        RckWorkspaceStatus status;
        try
        {
            status = RckWorkspaceStatusReader.Read(inputDirectory);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        if (!status.WorkspaceExists)
        {
            var initResult = RckWorkspaceInitializer.Initialize(inputDirectory);
            if (!initResult.Success)
            {
                if (!string.IsNullOrWhiteSpace(initResult.ErrorMessage))
                {
                    Console.Error.WriteLine(initResult.ErrorMessage);
                }

                return 1;
            }

            RenderAutoInit(initResult);
            Console.WriteLine();
            Console.WriteLine("Entering RFS session.");
            Console.WriteLine();

            status = RckWorkspaceStatusReader.Read(inputDirectory);
        }

        RenderHeader(status, RckWorkspaceModelConfigStore.TryReadDefaultModel(status.RepoRoot));
        RunPromptLoop(status.RepoRoot);
        return 0;
    }

    private static void RenderAutoInit(RckWorkspaceInitResult initResult)
    {
        Console.WriteLine("RFS");
        Console.WriteLine("────────────────────────");
        Console.WriteLine("Workspace not initialized.");
        Console.WriteLine();
        Console.WriteLine("Initializing RFS workspace...");
        Console.WriteLine(initResult.ConfigCreated ? "✓ .rfs created" : "• .rfs already existed");
        Console.WriteLine(initResult.RckDirectoriesCreated || initResult.HeadCreated || initResult.StateCreated || initResult.AnchorCreated
            ? "✓ RCK initialized"
            : "• RCK already initialized");
        Console.WriteLine(initResult.StateCreated ? "✓ genesis state created" : "• genesis state already existed");
        Console.WriteLine(initResult.AnchorCreated ? "✓ genesis anchor created" : "• genesis anchor already existed");
    }

    private static void RenderHeader(RckWorkspaceStatus status, string? workspaceModel)
    {
        var repoName = Path.GetFileName(Path.TrimEndingDirectorySeparator(status.RepoRoot));
        var modelLabel = string.IsNullOrWhiteSpace(workspaceModel) ? "(inherited)" : workspaceModel.Trim();
        var branchLabel = string.IsNullOrWhiteSpace(status.GitContext.Branch) ? "(detached)" : status.GitContext.Branch;
        var dirtyLabel = status.GitContext.Dirty.ToString().ToLowerInvariant();

        Console.WriteLine($"RFS · {repoName}");
        Console.WriteLine("────────────────────────");
        Console.WriteLine($"Model: {modelLabel}");
        Console.WriteLine($"RCK: states {status.StateCount} · deltas {status.DeltaCount} · anchors {status.AnchorCount}");
        Console.WriteLine($"Git: {branchLabel} · dirty {dirtyLabel}");
        Console.WriteLine();
    }

    private static void RunPromptLoop(string repoRoot)
    {
        Console.CancelKeyPress += HandleCancelKeyPress;
        try
        {
            while (true)
            {
                Console.Write("> ");
                var line = Console.ReadLine();
                if (line is null)
                {
                    break;
                }

                var input = line.Trim();
                if (input.Length == 0)
                {
                    continue;
                }

                if (IsExitCommand(input))
                {
                    break;
                }

                if (string.Equals(input, "/status", StringComparison.Ordinal))
                {
                    RenderStatus(repoRoot);
                    continue;
                }

                if (string.Equals(input, "/help", StringComparison.Ordinal))
                {
                    RenderHelp();
                    continue;
                }

                Console.WriteLine("Prompt received.");
                Console.WriteLine("Mode selection will be implemented in PT3.");
            }
        }
        finally
        {
            Console.CancelKeyPress -= HandleCancelKeyPress;
        }
    }

    private static void RenderStatus(string repoRoot)
    {
        try
        {
            var status = RckWorkspaceStatusReader.Read(repoRoot);
            foreach (var line in status.FormatConsoleLines())
            {
                Console.WriteLine(line);
            }
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
    }

    private static void RenderHelp()
    {
        Console.WriteLine("RFS TUI");
        Console.WriteLine();
        Console.WriteLine("Internal commands:");
        Console.WriteLine("  /status");
        Console.WriteLine("  /help");
        Console.WriteLine("  /exit");
        Console.WriteLine();
        Console.WriteLine("Prompt processing modes will be implemented in later PT phases.");
    }

    private static void HandleCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        Console.WriteLine();
        Environment.Exit(0);
    }

    private static bool IsExitCommand(string input)
    {
        return string.Equals(input, "/exit", StringComparison.Ordinal) || string.Equals(input, "exit", StringComparison.Ordinal);
    }
}
