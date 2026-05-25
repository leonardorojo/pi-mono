using System.Text.Json;
using Rufus.Agenting.Answering;

public static class PrincipalAnswerAgentContractChecks
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Run(List<string> failures)
    {
        RunTaskKindCase(failures);
        RunInputRoundTripCase(failures);
        RunOutputRoundTripCase(failures);
        RunEmptyCollectionsCase(failures);
    }

    private static void RunTaskKindCase(List<string> failures)
    {
        Expect(
            PrincipalAnswerAgentConstants.TaskKind == "complete-final-answer",
            $"principal answer task kind mismatch: '{PrincipalAnswerAgentConstants.TaskKind}'",
            failures);
    }

    private static void RunInputRoundTripCase(List<string> failures)
    {
        var input = new PrincipalAnswerAgentInput(
            "Implement reset board action",
            "{\"type\":\"rufus.context-pack\",\"schemaVersion\":1}",
            "Validated context pack with deterministic scope.",
            "complete::validated-context-pack",
            new[] { "state-1", "state-2" },
            new[] { "delta-1" },
            Array.Empty<string>(),
            1234,
            new[] { "transport risk is high" },
            "mode=complete; contextMode=validated");

        var json = JsonSerializer.Serialize(input, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Expect(root.TryGetProperty("userPrompt", out var userPrompt) && userPrompt.GetString() == "Implement reset board action", "principal answer input must serialize userPrompt", failures);
        Expect(root.TryGetProperty("validatedContextPackJson", out var contextPackJson) && contextPackJson.GetString() == "{\"type\":\"rufus.context-pack\",\"schemaVersion\":1}", "principal answer input must serialize validatedContextPackJson", failures);
        Expect(root.TryGetProperty("contextSummary", out var contextSummary) && contextSummary.GetString() == "Validated context pack with deterministic scope.", "principal answer input must serialize contextSummary", failures);
        Expect(root.TryGetProperty("contextPackScope", out var contextPackScope) && contextPackScope.GetString() == "complete::validated-context-pack", "principal answer input must serialize contextPackScope", failures);
        Expect(root.TryGetProperty("pipelineSummary", out var pipelineSummary) && pipelineSummary.GetString() == "mode=complete; contextMode=validated", "principal answer input must serialize pipelineSummary", failures);
        Expect(root.TryGetProperty("selectedStateIds", out var selectedStateIds) && selectedStateIds.ValueKind == JsonValueKind.Array && selectedStateIds.GetArrayLength() == 2, "principal answer input must serialize selectedStateIds", failures);
        Expect(root.TryGetProperty("selectedDeltaIds", out var selectedDeltaIds) && selectedDeltaIds.ValueKind == JsonValueKind.Array && selectedDeltaIds.GetArrayLength() == 1, "principal answer input must serialize selectedDeltaIds", failures);
        Expect(root.TryGetProperty("selectedAnchorIds", out var selectedAnchorIds) && selectedAnchorIds.ValueKind == JsonValueKind.Array && selectedAnchorIds.GetArrayLength() == 0, "principal answer input must serialize empty selectedAnchorIds", failures);
        Expect(root.TryGetProperty("estimatedTokens", out var estimatedTokens) && estimatedTokens.GetInt32() == 1234, "principal answer input must serialize estimatedTokens", failures);
        Expect(root.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array && warnings.GetArrayLength() == 1, "principal answer input must serialize warnings", failures);
        Expect(!root.TryGetProperty("rawJsonl", out _), "principal answer input must not include rawJsonl", failures);
        Expect(!root.TryGetProperty("stdout", out _), "principal answer input must not include stdout", failures);
        Expect(!root.TryGetProperty("stderr", out _), "principal answer input must not include stderr", failures);

        var roundTrip = JsonSerializer.Deserialize<PrincipalAnswerAgentInput>(json, JsonOptions);
        Expect(roundTrip is not null, "principal answer input must deserialize", failures);
        if (roundTrip is not null)
        {
            Expect(roundTrip.UserPrompt == input.UserPrompt, "principal answer input round-trip must preserve UserPrompt", failures);
            Expect(roundTrip.ValidatedContextPackJson == input.ValidatedContextPackJson, "principal answer input round-trip must preserve ValidatedContextPackJson", failures);
            Expect(roundTrip.ContextSummary == input.ContextSummary, "principal answer input round-trip must preserve ContextSummary", failures);
            Expect(roundTrip.ContextPackScope == input.ContextPackScope, "principal answer input round-trip must preserve ContextPackScope", failures);
            Expect(roundTrip.SelectedStateIds.SequenceEqual(input.SelectedStateIds), "principal answer input round-trip must preserve SelectedStateIds", failures);
            Expect(roundTrip.SelectedDeltaIds.SequenceEqual(input.SelectedDeltaIds), "principal answer input round-trip must preserve SelectedDeltaIds", failures);
            Expect(roundTrip.SelectedAnchorIds.SequenceEqual(input.SelectedAnchorIds), "principal answer input round-trip must preserve SelectedAnchorIds", failures);
            Expect(roundTrip.EstimatedTokens == input.EstimatedTokens, "principal answer input round-trip must preserve EstimatedTokens", failures);
            Expect(roundTrip.Warnings.SequenceEqual(input.Warnings), "principal answer input round-trip must preserve Warnings", failures);
            Expect(roundTrip.PipelineSummary == input.PipelineSummary, "principal answer input round-trip must preserve PipelineSummary", failures);
        }
    }

    private static void RunOutputRoundTripCase(List<string> failures)
    {
        var output = new PrincipalAnswerAgentOutput(
            "Reset board action should clear pieces and scores.",
            "Explains the reset board action.",
            null,
            null,
            "stdin",
            null,
            Array.Empty<string>(),
            Array.Empty<string>());

        var json = JsonSerializer.Serialize(output, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Expect(root.TryGetProperty("finalAnswer", out var finalAnswer) && finalAnswer.GetString() == output.FinalAnswer, "principal answer output must serialize finalAnswer", failures);
        Expect(root.TryGetProperty("answerSummary", out var answerSummary) && answerSummary.GetString() == output.AnswerSummary, "principal answer output must serialize answerSummary", failures);
        Expect(root.TryGetProperty("provider", out var provider) && provider.ValueKind == JsonValueKind.Null, "principal answer output must serialize provider as null", failures);
        Expect(root.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.Null, "principal answer output must serialize model as null", failures);
        Expect(root.TryGetProperty("transport", out var transport) && transport.GetString() == "stdin", "principal answer output must serialize transport", failures);
        Expect(root.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array && warnings.GetArrayLength() == 0, "principal answer output must allow empty warnings", failures);
        Expect(root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() == 0, "principal answer output must allow empty errors", failures);
        Expect(!root.TryGetProperty("rawJsonl", out _), "principal answer output must not include rawJsonl", failures);
        Expect(!root.TryGetProperty("stdout", out _), "principal answer output must not include stdout", failures);
        Expect(!root.TryGetProperty("stderr", out _), "principal answer output must not include stderr", failures);

        var roundTrip = JsonSerializer.Deserialize<PrincipalAnswerAgentOutput>(json, JsonOptions);
        Expect(roundTrip is not null, "principal answer output must deserialize", failures);
        if (roundTrip is not null)
        {
            Expect(roundTrip.FinalAnswer == output.FinalAnswer, "principal answer output round-trip must preserve FinalAnswer", failures);
            Expect(roundTrip.AnswerSummary == output.AnswerSummary, "principal answer output round-trip must preserve AnswerSummary", failures);
            Expect(roundTrip.Provider == output.Provider, "principal answer output round-trip must preserve Provider", failures);
            Expect(roundTrip.Model == output.Model, "principal answer output round-trip must preserve Model", failures);
            Expect(roundTrip.Transport == output.Transport, "principal answer output round-trip must preserve Transport", failures);
            Expect(roundTrip.EstimatedTokens == output.EstimatedTokens, "principal answer output round-trip must preserve EstimatedTokens", failures);
            Expect(roundTrip.Warnings.SequenceEqual(output.Warnings), "principal answer output round-trip must preserve Warnings", failures);
            Expect(roundTrip.Errors.SequenceEqual(output.Errors), "principal answer output round-trip must preserve Errors", failures);
        }
    }

    private static void RunEmptyCollectionsCase(List<string> failures)
    {
        var input = new PrincipalAnswerAgentInput(
            "Say hello",
            "{}",
            "Empty context pack for contract shape validation.",
            "complete::empty",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            null);

        var output = new PrincipalAnswerAgentOutput(
            "hello",
            null,
            "pi",
            "gpt-5.4-mini",
            null,
            null,
            Array.Empty<string>(),
            Array.Empty<string>());

        Expect(input.SelectedStateIds.Count == 0, "principal answer input must allow empty SelectedStateIds", failures);
        Expect(input.SelectedDeltaIds.Count == 0, "principal answer input must allow empty SelectedDeltaIds", failures);
        Expect(input.SelectedAnchorIds.Count == 0, "principal answer input must allow empty SelectedAnchorIds", failures);
        Expect(input.Warnings.Count == 0, "principal answer input must allow empty Warnings", failures);
        Expect(output.Warnings.Count == 0, "principal answer output must allow empty Warnings", failures);
        Expect(output.Errors.Count == 0, "principal answer output must allow empty Errors", failures);
    }

    private static void Expect(bool condition, string failure, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(failure);
        }
    }
}
