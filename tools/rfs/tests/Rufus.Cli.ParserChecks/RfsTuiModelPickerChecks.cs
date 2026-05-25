using Rufus.Cli.PiIntegration;
using Rufus.Cli.Tui;
using Rufus.RCK.Workspace;

internal static class RfsTuiModelPickerChecks
{
    internal static void Run(List<string> failures)
    {
        RunSessionModelStateCases(failures);
        RunModelSelectionStateCases(failures);
        RunRendererAndPrincipalModelCases(failures);
        RunCommandCatalogCases(failures);
    }

    private static void RunSessionModelStateCases(List<string> failures)
    {
        var sessionState = new RfsTuiSessionState();
        if (!string.Equals(sessionState.CurrentSessionModel, RfsTuiSessionState.DefaultSessionModel, StringComparison.Ordinal))
        {
            failures.Add($"[tui model picker] expected a fresh session to start on '{RfsTuiSessionState.DefaultSessionModel}' but got '{sessionState.CurrentSessionModel}'.");
        }

        sessionState.SetSessionModel("claude-sonnet-4.5");
        if (!string.Equals(sessionState.CurrentSessionModel, "claude-sonnet-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[tui model picker] expected session model to update to 'claude-sonnet-4.5' but got '{sessionState.CurrentSessionModel}'.");
        }

        sessionState.ResetSessionModel();
        if (!string.Equals(sessionState.CurrentSessionModel, RfsTuiSessionState.DefaultSessionModel, StringComparison.Ordinal))
        {
            failures.Add($"[tui model picker] expected session reset to restore '{RfsTuiSessionState.DefaultSessionModel}' but got '{sessionState.CurrentSessionModel}'.");
        }
    }

    private static void RunModelSelectionStateCases(List<string> failures)
    {
        var models = new[]
        {
            new PiRpcAvailableModel("claude-haiku-4.5", "github-copilot", "Claude Haiku 4.5"),
            new PiRpcAvailableModel("claude-sonnet-4.5", "github-copilot", "Claude Sonnet 4.5"),
            new PiRpcAvailableModel("gpt-5.4-mini", "github-copilot", "GPT-5.4 Mini"),
            new PiRpcAvailableModel("gpt-5.4", "github-copilot", "GPT-5.4"),
        };

        var state = new RfsTuiModelSelectionState(models, "gpt-5.4-mini");
        if (state.SelectedIndex != 2)
        {
            failures.Add($"[tui model picker] expected the current session model to be selected initially, but got index {state.SelectedIndex}.");
        }

        var moveDownResult = state.HandleKey(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false));
        if (moveDownResult != RfsTuiModelSelectionAction.Continue || state.SelectedIndex != 3)
        {
            failures.Add($"[tui model picker] expected ArrowDown to move to index 3 and continue, but got action {moveDownResult} and index {state.SelectedIndex}.");
        }

