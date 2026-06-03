using Rufus.Cli.PiIntegration;

namespace Rufus.Cli.Tui;

internal enum RfsTuiModelSelectionAction
{
    Continue = 0,
    Confirm = 1,
    Cancel = 2,
}

internal sealed class RfsTuiModelSelectionState
{
    private readonly IReadOnlyList<PiRpcAvailableModel> _models;

    public RfsTuiModelSelectionState(IReadOnlyList<PiRpcAvailableModel> models, string currentSessionModel)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        CurrentSessionModel = string.IsNullOrWhiteSpace(currentSessionModel)
            ? RfsTuiSessionState.DefaultSessionModel
            : currentSessionModel.Trim();
        SelectedIndex = FindInitialIndex(CurrentSessionModel);
    }

    public string CurrentSessionModel { get; }

    public int SelectedIndex { get; private set; }

    public string? SelectedModelId => _models.Count == 0 ? null : _models[SelectedIndex].Id;

    public string? SelectedProvider => _models.Count == 0 ? null : _models[SelectedIndex].Provider;

    public string? SelectedQualifiedModel
        => string.IsNullOrWhiteSpace(SelectedModelId)
            ? null
            : string.IsNullOrWhiteSpace(SelectedProvider)
                ? SelectedModelId
                : $"{SelectedProvider}/{SelectedModelId}";

    public RfsTuiModelSelectionAction HandleKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            return RfsTuiModelSelectionAction.Cancel;
        }

        if (key.KeyChar is 'q' or 'Q')
        {
            return RfsTuiModelSelectionAction.Cancel;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            return RfsTuiModelSelectionAction.Confirm;
        }

        if (key.Key == ConsoleKey.UpArrow)
        {
            MoveUp();
            return RfsTuiModelSelectionAction.Continue;
        }

        if (key.Key == ConsoleKey.DownArrow)
        {
            MoveDown();
            return RfsTuiModelSelectionAction.Continue;
        }

        return RfsTuiModelSelectionAction.Continue;
    }

    private int FindInitialIndex(string currentSessionModel)
    {
        if (_models.Count == 0)
        {
            return 0;
        }

        var currentModelId = RfsTuiModelPicker.ExtractModelId(currentSessionModel);
        for (var index = 0; index < _models.Count; index++)
        {
            if (string.Equals(_models[index].Id, currentModelId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    private void MoveUp()
    {
        if (SelectedIndex > 0)
        {
            SelectedIndex--;
        }
    }

    private void MoveDown()
    {
        if (SelectedIndex < _models.Count - 1)
        {
            SelectedIndex++;
        }
    }
}

internal sealed record RfsTuiModelSelectionResult(
    bool Success,
    bool Cancelled,
    string? SelectedModel,
    string? SelectedProvider,
    string? ErrorMessage,
    IReadOnlyList<PiRpcAvailableModel> Models)
{
    public string? SelectedQualifiedModel
        => string.IsNullOrWhiteSpace(SelectedModel)
            ? null
            : string.IsNullOrWhiteSpace(SelectedProvider)
                ? SelectedModel
                : $"{SelectedProvider}/{SelectedModel}";
}

internal sealed record RfsTuiResolvedModel(string Provider, string Model)
{
    public string QualifiedModel => $"{Provider}/{Model}";
}

internal static class RfsTuiModelPicker
{
    private const string CancelledMessage = "Model selection cancelled.";
    private static readonly string[] ProviderPreference =
    [
        "github-copilot",
        "openai-codex",
        "openai",
        "deepseek",
        "opencode",
        "openrouter",
        "vercel-ai-gateway",
        "azure-openai-responses",
    ];

    internal static async Task<RfsTuiModelSelectionResult> SelectInteractiveAsync(
        string repoRoot,
        string currentSessionModel,
        CancellationToken cancellationToken = default)
    {
        var loadResult = await LoadAvailableModelsAsync(repoRoot, cancellationToken).ConfigureAwait(false);
        if (!loadResult.Success)
        {
            return new RfsTuiModelSelectionResult(false, false, null, null, loadResult.ErrorMessage, loadResult.Models);
        }

        var models = OrderModels(loadResult.Models);
        if (models.Count == 0)
        {
            return new RfsTuiModelSelectionResult(false, false, null, null, "Pi RPC did not return any available models.", models);
        }

        if (!RfsTuiTerminal.UseLivePalette)
        {
            return new RfsTuiModelSelectionResult(false, false, null, null, "Model picker requires an interactive terminal.", models);
        }

        var selectionState = new RfsTuiModelSelectionState(models, currentSessionModel);
        var renderedLineCount = 0;

        void Redraw()
        {
            if (renderedLineCount > 0)
            {
                ClearRenderedBlock(renderedLineCount);
            }

            renderedLineCount = RfsTuiRenderer.WriteModelPickerScreen(models, selectionState, currentSessionModel);
        }

        Redraw();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            var action = selectionState.HandleKey(key);
            switch (action)
            {
                case RfsTuiModelSelectionAction.Continue:
                    Redraw();
                    break;
                case RfsTuiModelSelectionAction.Confirm:
                {
                    ClearRenderedBlock(renderedLineCount);
                    Console.WriteLine();
                    return new RfsTuiModelSelectionResult(true, false, selectionState.SelectedModelId, selectionState.SelectedProvider, null, models);
                }
                case RfsTuiModelSelectionAction.Cancel:
                {
                    ClearRenderedBlock(renderedLineCount);
                    Console.WriteLine();
                    return new RfsTuiModelSelectionResult(true, true, null, null, CancelledMessage, models);
                }
                default:
                    Redraw();
                    break;
            }
        }
    }

    internal static async Task<RfsTuiModelSelectionResult> ResolveRequestedModelAsync(
        string repoRoot,
        string requestedModel,
        string? preferredProvider = null,
        CancellationToken cancellationToken = default)
    {
        var loadResult = await LoadAvailableModelsAsync(repoRoot, cancellationToken).ConfigureAwait(false);
        if (!loadResult.Success)
        {
            return new RfsTuiModelSelectionResult(false, false, null, null, loadResult.ErrorMessage, loadResult.Models);
        }

        var models = OrderModels(loadResult.Models);
        var resolved = ResolveModel(models, requestedModel, preferredProvider);
        if (resolved is null)
        {
            var trimmedRequestedModel = string.IsNullOrWhiteSpace(requestedModel)
                ? string.Empty
                : requestedModel.Trim();
            return new RfsTuiModelSelectionResult(false, false, null, null, $"Model not found: {trimmedRequestedModel}", models);
        }

        return new RfsTuiModelSelectionResult(true, false, resolved.Model, resolved.Provider, null, models);
    }

    internal static async Task<string> ResolveExecutionModelAsync(
        string repoRoot,
        string requestedModel,
        string? preferredProvider = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveRequestedModelAsync(repoRoot, requestedModel, preferredProvider, cancellationToken).ConfigureAwait(false);
        if (resolved.Success && !string.IsNullOrWhiteSpace(resolved.SelectedQualifiedModel))
        {
            return resolved.SelectedQualifiedModel;
        }

        var trimmedRequestedModel = string.IsNullOrWhiteSpace(requestedModel)
            ? string.Empty
            : requestedModel.Trim();
        if (!string.IsNullOrWhiteSpace(preferredProvider) && !IsQualifiedModel(trimmedRequestedModel))
        {
            return $"{preferredProvider.Trim()}/{StripThinkingSuffix(trimmedRequestedModel)}";
        }

        return NormalizeExecutionModelString(trimmedRequestedModel);
    }

    internal static RfsTuiResolvedModel? ResolveModel(
        IReadOnlyList<PiRpcAvailableModel> models,
        string requestedModel,
        string? preferredProvider = null)
    {
        if (models.Count == 0)
        {
            return null;
        }

        var trimmedRequestedModel = string.IsNullOrWhiteSpace(requestedModel)
            ? string.Empty
            : requestedModel.Trim();
        if (trimmedRequestedModel.Length == 0)
        {
            return null;
        }

        if (TryParseQualifiedModel(trimmedRequestedModel, out var explicitProvider, out var explicitModelId))
        {
            return models.FirstOrDefault(model =>
                string.Equals(model.Provider, explicitProvider, StringComparison.OrdinalIgnoreCase)
                && string.Equals(model.Id, explicitModelId, StringComparison.OrdinalIgnoreCase)) is { } explicitMatch
                ? new RfsTuiResolvedModel(explicitMatch.Provider, explicitMatch.Id)
                : null;
        }

        var baseModel = StripThinkingSuffix(trimmedRequestedModel);
        var matches = models.Where(model => string.Equals(model.Id, baseModel, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length == 0)
        {
            return null;
        }

        var chosenProvider = ChoosePreferredProvider(matches, preferredProvider);
        if (chosenProvider is null)
        {
            return null;
        }

        var chosenModel = matches.First(model => string.Equals(model.Provider, chosenProvider, StringComparison.OrdinalIgnoreCase));
        return new RfsTuiResolvedModel(chosenModel.Provider, chosenModel.Id);
    }

    internal static string ExtractModelId(string model)
    {
        var trimmed = string.IsNullOrWhiteSpace(model)
            ? string.Empty
            : model.Trim();

        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (TryParseQualifiedModel(trimmed, out _, out var qualifiedModelId))
        {
            return qualifiedModelId;
        }

        return StripThinkingSuffix(trimmed);
    }

    internal static bool IsQualifiedModel(string model)
        => TryParseQualifiedModel(model, out _, out _);

    internal static IReadOnlyList<PiRpcAvailableModel> OrderModels(IReadOnlyList<PiRpcAvailableModel> models)
        => models
            .OrderBy(model => model.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static async Task<PiRpcModelListResult> LoadAvailableModelsAsync(
        string repoRoot,
        CancellationToken cancellationToken = default)
        => await PiRpcClient.GetAvailableModelsAsync(repoRoot, cancellationToken).ConfigureAwait(false);

    private static string NormalizeExecutionModelString(string requestedModel)
    {
        var trimmed = string.IsNullOrWhiteSpace(requestedModel)
            ? string.Empty
            : requestedModel.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        if (TryParseQualifiedModel(trimmed, out var provider, out var modelId))
        {
            return $"{provider}/{modelId}";
        }

        return trimmed;
    }

    private static bool TryParseQualifiedModel(string value, out string provider, out string modelId)
    {
        provider = string.Empty;
        modelId = string.Empty;

        var trimmed = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var slashIndex = trimmed.IndexOf('/');
        if (slashIndex <= 0 || slashIndex >= trimmed.Length - 1)
        {
            return false;
        }

        provider = trimmed[..slashIndex].Trim();
        modelId = trimmed[(slashIndex + 1)..].Trim();
        return provider.Length > 0 && modelId.Length > 0;
    }

    private static string StripThinkingSuffix(string value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        var colonIndex = trimmed.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex >= trimmed.Length - 1)
        {
            return trimmed;
        }

        if (trimmed.Contains('/'))
        {
            return trimmed;
        }

        return trimmed[..colonIndex].Trim();
    }

    private static string? ChoosePreferredProvider(IReadOnlyList<PiRpcAvailableModel> matches, string? preferredProvider)
    {
        if (matches.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(preferredProvider))
        {
            var preferredMatch = matches.FirstOrDefault(model => string.Equals(model.Provider, preferredProvider.Trim(), StringComparison.OrdinalIgnoreCase));
            if (preferredMatch is not null && !string.IsNullOrWhiteSpace(preferredMatch.Provider))
            {
                return preferredMatch.Provider;
            }
        }

        foreach (var provider in ProviderPreference)
        {
            var match = matches.FirstOrDefault(model => string.Equals(model.Provider, provider, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !string.IsNullOrWhiteSpace(match.Provider))
            {
                return match.Provider;
            }
        }

        return matches
            .OrderBy(model => model.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .First()
            .Provider;
    }

    private static void ClearRenderedBlock(int lineCount)
    {
        if (!RfsTuiTerminal.UseCursorControl)
        {
            return;
        }

        const string MoveCursorUpOneLine = "\u001b[1F";
        const string ClearLine = "\u001b[2K";

        for (var i = 0; i < lineCount; i++)
        {
            Console.Write(MoveCursorUpOneLine);
            Console.Write(ClearLine);
        }
    }
}
