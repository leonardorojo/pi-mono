using System.Globalization;
using System.Text;
using Rufus.Cli.PiIntegration;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.Tui;

internal static class RfsTuiSession
{
    private static readonly RfsTuiSessionState SessionState = new();

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

                if (string.Equals(input, "/log", StringComparison.Ordinal))
                {
                    RenderLog(repoRoot);
                    continue;
                }

                if (string.Equals(input, "/context", StringComparison.Ordinal))
                {
                    RenderContext();
                    continue;
                }

                if (string.Equals(input, "/trace", StringComparison.Ordinal))
                {
                    RenderTrace();
                    continue;
                }

                if (TryHandleTopLevelCommand(input, repoRoot))
                {
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
                    return await RunSimpleModeAsync(repoRoot, prompt);
                case RfsTuiModeSelection.Complete:
                    return await RunCompleteModeAsync(repoRoot, prompt);
                case RfsTuiModeSelection.Plan:
                    return await RunPlanModeAsync(repoRoot, prompt);
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

    private static async Task<bool> RunSimpleModeAsync(string repoRoot, string prompt)
    {
        Console.WriteLine("[Simple mode]");
        Console.WriteLine("Building Simple Context...");

        var simpleContextBuildResult = RckSimpleContextBuilder.Build(repoRoot, prompt);
        var simpleContext = simpleContextBuildResult.Context;
        var contextUsageReport = BuildContextUsageReport(
            simpleContext.Budget.EstimatedChars,
            simpleContext.Budget.EstimatedTokens,
            modelBudgetTokens: null,
            simpleContext.Budget.Truncated);

        Console.WriteLine($"  recent interactions: {simpleContext.RecentInteractions.Count}");
        Console.WriteLine($"  anchors: {simpleContext.Anchors.Count}");
        Console.WriteLine($"  artifacts: {simpleContext.Artifacts.Count}");
        Console.WriteLine($"  estimated chars: {contextUsageReport.EstimatedChars}");
        Console.WriteLine($"  estimated tokens: {contextUsageReport.EstimatedTokens}");
        Console.WriteLine($"  model budget: {FormatNullableInt(contextUsageReport.ModelBudgetTokens)}");
        Console.WriteLine($"  context usage: {FormatNullablePercentage(contextUsageReport.ContextUsageRatio)}");
        Console.WriteLine($"  transport size: {contextUsageReport.TransportSizeChars} chars");
        Console.WriteLine($"  transport risk: {contextUsageReport.TransportRisk}");
        Console.WriteLine($"  truncated: {contextUsageReport.Truncated.ToString().ToLowerInvariant()}");

        var askJsonResult = await PiJsonEventRunner.RunAskAsync(
            repoRoot,
            simpleContextBuildResult.PromptToSend,
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

        var pipelineSummary = new RckInteractionPipelineSummary(
            "simple",
            usesRckContext: true,
            usesTraceSlice: false,
            usesContextPack: false,
            validationStatus: null,
            recentInteractionCount: simpleContext.RecentInteractions.Count,
            selectedStateIds: simpleContext.RecentInteractions.Select(interaction => interaction.StateId).ToArray(),
            selectedDeltaIds: simpleContext.RecentInteractions.Where(interaction => !string.IsNullOrWhiteSpace(interaction.DeltaId)).Select(interaction => interaction.DeltaId!).ToArray(),
            selectedAnchorIds: simpleContext.Anchors.Select(anchor => anchor.Id).ToArray(),
            artifactRefCount: simpleContext.Artifacts.Count,
            estimatedChars: contextUsageReport.EstimatedChars,
            estimatedTokens: contextUsageReport.EstimatedTokens,
            modelBudgetTokens: contextUsageReport.ModelBudgetTokens,
            contextUsageRatio: contextUsageReport.ContextUsageRatio,
            transportSizeChars: contextUsageReport.TransportSizeChars,
            transportRisk: contextUsageReport.TransportRisk,
            truncated: contextUsageReport.Truncated,
            omissions: simpleContextBuildResult.Omissions.ToArray());

        var recordResult = RckInteractionRecorder.RecordTui(
            new RckTuiInteractionRecordInput(
                prompt,
                askJsonResult.Answer,
                askJsonResult.Provider,
                askJsonResult.Model,
                mode: "tui-simple",
                pipelineSummary: pipelineSummary),
            repoRoot);
        if (!recordResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(recordResult.ErrorMessage))
            {
                Console.Error.WriteLine(recordResult.ErrorMessage);
            }

            return false;
        }

        SessionState.RecordSimple(
            new RfsTuiSimpleContextSummary(
                simpleContext.RecentInteractions.Count,
                simpleContext.Anchors.Count,
                simpleContext.Artifacts.Count,
                contextUsageReport.EstimatedChars,
                contextUsageReport.EstimatedTokens,
                FormatNullableInt(contextUsageReport.ModelBudgetTokens),
                FormatNullablePercentage(contextUsageReport.ContextUsageRatio),
                contextUsageReport.TransportSizeChars,
                contextUsageReport.TransportRisk,
                contextUsageReport.Truncated,
                simpleContextBuildResult.Warnings.ToArray(),
                simpleContextBuildResult.Omissions.ToArray()),
            mode: "simple");

        Console.WriteLine();
        Console.WriteLine("State created:");
        Console.WriteLine($"  {recordResult.StateId?.ToString() ?? "(unknown)"}");
        Console.WriteLine("Delta created:");
        Console.WriteLine($"  {recordResult.DeltaId?.ToString() ?? "(unknown)"}");

        return false;
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

        SessionState.RecordDirect();

        Console.WriteLine();
        Console.WriteLine("State created:");
        Console.WriteLine($"  {recordResult.StateId?.ToString() ?? "(unknown)"}");
        Console.WriteLine("Delta created:");
        Console.WriteLine($"  {recordResult.DeltaId?.ToString() ?? "(unknown)"}");

        return false;
    }

    private static async Task<bool> RunCompleteModeAsync(string repoRoot, string prompt)
    {
        Console.WriteLine("[Complete mode]");
        Console.WriteLine("[1/5] Inferring intent...");
        Console.WriteLine("[2/5] Building TraceSlice proposal...");
        Console.WriteLine("[3/5] Validating proposal...");
        Console.WriteLine("[4/5] Building ContextPack...");
        Console.WriteLine("[5/5] Asking main LLM...");
        Console.WriteLine();

        var completeResult = await RfsCompleteModePipeline.BuildAsync(prompt, repoRoot);
        var completeContextUsageReport = BuildContextUsageReport(
            completeResult.EstimatedChars,
            completeResult.EstimatedTokens,
            modelBudgetTokens: null,
            completeResult.Truncated);
        if (!completeResult.Success || string.IsNullOrWhiteSpace(completeResult.PromptToSend))
        {
            if (!string.IsNullOrWhiteSpace(completeResult.ErrorMessage))
            {
                Console.Error.WriteLine(completeResult.ErrorMessage);
            }

            return false;
        }

        Console.WriteLine("Context:");
        Console.WriteLine($"  selection: {completeResult.TraceSliceSelectionStrategy ?? "(unknown)"}");
        Console.WriteLine($"  validation: {completeResult.ValidationStatus ?? "(unknown)"}");
        Console.WriteLine($"  states: {completeResult.SelectedStateIds.Count}");
        Console.WriteLine($"  deltas: {completeResult.SelectedDeltaIds.Count}");
        Console.WriteLine($"  anchors: {completeResult.SelectedAnchorIds.Count}");
        Console.WriteLine($"  artifact refs: {completeResult.ArtifactRefCount}");
        Console.WriteLine($"  estimated chars: {completeResult.EstimatedChars}");
        Console.WriteLine($"  estimated tokens: {completeResult.EstimatedTokens}");
        Console.WriteLine($"  model budget: {FormatNullableInt(completeContextUsageReport.ModelBudgetTokens)}");
        Console.WriteLine($"  context usage: {FormatNullablePercentage(completeContextUsageReport.ContextUsageRatio)}");
        Console.WriteLine($"  transport size: {completeContextUsageReport.TransportSizeChars} chars");
        Console.WriteLine($"  transport risk: {completeContextUsageReport.TransportRisk}");
        Console.WriteLine($"  truncated: {completeResult.Truncated.ToString().ToLowerInvariant()}");

        if (completeResult.Warnings.Count > 0)
        {
            Console.WriteLine("  warnings:");
            foreach (var warning in completeResult.Warnings)
            {
                Console.WriteLine($"    - {warning}");
            }
        }

        if (completeResult.Omissions.Count > 0)
        {
            Console.WriteLine("  omissions:");
            foreach (var omission in completeResult.Omissions)
            {
                Console.WriteLine($"    - {omission}");
            }
        }

        Console.WriteLine();

        var askJsonResult = await PiJsonEventRunner.RunAskAsync(
            repoRoot,
            completeResult.PromptToSend,
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

        var pipelineSummary = new RckInteractionPipelineSummary(
            "complete",
            usesRckContext: true,
            usesTraceSlice: true,
            usesContextPack: true,
            validationStatus: completeResult.ValidationStatus,
            traceSliceSelectionStrategy: completeResult.TraceSliceSelectionStrategy,
            contextPackScope: completeResult.ContextPackScope,
            intentKind: completeResult.IntentKind,
            intentSummary: completeResult.IntentSummary,
            proposalSummary: completeResult.ProposalSummary,
            proposalSource: completeResult.ProposalSource,
            materializationPolicySummary: completeResult.MaterializationPolicySummary,
            selectedStateIds: completeResult.SelectedStateIds,
            selectedDeltaIds: completeResult.SelectedDeltaIds,
            selectedAnchorIds: completeResult.SelectedAnchorIds,
            artifactRefCount: completeResult.ArtifactRefCount,
            estimatedChars: completeContextUsageReport.EstimatedChars,
            estimatedTokens: completeContextUsageReport.EstimatedTokens,
            modelBudgetTokens: completeContextUsageReport.ModelBudgetTokens,
            contextUsageRatio: completeContextUsageReport.ContextUsageRatio,
            transportSizeChars: completeContextUsageReport.TransportSizeChars,
            transportRisk: completeContextUsageReport.TransportRisk,
            truncated: completeContextUsageReport.Truncated,
            warnings: completeResult.Warnings,
            omissions: completeResult.Omissions);

        var recordResult = RckInteractionRecorder.RecordTui(
            new RckTuiInteractionRecordInput(
                prompt,
                askJsonResult.Answer,
                askJsonResult.Provider,
                askJsonResult.Model,
                mode: "tui-complete",
                pipelineSummary: pipelineSummary),
            repoRoot);
        if (!recordResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(recordResult.ErrorMessage))
            {
                Console.Error.WriteLine(recordResult.ErrorMessage);
            }

            return false;
        }

        SessionState.RecordComplete(
            new RfsTuiCompleteContextSummary(
                completeResult.TraceSliceSelectionStrategy,
                completeResult.ValidationStatus,
                completeResult.ContextPackScope,
                completeResult.SelectedStateIds.Count,
                completeResult.SelectedDeltaIds.Count,
                completeResult.SelectedAnchorIds.Count,
                completeContextUsageReport.EstimatedChars,
                completeContextUsageReport.EstimatedTokens,
                completeContextUsageReport.TransportRisk,
                completeContextUsageReport.Truncated,
                completeResult.Warnings.ToArray(),
                completeResult.Omissions.ToArray()));

        Console.WriteLine();
        Console.WriteLine("State created:");
        Console.WriteLine($"  {recordResult.StateId?.ToString() ?? "(unknown)"}");
        Console.WriteLine("Delta created:");
        Console.WriteLine($"  {recordResult.DeltaId?.ToString() ?? "(unknown)"}");

        return false;
    }

    private static async Task<bool> RunPlanModeAsync(string repoRoot, string prompt)
    {
        Console.WriteLine("[Plan mode]");
        Console.WriteLine("Building planning context...");

        var simpleContextBuildResult = RckSimpleContextBuilder.Build(repoRoot, prompt);
        var simpleContext = simpleContextBuildResult.Context;
        var contextUsageReport = BuildContextUsageReport(
            simpleContext.Budget.EstimatedChars,
            simpleContext.Budget.EstimatedTokens,
            modelBudgetTokens: null,
            simpleContext.Budget.Truncated);
        var planPromptToSend = BuildPlanPromptToSend(simpleContext);

        var recentInteractionCount = simpleContext.RecentInteractions.Count;
        var selectedStateIds = simpleContext.RecentInteractions.Select(interaction => interaction.StateId).ToArray();
        var selectedDeltaIds = simpleContext.RecentInteractions.Where(interaction => !string.IsNullOrWhiteSpace(interaction.DeltaId)).Select(interaction => interaction.DeltaId!).ToArray();
        var selectedAnchorIds = simpleContext.Anchors.Select(anchor => anchor.Id).ToArray();

        Console.WriteLine("  context: simple");
        Console.WriteLine($"  recent interactions: {recentInteractionCount}");
        Console.WriteLine($"  anchors: {selectedAnchorIds.Length}");
        Console.WriteLine($"  artifacts: {simpleContext.Artifacts.Count}");
        Console.WriteLine($"  estimated chars: {contextUsageReport.EstimatedChars}");
        Console.WriteLine($"  estimated tokens: {contextUsageReport.EstimatedTokens}");
        Console.WriteLine($"  model budget: {FormatNullableInt(contextUsageReport.ModelBudgetTokens)}");
        Console.WriteLine($"  context usage: {FormatNullablePercentage(contextUsageReport.ContextUsageRatio)}");
        Console.WriteLine($"  transport size: {contextUsageReport.TransportSizeChars} chars");
        Console.WriteLine($"  transport risk: {contextUsageReport.TransportRisk}");
        Console.WriteLine($"  truncated: {contextUsageReport.Truncated.ToString().ToLowerInvariant()}");

        if (simpleContextBuildResult.Warnings.Count > 0)
        {
            Console.WriteLine("  warnings:");
            foreach (var warning in simpleContextBuildResult.Warnings)
            {
                Console.WriteLine($"    - {warning}");
            }
        }

        if (simpleContextBuildResult.Omissions.Count > 0)
        {
            Console.WriteLine("  omissions:");
            foreach (var omission in simpleContextBuildResult.Omissions)
            {
                Console.WriteLine($"    - {omission}");
            }
        }

        Console.WriteLine();

        var askJsonResult = await PiJsonEventRunner.RunAskAsync(
            repoRoot,
            planPromptToSend,
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

        var pipelineSummary = new RckInteractionPipelineSummary(
            "plan",
            usesRckContext: true,
            usesTraceSlice: false,
            usesContextPack: false,
            validationStatus: null,
            contextMode: "simple",
            recentInteractionCount: recentInteractionCount,
            selectedStateIds: selectedStateIds,
            selectedDeltaIds: selectedDeltaIds,
            selectedAnchorIds: selectedAnchorIds,
            artifactRefCount: simpleContext.Artifacts.Count,
            estimatedChars: contextUsageReport.EstimatedChars,
            estimatedTokens: contextUsageReport.EstimatedTokens,
            modelBudgetTokens: contextUsageReport.ModelBudgetTokens,
            contextUsageRatio: contextUsageReport.ContextUsageRatio,
            transportSizeChars: contextUsageReport.TransportSizeChars,
            transportRisk: contextUsageReport.TransportRisk,
            truncated: contextUsageReport.Truncated,
            warnings: simpleContextBuildResult.Warnings,
            omissions: simpleContextBuildResult.Omissions);

        var recordResult = RckInteractionRecorder.RecordTui(
            new RckTuiInteractionRecordInput(
                prompt,
                askJsonResult.Answer,
                askJsonResult.Provider,
                askJsonResult.Model,
                mode: "tui-plan",
                pipelineSummary: pipelineSummary),
            repoRoot);
        if (!recordResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(recordResult.ErrorMessage))
            {
                Console.Error.WriteLine(recordResult.ErrorMessage);
            }

            return false;
        }

        SessionState.RecordPlan(
            new RfsTuiSimpleContextSummary(
                recentInteractionCount,
                selectedAnchorIds.Length,
                simpleContext.Artifacts.Count,
                contextUsageReport.EstimatedChars,
                contextUsageReport.EstimatedTokens,
                FormatNullableInt(contextUsageReport.ModelBudgetTokens),
                FormatNullablePercentage(contextUsageReport.ContextUsageRatio),
                contextUsageReport.TransportSizeChars,
                contextUsageReport.TransportRisk,
                contextUsageReport.Truncated,
                simpleContextBuildResult.Warnings.ToArray(),
                simpleContextBuildResult.Omissions.ToArray()));

        Console.WriteLine();
        Console.WriteLine("State created:");
        Console.WriteLine($"  {recordResult.StateId?.ToString() ?? "(unknown)"}");
        Console.WriteLine("Delta created:");
        Console.WriteLine($"  {recordResult.DeltaId?.ToString() ?? "(unknown)"}");

        return false;
    }

    private static RckContextUsageReport BuildContextUsageReport(
        int estimatedChars,
        int estimatedTokens,
        int? modelBudgetTokens,
        bool truncated)
    {
        return RckContextUsageEstimator.Create(estimatedChars, estimatedTokens, modelBudgetTokens, truncated);
    }

    private static string FormatNullableInt(int? value)
        => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "unknown";

    private static string FormatNullablePercentage(double? value)
        => value.HasValue ? value.Value.ToString("P1", CultureInfo.InvariantCulture) : "unknown";

    private static string BuildPlanPromptToSend(RckSimpleContext simpleContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are assisting inside an RFS repository session.");
        sb.AppendLine("Use the provided Simple Context to create a safe implementation plan.");
        sb.AppendLine("Do not modify files.");
        sb.AppendLine("Do not propose applying patches.");
        sb.AppendLine("Do not assume file contents unless provided.");
        sb.AppendLine("Return a concise, actionable plan.");
        sb.AppendLine();
        sb.AppendLine("[Simple Context]");
        sb.Append(simpleContext.Render());
        sb.AppendLine();
        sb.AppendLine("[User Prompt]");
        sb.AppendLine(simpleContext.Prompt.Text);
        sb.AppendLine();
        sb.AppendLine("Produce a plan only.");
        return sb.ToString();
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
            var modelReadResult = RckWorkspaceModelConfigStore.Read(repoRoot);

            Console.WriteLine("RCK:");
            Console.WriteLine($"  states: {status.StateCount}");
            Console.WriteLine($"  deltas: {status.DeltaCount}");
            Console.WriteLine($"  anchors: {status.AnchorCount}");
            Console.WriteLine($"  head: {ShortenId(status.Head)}");
            Console.WriteLine();
            Console.WriteLine("Git:");
            Console.WriteLine($"  branch: {(string.IsNullOrWhiteSpace(status.GitContext.Branch) ? "(detached)" : status.GitContext.Branch)}");
            Console.WriteLine($"  commit: {ShortenId(status.GitContext.Commit)}");
            Console.WriteLine($"  dirty: {status.GitContext.Dirty.ToString().ToLowerInvariant()}");
            Console.WriteLine();
            Console.WriteLine("Model:");
            Console.WriteLine($"  current model: {GetCurrentModelLabel(modelReadResult)}");
            Console.WriteLine($"  source: {GetModelSourceLabel(modelReadResult)}");
            Console.WriteLine();
            Console.WriteLine("Session:");
            Console.WriteLine($"  last mode: {SessionState.LastMode}");
            Console.WriteLine($"  last context: {SessionState.LastContextKind}");
            Console.WriteLine($"  last trace: {(SessionState.LastTrace is null ? "unavailable" : "available")}");
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
    }

    private static void RenderLog(string repoRoot)
    {
        var logResult = RckWorkspaceLogReader.Read(repoRoot);
        if (!logResult.Success)
        {
            Console.Error.WriteLine(logResult.ErrorMessage);
            return;
        }

        var entries = logResult.Entries.Take(10).ToArray();
        if (entries.Length == 0)
        {
            Console.WriteLine("No interactions yet.");
            return;
        }

        Console.WriteLine("Recent interactions:");
        foreach (var entry in entries)
        {
            Console.WriteLine($"- {entry.StateShortId} {entry.Mode} {entry.CreatedAtUtc:yyyy-MM-dd HH:mm:ss}Z");
            Console.WriteLine($"  prompt: {TruncateInline(entry.Prompt)}");
            Console.WriteLine($"  answer: {TruncateInline(entry.AnswerSummary)}");
            Console.WriteLine($"  delta: {ShortenId(entry.DeltaShortId)}");
            if (entry.Anchors.Count > 0)
            {
                Console.WriteLine($"  anchors: {entry.Anchors.Count}");
            }
        }
    }

    private static void RenderContext()
    {
        if (SessionState.LastContextKind == "simple" && SessionState.LastSimpleContext is not null)
        {
            var simple = SessionState.LastSimpleContext;
            Console.WriteLine("Last Simple Context:");
            Console.WriteLine($"  recent interactions: {simple.RecentInteractions}");
            Console.WriteLine($"  anchors: {simple.Anchors}");
            Console.WriteLine($"  artifacts: {simple.Artifacts}");
            Console.WriteLine($"  estimated chars: {simple.EstimatedChars}");
            Console.WriteLine($"  estimated tokens: {simple.EstimatedTokens}");
            Console.WriteLine($"  model budget: {simple.ModelBudget}");
            Console.WriteLine($"  context usage: {simple.ContextUsage}");
            Console.WriteLine($"  transport size: {simple.TransportSizeChars} chars");
            Console.WriteLine($"  transport risk: {simple.TransportRisk}");
            Console.WriteLine($"  truncated: {simple.Truncated.ToString().ToLowerInvariant()}");
            return;
        }

        if (SessionState.LastContextKind == "complete" && SessionState.LastCompleteContext is not null)
        {
            var complete = SessionState.LastCompleteContext;
            Console.WriteLine("Last Complete Context:");
            Console.WriteLine($"  selection strategy: {complete.SelectionStrategy ?? "(unknown)"}");
            Console.WriteLine($"  validation status: {complete.ValidationStatus ?? "(unknown)"}");
            Console.WriteLine($"  states: {complete.SelectedStateCount}");
            Console.WriteLine($"  deltas: {complete.SelectedDeltaCount}");
            Console.WriteLine($"  anchors: {complete.SelectedAnchorCount}");
            Console.WriteLine($"  contextPack scope: {complete.ContextPackScope ?? "(unknown)"}");
            Console.WriteLine($"  estimated chars: {complete.EstimatedChars}");
            Console.WriteLine($"  estimated tokens: {complete.EstimatedTokens}");
            Console.WriteLine($"  transport risk: {complete.TransportRisk}");
            Console.WriteLine($"  truncated: {complete.Truncated.ToString().ToLowerInvariant()}");
            return;
        }

        Console.WriteLine("No context has been built in this session yet.");
    }

    private static void RenderTrace()
    {
        if (SessionState.LastTrace is null)
        {
            Console.WriteLine("No TraceSlice has been built in this session yet.");
            return;
        }

        var trace = SessionState.LastTrace;
        Console.WriteLine("Last TraceSlice / validation summary:");
        Console.WriteLine($"  selection strategy: {trace.SelectionStrategy ?? "(unknown)"}");
        Console.WriteLine($"  validation status: {trace.ValidationStatus ?? "(unknown)"}");
        Console.WriteLine($"  selected states: {trace.SelectedStateCount}");
        Console.WriteLine($"  selected deltas: {trace.SelectedDeltaCount}");
        Console.WriteLine($"  selected anchors: {trace.SelectedAnchorCount}");
        if (trace.Warnings.Count > 0)
        {
            Console.WriteLine("  warnings:");
            foreach (var warning in trace.Warnings)
            {
                Console.WriteLine($"    - {warning}");
            }
        }

        if (trace.Omissions.Count > 0)
        {
            Console.WriteLine("  omissions:");
            foreach (var omission in trace.Omissions)
            {
                Console.WriteLine($"    - {omission}");
            }
        }
    }

    private static bool TryHandleTopLevelCommand(string input, string repoRoot)
    {
        if (string.Equals(input, "/model", StringComparison.Ordinal))
        {
            RenderModel(repoRoot);
            return true;
        }

        if (input.StartsWith("/model ", StringComparison.Ordinal))
        {
            return HandleModelSetCommand(input, repoRoot);
        }

        if (string.Equals(input, "/log", StringComparison.Ordinal))
        {
            RenderLog(repoRoot);
            return true;
        }

        if (string.Equals(input, "/context", StringComparison.Ordinal))
        {
            RenderContext();
            return true;
        }

        if (string.Equals(input, "/trace", StringComparison.Ordinal))
        {
            RenderTrace();
            return true;
        }

        if (!TryParseAnchorCommand(input, out var anchorLabel))
        {
            return false;
        }

        if (anchorLabel is null)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  /anchor \"milestone-name\"");
            return true;
        }

