using System.Text.Json;
using Rufus.Agenting;
using Rufus.Cli.ConversationalMemory;
using Rufus.Cli.Intent;
using Rufus.Cli.PiIntegration;
using Rufus.Cli.TraceSlice;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.ParserChecks;

internal static class RfsStageModelConfigChecks
{
    public static async Task RunAsync(List<string> failures)
    {
        await RunTryReadStageModelMissingFileCaseAsync(failures);
        await RunTryReadStageModelMissingLlmsStageCaseAsync(failures);
        await RunTryReadStageModelExactStageMatchCaseAsync(failures);
        await RunTryReadStageModelMissingStageCaseAsync(failures);
        await RunConversationalMemoryAgentModelOverrideCaseAsync(failures);
        await RunConversationalMemoryAgentDefaultModelCaseAsync(failures);
        await RunIntentAgentDefaultModelCaseAsync(failures);
        await RunProposalAgentDefaultModelCaseAsync(failures);
        await RunPartialStageConfigPreservesDefaultsCaseAsync(failures);
        await RunSetDefaultModelPreservesStagesCaseAsync(failures);
    }

    /// <summary>
    /// config file does not exist — TryReadStageModel returns null.
    /// </summary>
    private static Task RunTryReadStageModelMissingFileCaseAsync(List<string> failures)
    {
        const string name = "stage model config missing file returns null";
        var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-stage-model-config-check-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var result = RckWorkspaceModelConfigStore.TryReadStageModel("intent", tempDir);
            Expect(result is null, $"[{name}] expected null for missing config file but got '{result}'.", failures);
        }
        finally
        {
            SafeDeleteDirectory(tempDir);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// config file has no llm.stages — TryReadStageModel returns null.
    /// </summary>
    private static Task RunTryReadStageModelMissingLlmsStageCaseAsync(List<string> failures)
    {
        const string name = "stage model config missing llm.stages returns null";
        var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-stage-model-config-check-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            InitGitAndRfs(tempDir);

            // config has llm.defaultModel but no llm.stages
            var config = new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["type"] = "rufus.workspace",
                ["llm"] = new Dictionary<string, object?>
                {
                    ["defaultModel"] = "deepseek-v4-pro",
                },
            };
            File.WriteAllText(Path.Combine(tempDir, ".rfs", "config.json"), JsonSerializer.Serialize(config));

            var result = RckWorkspaceModelConfigStore.TryReadStageModel("intent", tempDir);
            Expect(result is null, $"[{name}] expected null for missing stages but got '{result}'.", failures);
        }
        finally
        {
            SafeDeleteDirectory(tempDir);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// config has llm.stages.intent.model — TryReadStageModel returns the value.
    /// </summary>
    private static Task RunTryReadStageModelExactStageMatchCaseAsync(List<string> failures)
    {
        const string name = "stage model config reads exact stage match";
        var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-stage-model-config-check-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            InitGitAndRfs(tempDir);

            var config = new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["type"] = "rufus.workspace",
                ["llm"] = new Dictionary<string, object?>
                {
                    ["defaultModel"] = "deepseek-v4-pro",
                    ["stages"] = new Dictionary<string, object?>
                    {
                        ["intent"] = new Dictionary<string, object?> { ["model"] = "deepseek-chat" },
                        ["traceSliceProposal"] = new Dictionary<string, object?> { ["model"] = "deepseek-v4-pro" },
                        ["conversationalMemory"] = new Dictionary<string, object?> { ["model"] = "deepseek-chat" },
                        ["principalAnswer"] = new Dictionary<string, object?> { ["model"] = "deepseek-chat" },
                    },
                },
            };
            File.WriteAllText(Path.Combine(tempDir, ".rfs", "config.json"), JsonSerializer.Serialize(config));

            var intentModel = RckWorkspaceModelConfigStore.TryReadStageModel("intent", tempDir);
            Expect(string.Equals(intentModel, "deepseek-chat", StringComparison.Ordinal), $"[{name}] expected 'deepseek-chat' for intent but got '{intentModel}'.", failures);

            var proposalModel = RckWorkspaceModelConfigStore.TryReadStageModel("traceSliceProposal", tempDir);
            Expect(string.Equals(proposalModel, "deepseek-v4-pro", StringComparison.Ordinal), $"[{name}] expected 'deepseek-v4-pro' for traceSliceProposal but got '{proposalModel}'.", failures);

            var memoryModel = RckWorkspaceModelConfigStore.TryReadStageModel("conversationalMemory", tempDir);
            Expect(string.Equals(memoryModel, "deepseek-chat", StringComparison.Ordinal), $"[{name}] expected 'deepseek-chat' for conversationalMemory but got '{memoryModel}'.", failures);
        }
        finally
        {
            SafeDeleteDirectory(tempDir);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// config has llm.stages but the requested stage is not present — TryReadStageModel returns null.
    /// </summary>
    private static Task RunTryReadStageModelMissingStageCaseAsync(List<string> failures)
    {
        const string name = "stage model config missing stage returns null";
        var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-stage-model-config-check-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            InitGitAndRfs(tempDir);

            var config = new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["type"] = "rufus.workspace",
                ["llm"] = new Dictionary<string, object?>
                {
                    ["defaultModel"] = "deepseek-v4-pro",
                    ["stages"] = new Dictionary<string, object?>
                    {
                        ["intent"] = new Dictionary<string, object?> { ["model"] = "deepseek-chat" },
                        ["traceSliceProposal"] = new Dictionary<string, object?> { ["model"] = "deepseek-v4-pro" },
                        ["principalAnswer"] = new Dictionary<string, object?> { ["model"] = "deepseek-chat" },
                    },
                },
            };
            File.WriteAllText(Path.Combine(tempDir, ".rfs", "config.json"), JsonSerializer.Serialize(config));

            var result = RckWorkspaceModelConfigStore.TryReadStageModel("principalAnswer", tempDir);
            Expect(result is null, $"[{name}] expected null for missing principalAnswer stage but got '{result}'.", failures);
        }
        finally
        {
            SafeDeleteDirectory(tempDir);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// PiConversationalMemoryAgent with explicit model override uses the override instead of the hardcoded default.
    /// </summary>
    private static async Task RunConversationalMemoryAgentModelOverrideCaseAsync(List<string> failures)
    {
        const string name = "conversational memory agent model override";
        var answerJson = BuildBasicConversationalMemoryAnswer();
        var transport = new FakeConversationalMemoryLlmTransport(success: true, answerJson: answerJson);
        var agent = new PiConversationalMemoryAgent("/tmp/cm-model-override-check", model: "deepseek-chat", transport: transport);

        Expect(string.Equals(agent.Descriptor.ExecutionModel.Model, "deepseek-chat", StringComparison.Ordinal), $"[{name}] expected model 'deepseek-chat' but got '{agent.Descriptor.ExecutionModel.Model}'.", failures);
        Expect(string.Equals(agent.Descriptor.ExecutionModel.Provider, "pi", StringComparison.Ordinal), $"[{name}] expected provider 'pi' but got '{agent.Descriptor.ExecutionModel.Provider}'.", failures);

        var task = new AgentTask(
            id: "cm-model-override",
            kind: "build-conversational-memory",
            goal: "build a conversational memory projection",
            input: JsonSerializer.Serialize(BuildBasicConversationalMemoryInput()),
            expectedOutput: "ConversationalMemory JSON");

        var result = await agent.ExecuteAsync(task);

        Expect(result.Status == AgentTaskStatus.Succeeded, $"[{name}] expected Succeeded but got {result.Status}.", failures);
        Expect(string.Equals(transport.LastModel, "deepseek-chat", StringComparison.Ordinal), $"[{name}] expected transport model 'deepseek-chat' but got '{transport.LastModel}'.", failures);
    }

    /// <summary>
    /// PiConversationalMemoryAgent without model override uses the hardcoded default.
    /// </summary>
    private static async Task RunConversationalMemoryAgentDefaultModelCaseAsync(List<string> failures)
    {
        const string name = "conversational memory agent default model";
        var answerJson = BuildBasicConversationalMemoryAnswer();
        var transport = new FakeConversationalMemoryLlmTransport(success: true, answerJson: answerJson);
        var agent = new PiConversationalMemoryAgent("/tmp/cm-default-model-check", transport: transport);

        Expect(string.Equals(agent.Descriptor.ExecutionModel.Model, "claude-haiku-4.5", StringComparison.Ordinal), $"[{name}] expected default model 'claude-haiku-4.5' but got '{agent.Descriptor.ExecutionModel.Model}'.", failures);
        Expect(string.Equals(agent.Descriptor.ExecutionModel.Provider, "pi", StringComparison.Ordinal), $"[{name}] expected provider 'pi' but got '{agent.Descriptor.ExecutionModel.Provider}'.", failures);

        var task = new AgentTask(
            id: "cm-default-model",
            kind: "build-conversational-memory",
            goal: "build a conversational memory projection",
            input: JsonSerializer.Serialize(BuildBasicConversationalMemoryInput()),
            expectedOutput: "ConversationalMemory JSON");

        var result = await agent.ExecuteAsync(task);

        Expect(result.Status == AgentTaskStatus.Succeeded, $"[{name}] expected Succeeded but got {result.Status}.", failures);
        Expect(string.Equals(transport.LastModel, "claude-haiku-4.5", StringComparison.Ordinal), $"[{name}] expected transport model 'claude-haiku-4.5' but got '{transport.LastModel}'.", failures);
    }

    /// <summary>
    /// PiIntentInferenceAgent without model override uses the hardcoded default.
    /// </summary>
    private static async Task RunIntentAgentDefaultModelCaseAsync(List<string> failures)
    {
        const string name = "intent agent default model";
        var answerJson = "{\"Intent\":\"code-change\",\"Summary\":\"test summary\",\"Entities\":[],\"Constraints\":[]}";
        var transport = new FakeIntentLlmTransport(success: true, answerJson: answerJson);
        var agent = new PiIntentInferenceAgent("/tmp/intent-default-model-check", transport: transport);

        Expect(string.Equals(agent.Descriptor.ExecutionModel.Model, "claude-haiku-4.5", StringComparison.Ordinal), $"[{name}] expected default model 'claude-haiku-4.5' but got '{agent.Descriptor.ExecutionModel.Model}'.", failures);
    }

    /// <summary>
    /// PiTraceSliceProposalAgent without model override uses the hardcoded default.
    /// </summary>
    private static async Task RunProposalAgentDefaultModelCaseAsync(List<string> failures)
    {
        const string name = "proposal agent default model";
        var agent = new PiTraceSliceProposalAgent("/tmp/proposal-default-model-check");

        Expect(string.Equals(agent.Descriptor.ExecutionModel.Model, "claude-sonnet-4.5", StringComparison.Ordinal), $"[{name}] expected default model 'claude-sonnet-4.5' but got '{agent.Descriptor.ExecutionModel.Model}'.", failures);
        Expect(string.Equals(agent.Descriptor.ExecutionModel.Provider, "pi", StringComparison.Ordinal), $"[{name}] expected provider 'pi' but got '{agent.Descriptor.ExecutionModel.Provider}'.", failures);
    }

    /// <summary>
    /// Partial stage config — only intent configured, others preserve defaults.
    /// </summary>
    private static Task RunPartialStageConfigPreservesDefaultsCaseAsync(List<string> failures)
    {
        const string name = "partial stage config preserves defaults";
        var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-stage-model-config-check-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            InitGitAndRfs(tempDir);

            var config = new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["type"] = "rufus.workspace",
                ["llm"] = new Dictionary<string, object?>
                {
                    ["defaultModel"] = "deepseek-v4-pro",
                    ["stages"] = new Dictionary<string, object?>
                    {
                        ["intent"] = new Dictionary<string, object?> { ["model"] = "deepseek-chat" },
                        ["traceSliceProposal"] = new Dictionary<string, object?> { ["model"] = "deepseek-v4-pro" },
                        ["principalAnswer"] = new Dictionary<string, object?> { ["model"] = "deepseek-chat" },
                    },
                },
            };
            File.WriteAllText(Path.Combine(tempDir, ".rfs", "config.json"), JsonSerializer.Serialize(config));

            var intentModel = RckWorkspaceModelConfigStore.TryReadStageModel("intent", tempDir);
            Expect(string.Equals(intentModel, "deepseek-chat", StringComparison.Ordinal), $"[{name}] expected 'deepseek-chat' for intent but got '{intentModel}'.", failures);

            // unconfigured stages should return null (use defaults)
            var proposalModel = RckWorkspaceModelConfigStore.TryReadStageModel("traceSliceProposal", tempDir);
            Expect(proposalModel is null, $"[{name}] expected null for unconfigured traceSliceProposal but got '{proposalModel}'.", failures);

            var memoryModel = RckWorkspaceModelConfigStore.TryReadStageModel("conversationalMemory", tempDir);
            Expect(memoryModel is null, $"[{name}] expected null for unconfigured conversationalMemory but got '{memoryModel}'.", failures);
        }
        finally
        {
            SafeDeleteDirectory(tempDir);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// SetDefaultModel writes llm.defaultModel without touching existing llm.stages.
    /// </summary>
    private static Task RunSetDefaultModelPreservesStagesCaseAsync(List<string> failures)
    {
        const string name = "SetDefaultModel preserves llm.stages";
        var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-stage-model-config-check-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            InitGitAndRfs(tempDir);

            // Write initial config with stages
            var initialConfig = new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["type"] = "rufus.workspace",
                ["llm"] = new Dictionary<string, object?>
                {
                    ["defaultModel"] = "gpt-5.4-mini",
                    ["stages"] = new Dictionary<string, object?>
                    {
                        ["intent"] = new Dictionary<string, object?> { ["model"] = "deepseek-chat" },
                        ["traceSliceProposal"] = new Dictionary<string, object?> { ["model"] = "deepseek-v4-pro" },
                        ["conversationalMemory"] = new Dictionary<string, object?> { ["model"] = "deepseek-chat" },
                        ["principalAnswer"] = new Dictionary<string, object?> { ["model"] = "deepseek-chat" },
                    },
                },
            };
            File.WriteAllText(Path.Combine(tempDir, ".rfs", "config.json"), JsonSerializer.Serialize(initialConfig));

            // Now call SetDefaultModel which should only update llm.defaultModel
            var setResult = RckWorkspaceModelConfigStore.SetDefaultModel("deepseek-v4-pro", tempDir);
            Expect(setResult.Success, $"[{name}] expected SetDefaultModel to succeed but got: {setResult.ErrorMessage}.", failures);

            // Verify stages survived
            var intentModel = RckWorkspaceModelConfigStore.TryReadStageModel("intent", tempDir);
            Expect(string.Equals(intentModel, "deepseek-chat", StringComparison.Ordinal), $"[{name}] intent stage should have survived SetDefaultModel but got '{intentModel}'.", failures);

            var proposalModel = RckWorkspaceModelConfigStore.TryReadStageModel("traceSliceProposal", tempDir);
            Expect(string.Equals(proposalModel, "deepseek-v4-pro", StringComparison.Ordinal), $"[{name}] traceSliceProposal stage should have survived SetDefaultModel but got '{proposalModel}'.", failures);

            // Verify defaultModel was updated
            var defaultModel = RckWorkspaceModelConfigStore.TryReadDefaultModel(tempDir);
            Expect(string.Equals(defaultModel, "deepseek-v4-pro", StringComparison.Ordinal), $"[{name}] expected defaultModel to be updated to 'deepseek-v4-pro' but got '{defaultModel}'.", failures);
        }
        finally
        {
            SafeDeleteDirectory(tempDir);
        }

