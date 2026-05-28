using System.Text.Json;
using Rufus.Agenting;
using Rufus.Cli.ConversationalMemory;
using Rufus.Cli.PiIntegration;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.ParserChecks;

internal static class PiConversationalMemoryAgentChecks
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task RunAsync(List<string> failures)
    {
        await RunSuccessCaseAsync(failures);
        await RunInvalidJsonCaseAsync(failures);
        await RunWrongTypeCaseAsync(failures);
        await RunWrongSchemaVersionCaseAsync(failures);
        await RunForbiddenFragmentsCaseAsync(failures);
        await RunMarkdownFencesCaseAsync(failures);
        await RunPromptGuardrailsCaseAsync(failures);
        await RunExecutionModelCaseAsync(failures);
        await RunUnexpectedPropertyCaseAsync(failures);
    }

    private static async Task RunSuccessCaseAsync(List<string> failures)
    {
        const string name = "conversational memory agent success";
        var input = BuildInput();
        var answerJson = BuildConversationalMemoryAnswer();
        var transport = new FakeConversationalMemoryLlmTransport(success: true, answerJson: answerJson);
        var agent = new PiConversationalMemoryAgent("/tmp/conversational-memory-agent-check", transport: transport);
        var task = new AgentTask(
            id: "conversational-memory-agent-success",
            kind: "build-conversational-memory",
            goal: "build a conversational memory projection",
            input: JsonSerializer.Serialize(input, JsonOptions),
            expectedOutput: "ConversationalMemory JSON");

        var result = await agent.ExecuteAsync(task);

        if (result.Status != AgentTaskStatus.Succeeded)
        {
            failures.Add($"[{name}] expected Succeeded but got {result.Status}. Errors: {string.Join(" | ", result.Errors)}");
            return;
        }

        if (!string.Equals(agent.Descriptor.ExecutionModel.Provider, "pi", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected provider pi but got {agent.Descriptor.ExecutionModel.Provider}.");
        }

        if (!string.Equals(agent.Descriptor.ExecutionModel.Model, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected claude-haiku-4.5 but got {agent.Descriptor.ExecutionModel.Model}.");
        }

        if (!string.Equals(transport.LastModel, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected transport model claude-haiku-4.5 but got {transport.LastModel}.");
        }

        var parsed = RckConversationalMemoryJsonCodec.Parse(result.Output!);
        Expect(string.Equals(parsed.Type, "rufus.conversational-memory", StringComparison.Ordinal), $"[{name}] expected type.", failures);
        Expect(parsed.SchemaVersion == 1, $"[{name}] expected schemaVersion=1.", failures);
        Expect(string.Equals(parsed.Summary, "We are deciding how to derive conversational continuity from RCK.", StringComparison.Ordinal), $"[{name}] expected summary.", failures);
        Expect(string.Equals(parsed.ActiveTopic, "Conversations over RCK logs", StringComparison.Ordinal), $"[{name}] expected activeTopic.", failures);
        Expect(parsed.OpenQuestions.SequenceEqual(new[] { "Should warnings be surfaced?" }), $"[{name}] expected openQuestions.", failures);
        Expect(parsed.RecentDecisions.SequenceEqual(new[] { "Use RckWorkspaceLogReader as the source." }), $"[{name}] expected recentDecisions.", failures);
        Expect(parsed.ContinuityHints.SequenceEqual(new[] { "The user is asking about continuity, not DAG slicing." }), $"[{name}] expected continuityHints.", failures);
        Expect(parsed.Warnings.SequenceEqual(new[] { "compact-summary" }), $"[{name}] expected warnings.", failures);
        Expect(result.Warnings.SequenceEqual(parsed.Warnings), $"[{name}] expected result warnings to mirror parsed warnings.", failures);
    }

    private static async Task RunInvalidJsonCaseAsync(List<string> failures)
    {
        const string name = "conversational memory agent invalid json";
        var agent = new PiConversationalMemoryAgent("/tmp/conversational-memory-agent-check", transport: new FakeConversationalMemoryLlmTransport(success: true, answerJson: "not-json"));
        var task = new AgentTask(
            id: "conversational-memory-agent-invalid-json",
            kind: "build-conversational-memory",
            goal: "build a conversational memory projection",
            input: JsonSerializer.Serialize(BuildInput(), JsonOptions),
            expectedOutput: "ConversationalMemory JSON");

        var result = await agent.ExecuteAsync(task);

        Expect(result.Status == AgentTaskStatus.Failed, $"[{name}] expected Failed but got {result.Status}.", failures);
        Expect(result.Errors.Count > 0 && result.Errors[0].Contains("invalid JSON", StringComparison.OrdinalIgnoreCase), $"[{name}] expected invalid JSON error but got: {string.Join(" | ", result.Errors)}", failures);
    }

    private static async Task RunWrongTypeCaseAsync(List<string> failures)
    {
        const string name = "conversational memory agent wrong type";
        var answer = BuildConversationalMemoryAnswer(type: "rufus.not-conversational-memory");
        var agent = new PiConversationalMemoryAgent("/tmp/conversational-memory-agent-check", transport: new FakeConversationalMemoryLlmTransport(success: true, answerJson: answer));
        var task = new AgentTask(
            id: "conversational-memory-agent-wrong-type",
            kind: "build-conversational-memory",
            goal: "build a conversational memory projection",
            input: JsonSerializer.Serialize(BuildInput(), JsonOptions),
            expectedOutput: "ConversationalMemory JSON");

        var result = await agent.ExecuteAsync(task);

        Expect(result.Status == AgentTaskStatus.Failed, $"[{name}] expected Failed but got {result.Status}.", failures);
        Expect(result.Errors.Count > 0 && result.Errors[0].Contains("type='rufus.conversational-memory'", StringComparison.OrdinalIgnoreCase), $"[{name}] expected type validation error but got: {string.Join(" | ", result.Errors)}", failures);
    }

    private static async Task RunWrongSchemaVersionCaseAsync(List<string> failures)
    {
        const string name = "conversational memory agent wrong schemaVersion";
        var answer = BuildConversationalMemoryAnswer(schemaVersion: 2);
        var agent = new PiConversationalMemoryAgent("/tmp/conversational-memory-agent-check", transport: new FakeConversationalMemoryLlmTransport(success: true, answerJson: answer));
        var task = new AgentTask(
            id: "conversational-memory-agent-wrong-schema",
            kind: "build-conversational-memory",
            goal: "build a conversational memory projection",
            input: JsonSerializer.Serialize(BuildInput(), JsonOptions),
            expectedOutput: "ConversationalMemory JSON");

        var result = await agent.ExecuteAsync(task);

        Expect(result.Status == AgentTaskStatus.Failed, $"[{name}] expected Failed but got {result.Status}.", failures);
        Expect(result.Errors.Count > 0 && result.Errors[0].Contains("schemaVersion=1", StringComparison.OrdinalIgnoreCase), $"[{name}] expected schemaVersion error but got: {string.Join(" | ", result.Errors)}", failures);
    }

    private static async Task RunForbiddenFragmentsCaseAsync(List<string> failures)
    {
        const string name = "conversational memory agent forbidden fragments";
        var answer = BuildConversationalMemoryAnswer(summary: "This mentions diff --git and stdout and should fail.");
        var agent = new PiConversationalMemoryAgent("/tmp/conversational-memory-agent-check", transport: new FakeConversationalMemoryLlmTransport(success: true, answerJson: answer));
        var task = new AgentTask(
            id: "conversational-memory-agent-forbidden-fragments",
            kind: "build-conversational-memory",
            goal: "build a conversational memory projection",
            input: JsonSerializer.Serialize(BuildInput(), JsonOptions),
            expectedOutput: "ConversationalMemory JSON");

        var result = await agent.ExecuteAsync(task);

        Expect(result.Status == AgentTaskStatus.Failed, $"[{name}] expected Failed but got {result.Status}.", failures);
        Expect(result.Errors.Count > 0 && (result.Errors[0].Contains("forbidden", StringComparison.OrdinalIgnoreCase) || result.Errors[0].Contains("stdout", StringComparison.OrdinalIgnoreCase)), $"[{name}] expected forbidden-fragment rejection but got: {string.Join(" | ", result.Errors)}", failures);
    }

    private static async Task RunMarkdownFencesCaseAsync(List<string> failures)
    {
        const string name = "conversational memory agent markdown fences";
        var answer = "```json\n{\"type\":\"rufus.conversational-memory\"}\n```";
        var agent = new PiConversationalMemoryAgent("/tmp/conversational-memory-agent-check", transport: new FakeConversationalMemoryLlmTransport(success: true, answerJson: answer));
        var task = new AgentTask(
            id: "conversational-memory-agent-markdown-fences",
            kind: "build-conversational-memory",
            goal: "build a conversational memory projection",
            input: JsonSerializer.Serialize(BuildInput(), JsonOptions),
            expectedOutput: "ConversationalMemory JSON");

        var result = await agent.ExecuteAsync(task);

        Expect(result.Status == AgentTaskStatus.Failed, $"[{name}] expected Failed but got {result.Status}.", failures);
        Expect(result.Errors.Count > 0, $"[{name}] expected at least one validation error.", failures);
    }

    private static async Task RunPromptGuardrailsCaseAsync(List<string> failures)
    {
        const string name = "conversational memory agent prompt guardrails";
        var transport = new FakeConversationalMemoryLlmTransport(success: true, answerJson: BuildConversationalMemoryAnswer());
        var agent = new PiConversationalMemoryAgent("/tmp/conversational-memory-agent-check", transport: transport);
        var task = new AgentTask(
            id: "conversational-memory-agent-prompt-guardrails",
            kind: "build-conversational-memory",
            goal: "build a conversational memory projection",
            input: JsonSerializer.Serialize(BuildInput(), JsonOptions),
            expectedOutput: "ConversationalMemory JSON");

        var result = await agent.ExecuteAsync(task);

        Expect(result.Status == AgentTaskStatus.Succeeded, $"[{name}] expected Succeeded but got {result.Status}.", failures);
        var prompt = transport.LastPrompt ?? string.Empty;
        Expect(prompt.Contains("Do not perform TraceSlice selection.", StringComparison.Ordinal), $"[{name}] expected trace-slice guardrail.", failures);
        Expect(prompt.Contains("Do not select anchors/states/deltas.", StringComparison.Ordinal), $"[{name}] expected selection guardrail.", failures);
        Expect(prompt.Contains("Use only the provided recent interactions.", StringComparison.Ordinal), $"[{name}] expected input-only guardrail.", failures);
        Expect(prompt.Contains("Return only a single JSON object and nothing else.", StringComparison.Ordinal), $"[{name}] expected JSON-only guardrail.", failures);
    }

    private static async Task RunExecutionModelCaseAsync(List<string> failures)
    {
        const string name = "conversational memory agent fixed execution model";
        var transport = new FakeConversationalMemoryLlmTransport(success: true, answerJson: BuildConversationalMemoryAnswer());
        var agent = new PiConversationalMemoryAgent("/tmp/conversational-memory-agent-check", transport: transport);

        Expect(string.Equals(agent.Descriptor.ExecutionModel.Provider, "pi", StringComparison.Ordinal), $"[{name}] expected provider pi.", failures);
        Expect(string.Equals(agent.Descriptor.ExecutionModel.Model, "claude-haiku-4.5", StringComparison.Ordinal), $"[{name}] expected fixed model claude-haiku-4.5.", failures);

        await Task.CompletedTask;
    }

    private static async Task RunUnexpectedPropertyCaseAsync(List<string> failures)
    {
        const string name = "conversational memory agent rejects unexpected properties";
        var answer = "{\"type\":\"rufus.conversational-memory\",\"schemaVersion\":1,\"summary\":\"ok\",\"activeTopic\":\"topic\",\"openQuestions\":[],\"recentDecisions\":[],\"continuityHints\":[],\"warnings\":[],\"extra\":true}";
        var agent = new PiConversationalMemoryAgent("/tmp/conversational-memory-agent-check", transport: new FakeConversationalMemoryLlmTransport(success: true, answerJson: answer));
        var task = new AgentTask(
            id: "conversational-memory-agent-unexpected-property",
            kind: "build-conversational-memory",
            goal: "build a conversational memory projection",
            input: JsonSerializer.Serialize(BuildInput(), JsonOptions),
            expectedOutput: "ConversationalMemory JSON");

        var result = await agent.ExecuteAsync(task);

        Expect(result.Status == AgentTaskStatus.Failed, $"[{name}] expected Failed but got {result.Status}.", failures);
        Expect(result.Errors.Count > 0 && result.Errors[0].Contains("Unexpected property", StringComparison.OrdinalIgnoreCase), $"[{name}] expected unexpected-property validation error but got: {string.Join(" | ", result.Errors)}", failures);
    }

    private static RckConversationalMemoryInput BuildInput()
    {
        return new RckConversationalMemoryInput(
            CurrentPrompt: "What should we do next with conversational memory?",
            RecentInteractions: new[]
            {
                new RckConversationalMemoryInteraction("state-3", "delta-3", "tui-complete", "Prompt 3", "Answer summary 3", new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero)),
                new RckConversationalMemoryInteraction("state-2", "delta-2", "tui-simple", "Prompt 2", "Answer summary 2", new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)),
                new RckConversationalMemoryInteraction("state-1", null, "tui-direct", "Prompt 1", "Answer summary 1", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            },
            Limits: new RckConversationalMemoryLimits(3, 120, 1_000));
    }

    private static string BuildConversationalMemoryAnswer(
        string type = "rufus.conversational-memory",
        int schemaVersion = 1,
        string summary = "We are deciding how to derive conversational continuity from RCK.",
        string activeTopic = "Conversations over RCK logs",
        IReadOnlyList<string>? openQuestions = null,
        IReadOnlyList<string>? recentDecisions = null,
        IReadOnlyList<string>? continuityHints = null,
        IReadOnlyList<string>? warnings = null)
    {
        var answer = new Dictionary<string, object?>
        {
            ["type"] = type,
            ["schemaVersion"] = schemaVersion,
            ["summary"] = summary,
            ["activeTopic"] = activeTopic,
            ["openQuestions"] = openQuestions ?? new[] { "Should warnings be surfaced?" },
            ["recentDecisions"] = recentDecisions ?? new[] { "Use RckWorkspaceLogReader as the source." },
            ["continuityHints"] = continuityHints ?? new[] { "The user is asking about continuity, not DAG slicing." },
            ["warnings"] = warnings ?? new[] { "compact-summary" },
        };

        return JsonSerializer.Serialize(answer, JsonOptions);
    }

    private static void Expect(bool condition, string message, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }

    private sealed class FakeConversationalMemoryLlmTransport : IConversationalMemoryLlmTransport
    {
        private readonly bool _success;
        private readonly string _answerJson;

        public FakeConversationalMemoryLlmTransport(bool success, string answerJson)
        {
            _success = success;
            _answerJson = answerJson;
        }

        public int CallCount { get; private set; }
        public string? LastWorkingDirectory { get; private set; }
        public string? LastPrompt { get; private set; }
        public string? LastModel { get; private set; }

        public Task<PiJsonAskResult> AskAsync(string workingDirectory, string prompt, string model, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastWorkingDirectory = workingDirectory;
            LastPrompt = prompt;
            LastModel = model;

            if (!_success)
            {
                return Task.FromResult(new PiJsonAskResult(false, prompt, string.Empty, "fake transport failure", "pi", model));
            }

            return Task.FromResult(new PiJsonAskResult(true, prompt, _answerJson, null, "pi", model));
        }
    }
}