        var moveUpResult = state.HandleKey(new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false));
        if (moveUpResult != RfsTuiModelSelectionAction.Continue || state.SelectedIndex != 2)
        {
            failures.Add($"[tui model picker] expected ArrowUp to move back to index 2 and continue, but got action {moveUpResult} and index {state.SelectedIndex}.");
        }

        var enterResult = state.HandleKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
        if (enterResult != RfsTuiModelSelectionAction.Confirm || !string.Equals(state.SelectedModelId, "gpt-5.4-mini", StringComparison.Ordinal))
        {
            failures.Add($"[tui model picker] expected Enter to confirm 'gpt-5.4-mini' but got action {enterResult} and selected '{state.SelectedModelId ?? "(null)"}'.");
        }

        var escapeState = new RfsTuiModelSelectionState(models, "claude-haiku-4.5");
        var escapeResult = escapeState.HandleKey(new ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false));
        if (escapeResult != RfsTuiModelSelectionAction.Cancel)
        {
            failures.Add($"[tui model picker] expected Escape to cancel but got {escapeResult}.");
        }

        var qState = new RfsTuiModelSelectionState(models, "claude-haiku-4.5");
        var qResult = qState.HandleKey(new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false));
        if (qResult != RfsTuiModelSelectionAction.Cancel)
        {
            failures.Add($"[tui model picker] expected q to cancel but got {qResult}.");
        }

        var missingCurrentState = new RfsTuiModelSelectionState(models, "does-not-exist");
        if (missingCurrentState.SelectedIndex != 0)
        {
            failures.Add($"[tui model picker] expected missing current model to fall back to the first entry, but got index {missingCurrentState.SelectedIndex}.");
        }
    }

    private static void RunCommandCatalogCases(List<string> failures)
    {
        var helpCommands = RfsTuiCommandCatalog.GetHelpCommands();
        var modelShow = helpCommands.FirstOrDefault(command => command.Kind == RfsTuiCommandKind.ModelShow);
        var modelSet = helpCommands.FirstOrDefault(command => command.Kind == RfsTuiCommandKind.ModelSet);

        if (modelShow is null || !string.Equals(modelShow.Description, "Open session model picker", StringComparison.Ordinal))
        {
            failures.Add("[tui model picker] expected /model help text to describe the session picker.");
        }

        if (modelSet is null || !string.Equals(modelSet.Description, "Set session model (temporary)", StringComparison.Ordinal))
        {
            failures.Add("[tui model picker] expected /model <model> help text to describe temporary session updates.");
        }

        var exactModel = RfsTuiCommandCatalog.FindExactMatch("/model");
        if (exactModel is null || exactModel.Kind != RfsTuiCommandKind.ModelShow)
        {
            failures.Add("[tui model picker] expected /model to resolve to the picker command.");
        }

        var modelSuggestions = RfsTuiCommandCatalog.GetSuggestions("/model");
        if (modelSuggestions.Count == 0)
        {
            failures.Add("[tui model picker] expected /model suggestions to be populated.");
        }
    }

    private static void RunRendererAndPrincipalModelCases(List<string> failures)
    {
        var status = new RckWorkspaceStatus(
            RepoRoot: "/tmp/rfs",
            WorkspaceExists: true,
            ConfigExists: true,
            RckExists: true,
            HeadExists: true,
            Head: "abcdef1234567890",
            StateCount: 3,
            DeltaCount: 2,
            AnchorCount: 1,
            GitContext: new GitWorkspaceContext("main", "abcdef1234567890", Dirty: false, Array.Empty<GitWorkspaceArtifactChange>()));

        var originalOut = Console.Out;
        using var stdout = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            RfsTuiRenderer.WriteHeader(status, "pi-mono", "claude-sonnet-4.5", leadingBlankLine: false);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var headerText = stdout.ToString();
        if (!headerText.Contains("claude-sonnet-4.5 · session", StringComparison.Ordinal))
        {
            failures.Add("[tui model picker] expected the header to mark non-default session models as session-scoped.");
        }

        if (!headerText.Contains("Model:", StringComparison.Ordinal))
        {
            failures.Add("[tui model picker] expected the header to include a Model line.");
        }

        var executionModel = RfsTuiSession.CreatePrincipalAnswerExecutionModel("claude-sonnet-4.5");
        if (!string.Equals(executionModel.Model, "claude-sonnet-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[tui model picker] expected the principal answer execution model to use the session model but got '{executionModel.Model}'.");
        }

        var defaultExecutionModel = RfsTuiSession.CreatePrincipalAnswerExecutionModel(string.Empty);
        if (!string.Equals(defaultExecutionModel.Model, RfsTuiSessionState.DefaultSessionModel, StringComparison.Ordinal))
        {
            failures.Add($"[tui model picker] expected empty session model to fall back to '{RfsTuiSessionState.DefaultSessionModel}' but got '{defaultExecutionModel.Model}'.");
        }
    }
}