        return Task.CompletedTask;
    }

    private static void InitGitAndRfs(string repoRoot)
    {
        var processStartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            ArgumentList = { "init" },
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = System.Diagnostics.Process.Start(processStartInfo);
        process?.WaitForExit();

        Directory.CreateDirectory(Path.Combine(repoRoot, ".rfs"));
    }

    private static string BuildBasicConversationalMemoryAnswer()
    {
        var answer = new Dictionary<string, object?>
        {
            ["type"] = "rufus.conversational-memory",
            ["schemaVersion"] = 1,
            ["summary"] = "Conversational continuity from recent interactions.",
            ["activeTopic"] = "Stage model config",
            ["openQuestions"] = new List<string>(),
            ["recentDecisions"] = new List<string>(),
            ["continuityHints"] = new List<string>(),
            ["warnings"] = new List<string>(),
        };
        return JsonSerializer.Serialize(answer);
    }

    private static RckConversationalMemoryInput BuildBasicConversationalMemoryInput()
    {
        return new RckConversationalMemoryInput(
            CurrentPrompt: "Test conversational memory with model override.",
            RecentInteractions: new[]
            {
                new RckConversationalMemoryInteraction("state-1", "delta-1", "tui-complete", "Prompt 1", "Answer 1", DateTimeOffset.UtcNow),
            },
            Limits: new RckConversationalMemoryLimits(1, 120, 1_000));
    }

    private static void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
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

        public string? LastPrompt { get; private set; }
        public string? LastModel { get; private set; }

        public Task<PiJsonAskResult> AskAsync(string workingDirectory, string prompt, string model, CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            LastModel = model;

            if (!_success)
            {
                return Task.FromResult(new PiJsonAskResult(false, prompt, string.Empty, "fake transport failure", "pi", model));
            }

            return Task.FromResult(new PiJsonAskResult(true, prompt, _answerJson, null, "pi", model));
        }
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

        public Task<PiJsonAskResult> AskAsync(string workingDirectory, string prompt, string model, CancellationToken cancellationToken = default)
        {
            if (!_success)
            {
                return Task.FromResult(new PiJsonAskResult(false, prompt, string.Empty, "fake transport failure", "pi", model));
            }

            return Task.FromResult(new PiJsonAskResult(true, prompt, _answerJson, null, "pi", model));
        }
    }
}