        var result = RckWorkspaceAnchorWriter.CreateExplicitAnchor(anchorLabel, repoRoot);
        foreach (var line in result.FormatConsoleLines())
        {
            Console.WriteLine(line);
        }

        return true;
    }

    private static bool TryParseAnchorCommand(string input, out string? anchorLabel)
    {
        anchorLabel = null;
        const string command = "/anchor";

        if (!input.StartsWith(command, StringComparison.Ordinal))
        {
            return false;
        }

        if (input.Length > command.Length && !char.IsWhiteSpace(input[command.Length]))
        {
            return false;
        }

        var remainder = input[command.Length..].Trim();
        if (remainder.Length == 0)
        {
            return true;
        }

        if (remainder.StartsWith('"') && remainder.EndsWith('"'))
        {
            remainder = remainder[1..^1].Trim();
        }

        anchorLabel = remainder.Length == 0 || remainder.Contains('\n') || remainder.Contains('\r')
            ? null
            : remainder;
        return true;
    }

    private static void RenderHelp()
    {
        Console.WriteLine("RFS TUI");
        Console.WriteLine();
        Console.WriteLine("Prompts:");
        Console.WriteLine("  Write a prompt directly, then choose:");
        Console.WriteLine("    1 Direct");
        Console.WriteLine("    2 Simple");
        Console.WriteLine("    3 Complete");
        Console.WriteLine("    4 Plan");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  /status");
        Console.WriteLine("  /log");
        Console.WriteLine("  /model");
        Console.WriteLine("  /model <model>");
        Console.WriteLine("  /context");
        Console.WriteLine("  /trace");
        Console.WriteLine("  /anchor \"name\"");
        Console.WriteLine("  /help");
        Console.WriteLine("  /exit");
    }

    private static bool HandleModelSetCommand(string input, string repoRoot)
    {
        var model = input["/model".Length..].Trim();
        if (model.Length == 0)
        {
            RenderModel(repoRoot);
            return true;
        }

        if (model.StartsWith('"') && model.EndsWith('"'))
        {
            model = model[1..^1].Trim();
        }

        if (model.Length == 0 || model.Contains('\n') || model.Contains('\r'))
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  /model <model>");
            return true;
        }

        var setResult = RckWorkspaceModelConfigStore.SetDefaultModel(model, repoRoot);
        if (!setResult.Success)
        {
            Console.Error.WriteLine(setResult.ErrorMessage);
            return true;
        }

        Console.WriteLine("Model updated:");
        Console.WriteLine($"  {setResult.DefaultModel ?? model}");
        return true;
    }

    private static void RenderModel(string repoRoot)
    {
        var readResult = RckWorkspaceModelConfigStore.Read(repoRoot);
        if (!readResult.Success)
        {
            Console.Error.WriteLine(readResult.ErrorMessage);
            return;
        }

        Console.WriteLine("Current model:");
        Console.WriteLine($"  {GetCurrentModelLabel(readResult)}");
        Console.WriteLine();
        Console.WriteLine("Source:");
        Console.WriteLine($"  {GetModelSourceLabel(readResult)}");
    }

    private static string GetCurrentModelLabel(RckWorkspaceModelConfigReadResult readResult)
    {
        if (readResult.HasConfiguredDefaultModel)
        {
            return readResult.DefaultModel!.Trim();
        }

        return "(inherited)";
    }

    private static string GetModelSourceLabel(RckWorkspaceModelConfigReadResult readResult)
    {
        if (readResult.HasConfiguredDefaultModel)
        {
            return "workspace";
        }

        return readResult.ConfigExists ? "inherited" : "default";
    }

    private static string ShortenId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(unknown)";
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 8 ? trimmed : trimmed[..8];
    }

    private static string TruncateInline(string? value, int maxLength = 72)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(none)";
        }

        var singleLine = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        if (singleLine.Length <= maxLength)
        {
            return singleLine;
        }

        return singleLine[..Math.Max(0, maxLength - 1)] + "…";
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
