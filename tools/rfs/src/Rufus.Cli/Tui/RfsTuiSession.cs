using Rufus.Cli.PiIntegration;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.Tui;

internal static class RfsTuiSession
{
    public static int Run(string? startingDirectory = null)
        => RunAsync(startingDirectory ?? Directory.GetCurrentDirectory()).GetAwaiter().GetResult();

    private static async Task<int> RunAsync(string inputDirectory)
    {
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
        await RunPromptLoopAsync(status.RepoRoot);
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

    private static async Task RunPromptLoopAsync(string repoRoot)
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

                if (await RunPromptModeSelectionAsync(input, repoRoot))
                {
                    break;
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= HandleCancelKeyPress;
        }
    }

    private static async Task<bool> RunPromptModeSelectionAsync(string prompt, string repoRoot)
    {
        RenderModeSelectionMenu();

        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line is null)
            {
                return true;
            }

            var selectionInput = line.Trim();
            if (selectionInput.Length == 0)
            {
                continue;
            }

            if (string.Equals(selectionInput, "/help", StringComparison.Ordinal))
            {
                RenderModeSelectionHelp();
                continue;
            }

            var selection = RfsTuiModeSelectionParser.ParseModeSelection(selectionInput);
            switch (selection)
            {
                case RfsTuiModeSelection.Direct:
                    return await RunDirectModeAsync(repoRoot, prompt);
                case RfsTuiModeSelection.Simple:
                    RenderModeSelectionStub("Simple mode", prompt, "PT6");
                    return false;
                case RfsTuiModeSelection.Complete:
                    RenderModeSelectionStub("Complete mode", prompt, "PT7");
                    return false;
                case RfsTuiModeSelection.Plan:
                    RenderModeSelectionStub("Plan mode", prompt, "PT8");
                    return false;
                case RfsTuiModeSelection.Cancel:
                    Console.WriteLine("Prompt cancelled.");
                    return false;
                case RfsTuiModeSelection.Exit:
                    return true;
                default:
                    Console.WriteLine("Invalid mode. Choose 1, 2, 3, 4, or /cancel.");
                    break;
            }
        }
    }

    private static async Task<bool> RunDirectModeAsync(string repoRoot, string prompt)
    {
        Console.WriteLine("[Direct mode]");
        Console.WriteLine();

        var askJsonResult = await PiJsonEventRunner.RunAskAsync(
            repoRoot,
            prompt,
            RckWorkspaceModelConfigStore.TryReadDefaultModel(repoRoot));

        if (!askJsonResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(askJsonResult.ErrorMessage))
            {
                Console.Error.WriteLine(askJsonResult.ErrorMessage);
            }

            return false;
        }

        Console.WriteLine("Respuesta:");
        Console.WriteLine("────────────────────────────────────────────");

        if (string.IsNullOrWhiteSpace(askJsonResult.Answer))
        {
            Console.WriteLine("(no assistant output)");
        }
        else
        {
            foreach (var answerLine in askJsonResult.Answer.Split('\n', StringSplitOptions.None))
            {
                Console.WriteLine(answerLine);
            }
        }

        var recordResult = RckInteractionRecorder.RecordTui(
            new RckTuiInteractionRecordInput(prompt, askJsonResult.Answer, askJsonResult.Provider, askJsonResult.Model),
            repoRoot);
        if (!recordResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(recordResult.ErrorMessage))
            {
                Console.Error.WriteLine(recordResult.ErrorMessage);
            }

            return false;
        }

        Console.WriteLine();
        Console.WriteLine("State created:");
        Console.WriteLine($"  {recordResult.StateId?.ToString() ?? "(unknown)"}");
        Console.WriteLine("Delta created:");
        Console.WriteLine($"  {recordResult.DeltaId?.ToString() ?? "(unknown)"}");

        return false;
    }

    private static void RenderModeSelectionMenu()
    {
        Console.WriteLine("¿Cómo querés procesar este prompt?");
        Console.WriteLine();
        Console.WriteLine("1. Directo");
        Console.WriteLine("   Preguntar al LLM sin contexto RCK.");
        Console.WriteLine();
        Console.WriteLine("2. Simple");
        Console.WriteLine("   Usar últimas 5 interacciones + git status + artifacts metadata.");
        Console.WriteLine();
        Console.WriteLine("3. Completo");
        Console.WriteLine("   Intent + TraceSliceProposal + Validation + ContextPack.");
        Console.WriteLine();
        Console.WriteLine("4. Plan");
        Console.WriteLine("   Generar plan de implementación sin tocar código.");
    }

    private static void RenderModeSelectionHelp()
    {
        Console.WriteLine("Mode selection commands:");
        Console.WriteLine("  1 Directo");
        Console.WriteLine("  2 Simple");
        Console.WriteLine("  3 Completo");
        Console.WriteLine("  4 Plan");
        Console.WriteLine("  /cancel");
        Console.WriteLine("  /help");
        Console.WriteLine("  /exit");
    }

    private static void RenderModeSelectionStub(string modeLabel, string prompt, string ptLabel)
    {
        Console.WriteLine($"[{modeLabel}]");
        Console.WriteLine("Prompt:");
        Console.WriteLine($"  {prompt}");
        Console.WriteLine();
        Console.WriteLine($"Mode execution will be implemented in {ptLabel}.");
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
        Console.WriteLine("Escribí un prompt directamente.");
        Console.WriteLine("Después elegí modo:");
        Console.WriteLine("  1 Directo");
        Console.WriteLine("  2 Simple");
        Console.WriteLine("  3 Completo");
        Console.WriteLine("  4 Plan");
        Console.WriteLine();
        Console.WriteLine("Comandos internos:");
        Console.WriteLine("  /status");
        Console.WriteLine("  /help");
        Console.WriteLine("  /exit");
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
