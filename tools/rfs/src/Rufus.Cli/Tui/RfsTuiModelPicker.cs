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

        for (var index = 0; index < _models.Count; index++)
        {
            if (string.Equals(_models[index].Id, currentSessionModel, StringComparison.OrdinalIgnoreCase))
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
    string? ErrorMessage,
    IReadOnlyList<PiRpcAvailableModel> Models);

internal static class RfsTuiModelPicker
{
    private const string CancelledMessage = "Model selection cancelled.";

    internal static async Task<RfsTuiModelSelectionResult> SelectInteractiveAsync(
        string repoRoot,
        string currentSessionModel,
        CancellationToken cancellationToken = default)
    {
        var loadResult = await LoadAvailableModelsAsync(repoRoot, cancellationToken).ConfigureAwait(false);
        if (!loadResult.Success)
        {
            return new RfsTuiModelSelectionResult(false, false, null, loadResult.ErrorMessage, loadResult.Models);
        }

        var models = OrderModels(loadResult.Models);
        if (models.Count == 0)
        {
            return new RfsTuiModelSelectionResult(false, false, null, "Pi RPC did not return any available models.", models);
        }

        if (!RfsTuiTerminal.UseLivePalette)
        {
            return new RfsTuiModelSelectionResult(false, false, null, "Model picker requires an interactive terminal.", models);
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
                    return new RfsTuiModelSelectionResult(true, false, selectionState.SelectedModelId, null, models);
                }
                case RfsTuiModelSelectionAction.Cancel:
                {
                    ClearRenderedBlock(renderedLineCount);
                    Console.WriteLine();
                    return new RfsTuiModelSelectionResult(true, true, null, CancelledMessage, models);
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
        CancellationToken cancellationToken = default)
    {
        var trimmedRequestedModel = string.IsNullOrWhiteSpace(requestedModel)
            ? string.Empty
            : requestedModel.Trim();

        if (trimmedRequestedModel.Length == 0)
        {
            return new RfsTuiModelSelectionResult(false, false, null, "Missing model name.", Array.Empty<PiRpcAvailableModel>());
        }

        var loadResult = await LoadAvailableModelsAsync(repoRoot, cancellationToken).ConfigureAwait(false);
        if (!loadResult.Success)
        {
            return new RfsTuiModelSelectionResult(false, false, null, loadResult.ErrorMessage, loadResult.Models);
        }

        var models = OrderModels(loadResult.Models);
        var match = models.FirstOrDefault(model => string.Equals(model.Id, trimmedRequestedModel, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return new RfsTuiModelSelectionResult(false, false, null, $"Model not found: {trimmedRequestedModel}", models);
        }

        return new RfsTuiModelSelectionResult(true, false, match.Id, null, models);
    }

    internal static async Task<RfsTuiModelSelectionResult> LoadModelsAsync(
        string repoRoot,
        CancellationToken cancellationToken = default)
    {
        var loadResult = await LoadAvailableModelsAsync(repoRoot, cancellationToken).ConfigureAwait(false);
        if (!loadResult.Success)
        {
            return new RfsTuiModelSelectionResult(false, false, null, loadResult.ErrorMessage, loadResult.Models);
        }

        var models = OrderModels(loadResult.Models);
        return new RfsTuiModelSelectionResult(true, false, null, null, models);
    }

    internal static IReadOnlyList<PiRpcAvailableModel> OrderModels(IReadOnlyList<PiRpcAvailableModel> models)
        => models
            .OrderBy(model => model.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static Task<PiRpcModelListResult> LoadAvailableModelsAsync(
        string repoRoot,
        CancellationToken cancellationToken = default)
        => PiRpcClient.GetAvailableModelsAsync(repoRoot, cancellationToken);

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
