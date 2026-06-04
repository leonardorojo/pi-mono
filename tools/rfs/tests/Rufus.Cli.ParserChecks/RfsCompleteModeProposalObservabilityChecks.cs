using System.Text.Json;
using Rufus.Agenting;
using Rufus.Agenting.TraceSlice;
using Rufus.Cli.Intent;
using Rufus.Cli.TraceSlice;
using Rufus.Cli.PiIntegration;
using Rufus.Cli.Tui;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.ParserChecks;

internal static class RfsCompleteModeProposalObservabilityChecks
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task RunAsync(List<string> failures)
    {
        await RunModelPrintedBeforeLlmCallAsync(failures);
        await RunQuestionIntentStillCallsAnchorSelectionAsync(failures);
        await RunImpureAnchorSelectionOutputStillCompletesAsync(failures);
    }

    /// <summary>
    /// The anchor selection model diagnostic must appear in stage output
    /// BEFORE the LLM call, so timeouts don't hide which model was attempted.
    /// </summary>
    private static async Task RunModelPrintedBeforeLlmCallAsync(List<string> failures)
    {
        const string name = "complete mode prints proposal model before anchor selection llm";

        var originalOut = Console.Out;
        using var stdout = new StringWriter();
        try
        {
            Console.SetOut(stdout);

            var intentAnswerJson = "{\"Intent\":\"code-change\",\"Summary\":\"Implement reset board action.\",\"Entities\":[\"reset board\"],\"Constraints\":[]}";
            var intentTransport = new FakeIntentLlmTransport(success: true, answerJson: intentAnswerJson);
            var intentAgent = new PiIntentInferenceAgent("/home/rufus/DEV/leonardorojo/ChessBoardApp", transport: intentTransport);

            var anchorSelectionTransport = new FakeTraceSliceProposalLlmTransport(
                success: true,
                answerJson: BuildAnchorSelectionAnswer(
                    selectedAnchorIds: Array.Empty<string>(),
                    fallbackStrategy: "recent-chain",
                    rationale: new[] { ("recent-chain", "no relevant anchors") },
                    warnings: new[] { "no relevant anchors" },
                    confidence: 0.2,
                    schemaVersion: 1,
                    type: "rufus.anchor-selection"));

            // Explicit model override to make the diagnostic testable
            var proposalAgent = new PiTraceSliceProposalAgent(
                "/home/rufus/DEV/leonardorojo/ChessBoardApp",
                model: "deepseek-chat",
                transport: anchorSelectionTransport);

            var result = await RfsCompleteModePipeline.BuildAsync(
                "Implement reset board action",
                "/home/rufus/DEV/leonardorojo/ChessBoardApp",
                5,
                intentAgent,
                proposalAgent);

            if (!result.Success)
            {
                failures.Add($"[{name}] expected Success=true but got false. Error: {result.ErrorMessage}");
                return;
            }

            // The agent descriptor model must match the override
            if (!string.Equals(proposalAgent.Descriptor.ExecutionModel.Model, "deepseek-chat", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected agent descriptor model 'deepseek-chat' but got '{proposalAgent.Descriptor.ExecutionModel.Model}'.");
            }

            // Transport must have been called with the override model
            if (!string.Equals(anchorSelectionTransport.LastModel, "deepseek-chat", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected transport model 'deepseek-chat' but got '{anchorSelectionTransport.LastModel}'.");
            }

            // Transport must have been called exactly once
            if (anchorSelectionTransport.CallCount != 1)
            {
                failures.Add($"[{name}] expected anchor selection transport to be called once but got {anchorSelectionTransport.CallCount} calls.");
            }

            // The stage output must include the model diagnostic
            var completeConsole = stdout.ToString();
            if (!completeConsole.Contains("model: deepseek-chat", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected console output to include 'model: deepseek-chat' but it was missing.\nConsole: {completeConsole}");
            }

            if (!completeConsole.Contains("source: pi-trace-slice-proposal", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected console output to include 'source: pi-trace-slice-proposal'.\nConsole: {completeConsole}");
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    /// Question intent must still go through anchor selection (no short-circuit).
    /// The LLM transport must be called, and ProposalSource must be "pi-trace-slice-proposal".
    /// </summary>
    private static async Task RunQuestionIntentStillCallsAnchorSelectionAsync(List<string> failures)
    {
        const string name = "complete mode question intent still calls anchor selection llm";

        var originalOut = Console.Out;
        using var stdout = new StringWriter();
        try
        {
            Console.SetOut(stdout);

            var intentAnswerJson = "{\"Intent\":\"question\",\"Summary\":\"Ask factual question about Japan capital.\",\"Entities\":[],\"Constraints\":[]}";
            var intentTransport = new FakeIntentLlmTransport(success: true, answerJson: intentAnswerJson);
            var intentAgent = new PiIntentInferenceAgent("/home/rufus/DEV/leonardorojo/ChessBoardApp", transport: intentTransport);

            var anchorSelectionTransport = new FakeTraceSliceProposalLlmTransport(
                success: true,
                answerJson: BuildAnchorSelectionAnswer(
                    selectedAnchorIds: Array.Empty<string>(),
                    fallbackStrategy: "recent-chain",
                    rationale: new[] { ("recent-chain", "no relevant anchors") },
                    warnings: Array.Empty<string>(),
                    confidence: 0.85,
                    schemaVersion: 1,
                    type: "rufus.anchor-selection"));

            var proposalAgent = new PiTraceSliceProposalAgent(
                "/home/rufus/DEV/leonardorojo/ChessBoardApp",
                transport: anchorSelectionTransport);

            var result = await RfsCompleteModePipeline.BuildAsync(
                "What is the capital of Japan?",
                "/home/rufus/DEV/leonardorojo/ChessBoardApp",
                5,
                intentAgent,
                proposalAgent);

            // Anchor selection transport MUST have been called (no short-circuit)
            if (anchorSelectionTransport.CallCount != 1)
            {
                failures.Add($"[{name}] expected anchor selection transport to be called once but got {anchorSelectionTransport.CallCount} calls.");
            }

            if (!result.Success)
            {
                failures.Add($"[{name}] expected Success=true but got false. Error: {result.ErrorMessage}");
                return;
            }

            if (!string.Equals(result.IntentKind, "question", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected IntentKind 'question' but got '{result.IntentKind}'.");
            }

            // ProposalSource must be the normal agent id, NOT "question-short-circuit"
            if (!string.Equals(result.ProposalSource, "pi-trace-slice-proposal", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected ProposalSource 'pi-trace-slice-proposal' but got '{result.ProposalSource}'.");
            }

            // No selected anchors expected for factual questions
            if (result.SelectedAnchorIds.Count != 0)
            {
                failures.Add($"[{name}] expected 0 selected anchors but got {result.SelectedAnchorIds.Count}.");
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static async Task RunImpureAnchorSelectionOutputStillCompletesAsync(List<string> failures)
    {
        const string name = "complete mode accepts impure anchor-selection output";

        var originalOut = Console.Out;
        using var stdout = new StringWriter();
        try
        {
            Console.SetOut(stdout);

            var intentAnswerJson = "{\"Intent\":\"code-change\",\"Summary\":\"Implement reset board action.\",\"Entities\":[\"reset board\"],\"Constraints\":[]}";
            var intentTransport = new FakeIntentLlmTransport(success: true, answerJson: intentAnswerJson);
            var intentAgent = new PiIntentInferenceAgent("/home/rufus/DEV/leonardorojo/ChessBoardApp", transport: intentTransport);

            var impureAnchorSelectionAnswer = "Here is the anchor selection:\n" + BuildAnchorSelectionAnswer(
                selectedAnchorIds: Array.Empty<string>(),
                fallbackStrategy: "recent-chain",
                rationale: new[] { ("recent-chain", "no relevant anchors") },
                warnings: new[] { "no relevant anchors" },
                confidence: 0.2,
                schemaVersion: 1,
                type: "rufus.anchor-selection") + "\nHope this helps.";

            var anchorSelectionTransport = new FakeTraceSliceProposalLlmTransport(
                success: true,
                answerJson: impureAnchorSelectionAnswer);

            var proposalAgent = new PiTraceSliceProposalAgent(
                "/home/rufus/DEV/leonardorojo/ChessBoardApp",
                transport: anchorSelectionTransport);

            var result = await RfsCompleteModePipeline.BuildAsync(
                "Implement reset board action",
                "/home/rufus/DEV/leonardorojo/ChessBoardApp",
                5,
                intentAgent,
                proposalAgent);

            if (!result.Success)
            {
                failures.Add($"[{name}] expected Success=true but got false. Error: {result.ErrorMessage}");
                return;
            }

            if (anchorSelectionTransport.CallCount != 1)
            {
                failures.Add($"[{name}] expected anchor selection transport to be called once but got {anchorSelectionTransport.CallCount} calls.");
            }

            if (result.SelectedAnchorIds.Count != 0)
            {
                failures.Add($"[{name}] expected 0 selected anchors for recent-chain fallback but got {result.SelectedAnchorIds.Count}.");
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static string BuildAnchorSelectionAnswer(
        IReadOnlyList<string> selectedAnchorIds,
        string fallbackStrategy,
        IReadOnlyList<(string Target, string Reason)> rationale,
        IReadOnlyList<string> warnings,
        double confidence,
        int schemaVersion,
        string type)
    {
        var payload = new Dictionary<string, object?>
        {
            ["type"] = type,
            ["schemaVersion"] = schemaVersion,
            ["selectedAnchorIds"] = selectedAnchorIds,
            ["fallbackStrategy"] = fallbackStrategy,
            ["rationale"] = rationale.Select(item => new Dictionary<string, object?>
            {
                ["target"] = item.Target,
                ["reason"] = item.Reason,
            }).ToArray(),
            ["warnings"] = warnings,
            ["confidence"] = confidence,
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private sealed class FakeIntentLlmTransport : IIntentLlmTransport
    {
        private readonly bool _success;
        private readonly string _answerJson;

        public FakeIntentLlmTransport(bool success, string answerJson)
        {
            _success = success;
            _answerJson = answerJson;
        }

        public int CallCount { get; private set; }
        public string? LastModel { get; private set; }

        public Task<PiJsonAskResult> AskAsync(string workingDirectory, string prompt, string model, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastModel = model;

            if (!_success)
            {
                return Task.FromResult(new PiJsonAskResult(false, prompt, string.Empty, "fake intent transport failure", "pi", model));
            }

            return Task.FromResult(new PiJsonAskResult(true, prompt, _answerJson, null, "pi", model));
        }
    }

    private sealed class FakeTraceSliceProposalLlmTransport : ITraceSliceProposalLlmTransport
    {
        private readonly bool _success;
        private readonly string _answerJson;

        public FakeTraceSliceProposalLlmTransport(bool success, string answerJson)
        {
            _success = success;
            _answerJson = answerJson;
        }

        public int CallCount { get; private set; }
        public string? LastModel { get; private set; }

        public Task<PiJsonAskResult> AskAsync(string workingDirectory, string prompt, string model, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastModel = model;

            if (!_success)
            {
                return Task.FromResult(new PiJsonAskResult(false, prompt, string.Empty, "fake transport failure", "pi", model));
            }

            return Task.FromResult(new PiJsonAskResult(true, prompt, _answerJson, null, "pi", model));
        }
    }
}
