using System.Text.Json.Serialization;

namespace Rufus.Agenting.Answering;

/// <summary>
/// Future Complete-mode answer contract.
/// The caller resolves the workspace main model and maps this DTO into AgentTask/AgentTaskResult.
/// </summary>
public sealed record PrincipalAnswerAgentInput
{
    public string UserPrompt { get; }

    public string ValidatedContextPackJson { get; }

    public string ContextSummary { get; }

    public string ContextPackScope { get; }

    public IReadOnlyList<string> SelectedStateIds { get; }

    public IReadOnlyList<string> SelectedDeltaIds { get; }

    public IReadOnlyList<string> SelectedAnchorIds { get; }

    public int? EstimatedTokens { get; }

    public IReadOnlyList<string> Warnings { get; }

    public string? PipelineSummary { get; }

    [JsonConstructor]
    public PrincipalAnswerAgentInput(
        string UserPrompt,
        string ValidatedContextPackJson,
        string ContextSummary,
        string ContextPackScope,
        IReadOnlyList<string>? SelectedStateIds = null,
        IReadOnlyList<string>? SelectedDeltaIds = null,
        IReadOnlyList<string>? SelectedAnchorIds = null,
        int? EstimatedTokens = null,
        IReadOnlyList<string>? Warnings = null,
        string? PipelineSummary = null)
    {
        this.UserPrompt = Normalize(UserPrompt, nameof(UserPrompt));
        this.ValidatedContextPackJson = Normalize(ValidatedContextPackJson, nameof(ValidatedContextPackJson));
        this.ContextSummary = Normalize(ContextSummary, nameof(ContextSummary));
        this.ContextPackScope = Normalize(ContextPackScope, nameof(ContextPackScope));
        this.SelectedStateIds = NormalizeValues(SelectedStateIds, nameof(SelectedStateIds));
        this.SelectedDeltaIds = NormalizeValues(SelectedDeltaIds, nameof(SelectedDeltaIds));
        this.SelectedAnchorIds = NormalizeValues(SelectedAnchorIds, nameof(SelectedAnchorIds));
        this.EstimatedTokens = EstimatedTokens;
        this.Warnings = NormalizeValues(Warnings, nameof(Warnings));
        this.PipelineSummary = NormalizeOptional(PipelineSummary, nameof(PipelineSummary));
    }

    private static string Normalize(string value, string paramName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} cannot be empty.", paramName)
            : value;
    }

    private static string? NormalizeOptional(string? value, string paramName)
    {
        if (value is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} cannot be empty.", paramName)
            : value;
    }

    private static IReadOnlyList<string> NormalizeValues(IEnumerable<string>? values, string paramName)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values.Select((value, index) =>
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Value at index {index} cannot be empty.", paramName);
            }

            return value;
        }).ToArray();
    }
}
