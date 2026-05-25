using System.Text.Json;
using Rufus.Agenting;
using Rufus.Agenting.Answering;
using Rufus.Cli.Answering;

public static class PiPrincipalAnswerAgentChecks
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync(List<string> failures)
    {
        await RunSuccessCaseAsync(failures);
        await RunKindGuardCaseAsync(failures);
        await RunTransportFailureCaseAsync(failures);
    }

    private static async Task RunSuccessCaseAsync(List<string> failures)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-principal-answer-agent-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var fakeTransport = new FakePrincipalAnswerLlmTransport(
            new PrincipalAnswerLlmResult(
                Success: true,
                Prompt: "ignored by fake transport",
                Answer: "The board reset should clear both pieces and derived scores.",
                ErrorMessage: null,
                Provider: "pi",
                Model: "gpt-5.4-mini",
                Transport: "argv",
                EstimatedTokens: 99,
                Warnings: Array.Empty<string>(),
                Errors: Array.Empty<string>()));

        var executionModel = new AgentExecutionModel("pi", "gpt-5.4-mini");
        var agent = new PiPrincipalAnswerAgent(tempRoot, executionModel, fakeTransport);
        var input = new PrincipalAnswerAgentInput(
            UserPrompt: "Implement reset board action",
            PromptToSend: "You are assisting inside an RFS repository session.\n[USER PROMPT]\nImplement reset board action",
            ValidatedContextPackJson: "{\"type\":\"rufus.context-pack\",\"schemaVersion\":1}",
            ContextSummary: "Validated context pack with deterministic scope.",
            ContextPackScope: "complete::validated-context-pack",
            SelectedStateIds: new[] { "state-1", "state-2" },
            SelectedDeltaIds: new[] { "delta-1" },
            SelectedAnchorIds: Array.Empty<string>(),
            EstimatedTokens: 1234,
            Warnings: new[] { "transport risk is high" },
            PipelineSummary: "mode=complete; validation=accepted");

        var task = new AgentTask(
            id: "task-1",
            kind: PrincipalAnswerAgentConstants.TaskKind,
            goal: "Produce the final answer from the validated ContextPack and user prompt.",
            input: JsonSerializer.Serialize(input, JsonOptions));

        var result = await agent.ExecuteAsync(task);
        Expect(result.Status == AgentTaskStatus.Succeeded, "principal answer agent should succeed with a fake transport", failures);
        Expect(result.AgentId == agent.Id, "principal answer agent should preserve agent id", failures);
        Expect(result.ExecutionModel.Provider == executionModel.Provider, "principal answer agent should preserve execution provider", failures);
        Expect(result.ExecutionModel.Model == executionModel.Model, "principal answer agent should preserve execution model", failures);
        Expect(string.IsNullOrWhiteSpace(result.Output) is false, "principal answer agent should emit JSON output", failures);
        Expect(Directory.Exists(Path.Combine(tempRoot, ".rfs")) is false, "principal answer agent should not write RCK workspace files", failures);
        Expect(fakeTransport.CapturedWorkingDirectory == tempRoot, "principal answer agent should pass the configured working directory to transport", failures);
        Expect(fakeTransport.CapturedPrompt == input.PromptToSend, "principal answer agent should pass PromptToSend to transport unchanged", failures);
        Expect(fakeTransport.CapturedWorkspaceModel == executionModel.Model, "principal answer agent should pass execution model to transport", failures);

        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            var output = JsonSerializer.Deserialize<PrincipalAnswerAgentOutput>(result.Output, JsonOptions);
            Expect(output is not null, "principal answer agent output must deserialize", failures);
            if (output is not null)
            {
                Expect(output.FinalAnswer == "The board reset should clear both pieces and derived scores.", "principal answer agent output must contain final answer", failures);
                Expect(output.Provider == "pi", "principal answer agent output must contain provider", failures);
                Expect(output.Model == "gpt-5.4-mini", "principal answer agent output must contain model", failures);
                Expect(output.Transport == "argv", "principal answer agent output must contain transport", failures);
                Expect(output.EstimatedTokens == 99, "principal answer agent output must contain estimated tokens when the transport provides them", failures);
            }
        }
    }

    private static async Task RunKindGuardCaseAsync(List<string> failures)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-principal-answer-agent-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var fakeTransport = new FakePrincipalAnswerLlmTransport(
            new PrincipalAnswerLlmResult(true, "ignored", "unused", null, "pi", "gpt-5.4-mini", "argv"));
        var agent = new PiPrincipalAnswerAgent(tempRoot, new AgentExecutionModel("pi", "gpt-5.4-mini"), fakeTransport);
        var task = new AgentTask(
            id: "task-2",
            kind: "wrong-kind",
            goal: "Should be rejected.",
            input: "{}");

        var result = await agent.ExecuteAsync(task);
        Expect(result.Status == AgentTaskStatus.Failed, "principal answer agent should reject unsupported task kinds", failures);
        Expect(result.Output is null, "principal answer agent should not emit output on kind rejection", failures);
        Expect(result.Errors.Count > 0 && result.Errors[0].Contains("Unsupported agent task kind", StringComparison.Ordinal), "principal answer agent should return a clear kind error", failures);
        Expect(fakeTransport.CapturedPrompt is null, "principal answer agent should not call transport for unsupported task kinds", failures);
    }

    private static async Task RunTransportFailureCaseAsync(List<string> failures)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-principal-answer-agent-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var fakeTransport = new FakePrincipalAnswerLlmTransport(
            new PrincipalAnswerLlmResult(
                Success: false,
                Prompt: "ignored",
                Answer: string.Empty,
                ErrorMessage: "pi failed before answering",
                Provider: null,
                Model: null,
                Transport: "stdin",
                EstimatedTokens: null,
                Warnings: Array.Empty<string>(),
                Errors: new[] { "pi failed before answering" }));

        var agent = new PiPrincipalAnswerAgent(tempRoot, new AgentExecutionModel("pi", "gpt-5.4-mini"), fakeTransport);
        var input = new PrincipalAnswerAgentInput(
            "Implement reset board action",
            "You are assisting inside an RFS repository session.\n[USER PROMPT]\nImplement reset board action",
            "{\"type\":\"rufus.context-pack\",\"schemaVersion\":1}",
            "Validated context pack with deterministic scope.",
            "complete::validated-context-pack");

        var task = new AgentTask(
            id: "task-3",
            kind: PrincipalAnswerAgentConstants.TaskKind,
            goal: "Produce the final answer from the validated ContextPack and user prompt.",
            input: JsonSerializer.Serialize(input, JsonOptions));

        var result = await agent.ExecuteAsync(task);
        Expect(result.Status == AgentTaskStatus.Failed, "principal answer agent should fail when the transport fails", failures);
        Expect(result.Output is null, "principal answer agent should not emit output when the transport fails", failures);
        Expect(result.Errors.Count > 0 && result.Errors[0].Contains("pi failed before answering", StringComparison.Ordinal), "principal answer agent should surface transport errors", failures);
    }

    private static void Expect(bool condition, string failure, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(failure);
        }
    }

    private sealed class FakePrincipalAnswerLlmTransport : IPrincipalAnswerLlmTransport
    {
        private readonly PrincipalAnswerLlmResult _result;

        public FakePrincipalAnswerLlmTransport(PrincipalAnswerLlmResult result)
        {
            _result = result;
        }

        public string? CapturedWorkingDirectory { get; private set; }
        public string? CapturedPrompt { get; private set; }
        public string? CapturedWorkspaceModel { get; private set; }

        public Task<PrincipalAnswerLlmResult> AskAsync(
            string workingDirectory,
            string prompt,
            string? workspaceModel,
            CancellationToken cancellationToken = default)
        {
            CapturedWorkingDirectory = workingDirectory;
            CapturedPrompt = prompt;
            CapturedWorkspaceModel = workspaceModel;
            return Task.FromResult(_result);
        }
    }
}
