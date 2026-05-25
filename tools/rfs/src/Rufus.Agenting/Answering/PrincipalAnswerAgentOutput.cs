using System.Text.Json.Serialization;

namespace Rufus.Agenting.Answering;

/// <summary>
/// Future Complete-mode answer output contract.
/// The implementation should surface provider/model/transport without exposing raw JSONL or stdout/stderr.
/// </summary>
public sealed record PrincipalAnswerAgentOutput
{
    public string FinalAnswer { get; }

    public string? AnswerSummary { get; }

    public string? Provider { get; }

    public string? Model { get; }

    public string? Transport { get; }

    public int? EstimatedTokens { get; }

    public IReadOnlyList<string> Warnings { get; }

    public IReadOnlyList<string> Errors { get; }

    [JsonConstructor]
    public PrincipalAnswerAgentOutput(
        string FinalAnswer,
        string? AnswerSummary = null,
        string? Provider = null,
        string? Model = null,
        string? Transport = null,
        int? EstimatedTokens = null,
        IReadOnlyList<string>? Warnings = null,
        IReadOnlyList<string>? Errors = null)
    {
        this.FinalAnswer = Normalize(FinalAnswer, nameof(FinalAnswer));
        this.AnswerSummary = NormalizeOptional(AnswerSummary, nameof(AnswerSummary));
        this.Provider = NormalizeOptional(Provider, nameof(Provider));
        this.Model = NormalizeOptional(Model, nameof(Model));
        this.Transport = NormalizeOptional(Transport, nameof(Transport));
        this.EstimatedTokens = EstimatedTokens;
        this.Warnings = NormalizeValues(Warnings, nameof(Warnings));
        this.Errors = NormalizeValues(Errors, nameof(Errors));
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
