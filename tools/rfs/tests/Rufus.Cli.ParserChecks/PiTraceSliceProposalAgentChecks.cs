using System.Text.Json;
using Rufus.Agenting;
using Rufus.Agenting.TraceSlice;
using Rufus.Cli.TraceSlice;
using Rufus.Cli.Tui;
using Rufus.Cli.PiIntegration;
using Rufus.RCK.Workspace;

public static class PiTraceSliceProposalAgentChecks
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task RunAsync(List<string> failures)
    {
        await RunSuccessCaseAsync(failures);
        await RunMarkdownFencedJsonCaseAsync(failures);
        await RunTrailingTextCaseAsync(failures);
        await RunMarkdownFencedNoLanguageCaseAsync(failures);
        await RunInvalidJsonCaseAsync(failures);
        await RunInvalidShapeCaseAsync(failures);
        await RunWrongTaskKindCaseAsync(failures);
    }

    private static async Task RunSuccessCaseAsync(List<string> failures)
    {
        const string prompt = "PLT1 Complete TraceSlice LLM proposal smoke: explain one safe next step for this repository.";
        var intent = new RckTraceSliceProposalIntentProjection(
            Kind: "build-trace-slice",
            Summary: "Prepare a focused slice for the next safe repository step.",
            Source: "pi-intent-inference");
        var dagQuickIndex = CreateDagQuickIndex();
        var answerJson = BuildTraceSliceProposalAnswer(prompt, intent, dagQuickIndex, includeInvalidShape: false, includeUnsafePolicy: false, includeBadKind: false);
        var transport = new FakeTraceSliceProposalLlmTransport(success: true, answerJson: answerJson);
        var agent = new PiTraceSliceProposalAgent("/tmp/trace-slice-proposal-agent-check", transport: transport);
        var task = new AgentTask(
            id: "trace-slice-proposal-agent-check",
            kind: "propose-trace-slice",
            goal: "build an LLM-backed trace slice proposal",
            input: JsonSerializer.Serialize(new TraceSliceProposalAgentInput(
                prompt,
                intent,
                dagQuickIndex,
                new TraceSliceProposalAgentLimits(MaxStates: 5, MaxDeltas: 5),
                new[]
                {
                    "Select only ids available in dagQuickIndex.",
                    "Respect maxStates/maxDeltas.",
                    "includeArtifactContents=false.",
                    "includeGitDiffs=false.",
                    "includeStdoutStderr=false.",
                    "includeJsonl=false.",
                }), JsonOptions),
            expectedOutput: "TraceSliceProposal JSON");

        var result = await agent.ExecuteAsync(task);

        if (result.Status != AgentTaskStatus.Succeeded)
        {
            failures.Add($"[trace-slice-proposal agent success] expected Succeeded but got {result.Status}. Errors: {string.Join(" | ", result.Errors)}");
            return;
        }

        if (!string.Equals(agent.Descriptor.ExecutionModel.Provider, "pi", StringComparison.Ordinal))
        {
            failures.Add($"[trace-slice-proposal agent success] expected provider 'pi' but got '{agent.Descriptor.ExecutionModel.Provider}'.");
        }

        if (!string.Equals(agent.Descriptor.ExecutionModel.Model, "claude-sonnet-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[trace-slice-proposal agent success] expected model 'claude-sonnet-4.5' but got '{agent.Descriptor.ExecutionModel.Model}'.");
        }

        if (!agent.Descriptor.Capabilities.Contains("trace-slice-proposal", StringComparer.Ordinal))
        {
            failures.Add("[trace-slice-proposal agent success] expected capability 'trace-slice-proposal'.");
        }

        if (transport.CallCount != 1)
        {
            failures.Add($"[trace-slice-proposal agent success] expected transport to be called once but got {transport.CallCount}.");
        }

        if (!string.Equals(transport.LastModel, "claude-sonnet-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[trace-slice-proposal agent success] expected transport model 'claude-sonnet-4.5' but got '{transport.LastModel}'.");
        }

        if (string.IsNullOrWhiteSpace(transport.LastPrompt))
        {
            failures.Add("[trace-slice-proposal agent success] expected prompt to be sent to transport.");
        }
        else
        {
            if (!transport.LastPrompt.Contains("Return only a single JSON object and nothing else.", StringComparison.Ordinal))
            {
                failures.Add("[trace-slice-proposal agent success] expected prompt to require JSON-only output.");
            }

            if (!transport.LastPrompt.Contains("schemaVersion = 1", StringComparison.Ordinal))
            {
                failures.Add("[trace-slice-proposal agent success] expected prompt to mention schemaVersion = 1.");
            }

            if (!transport.LastPrompt.Contains("includeArtifactContents=false", StringComparison.Ordinal))
            {
                failures.Add("[trace-slice-proposal agent success] expected prompt to forbid artifact contents.");
            }
        }

        var proposal = JsonSerializer.Deserialize<TraceSliceProposal>(result.Output!, JsonOptions);
        if (proposal is null)
        {
            failures.Add("[trace-slice-proposal agent success] expected output to deserialize to TraceSliceProposal.");
            return;
        }

        if (!string.Equals(proposal.Type, "rufus.trace-slice-proposal", StringComparison.Ordinal))
        {
            failures.Add($"[trace-slice-proposal agent success] expected proposal type 'rufus.trace-slice-proposal' but got '{proposal.Type}'.");
        }

        if (proposal.SchemaVersion != 1)
        {
            failures.Add($"[trace-slice-proposal agent success] expected schemaVersion 1 but got {proposal.SchemaVersion}.");
        }

        if (proposal.Prompt.IsExcerpt)
        {
            failures.Add("[trace-slice-proposal agent success] expected prompt.isExcerpt=false.");
        }

        if (proposal.RequestedSelection.StateIds.Count != 5 || proposal.RequestedSelection.DeltaIds.Count != 5 || proposal.RequestedSelection.AnchorIds.Count != 0)
        {
            failures.Add($"[trace-slice-proposal agent success] expected selection 5/5/0 but got {proposal.RequestedSelection.StateIds.Count}/{proposal.RequestedSelection.DeltaIds.Count}/{proposal.RequestedSelection.AnchorIds.Count}.");
        }

        if (proposal.RequestedMaterializationPolicy.IncludeArtifactContents || proposal.RequestedMaterializationPolicy.IncludeGitDiffs || proposal.RequestedMaterializationPolicy.IncludeStdoutStderr || proposal.RequestedMaterializationPolicy.IncludeJsonl)
        {
            failures.Add("[trace-slice-proposal agent success] expected restricted materialization flags to remain false.");
        }

        if (proposal.Rationale.Count == 0)
        {
            failures.Add("[trace-slice-proposal agent success] expected rationale entries.");
        }

        if (proposal.Warnings.Count != 0)
        {
            failures.Add($"[trace-slice-proposal agent success] expected empty warnings but got {proposal.Warnings.Count}.");
        }
    }

    private static async Task RunMarkdownFencedJsonCaseAsync(List<string> failures)
    {
        const string name = "trace-slice-proposal agent markdown fenced json";
        var prompt = "PLT1 Complete TraceSlice LLM proposal smoke: explain one safe next step for this repository.";
        var intent = new RckTraceSliceProposalIntentProjection(
            Kind: "build-trace-slice",
            Summary: "Prepare a focused slice for the next safe repository step.",
            Source: "pi-intent-inference");
        var dagQuickIndex = CreateDagQuickIndex();
        var answerJson = "```json\n" + BuildTraceSliceProposalAnswer(prompt, intent, dagQuickIndex, includeInvalidShape: false, includeUnsafePolicy: false, includeBadKind: false) + "\n```";
        var transport = new FakeTraceSliceProposalLlmTransport(success: true, answerJson: answerJson);
        var agent = new PiTraceSliceProposalAgent("/tmp/trace-slice-proposal-agent-check", transport: transport);
        var task = new AgentTask(
            id: "trace-slice-proposal-agent-markdown-fenced-json",
            kind: "propose-trace-slice",
            goal: "build an LLM-backed trace slice proposal",
            input: JsonSerializer.Serialize(new TraceSliceProposalAgentInput(
                prompt,
                intent,
                dagQuickIndex,
                new TraceSliceProposalAgentLimits(MaxStates: 5, MaxDeltas: 5),
                new[]
                {
                    "Select only ids available in dagQuickIndex.",
                    "Respect maxStates/maxDeltas = 5.",
                    "includeArtifactContents=false.",
                    "includeGitDiffs=false.",
                    "includeStdoutStderr=false.",
                    "includeJsonl=false.",
                    "Return JSON only.",
                    "No markdown fences or extra prose.",
                }), JsonOptions),
            expectedOutput: "TraceSliceProposal JSON");

        var result = await agent.ExecuteAsync(task);

        if (result.Status != AgentTaskStatus.Succeeded)
        {
            failures.Add($"[{name}] expected Succeeded but got {result.Status}. Errors: {string.Join(" | ", result.Errors)}");
            return;
        }

        var proposal = JsonSerializer.Deserialize<TraceSliceProposal>(result.Output!, JsonOptions);
        if (proposal is null)
        {
            failures.Add($"[{name}] expected output to deserialize to TraceSliceProposal.");
            return;
        }

        if (!string.Equals(proposal.Type, "rufus.trace-slice-proposal", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected type 'rufus.trace-slice-proposal' but got '{proposal.Type}'.");
        }
    }

    private static async Task RunTrailingTextCaseAsync(List<string> failures)
    {
        const string name = "trace-slice-proposal agent trailing text after json";
        var prompt = "PLT1 Complete TraceSlice LLM proposal smoke: explain one safe next step for this repository.";
        var intent = new RckTraceSliceProposalIntentProjection(
            Kind: "build-trace-slice",
            Summary: "Prepare a focused slice for the next safe repository step.",
            Source: "pi-intent-inference");
        var dagQuickIndex = CreateDagQuickIndex();
        var answerJson = BuildTraceSliceProposalAnswer(prompt, intent, dagQuickIndex, includeInvalidShape: false, includeUnsafePolicy: false, includeBadKind: false) + "\nExtra trailing note from the model.";
        var transport = new FakeTraceSliceProposalLlmTransport(success: true, answerJson: answerJson);
        var agent = new PiTraceSliceProposalAgent("/tmp/trace-slice-proposal-agent-check", transport: transport);
        var task = new AgentTask(
            id: "trace-slice-proposal-agent-trailing-text",
            kind: "propose-trace-slice",
            goal: "build an LLM-backed trace slice proposal",
            input: JsonSerializer.Serialize(new TraceSliceProposalAgentInput(
                prompt,
                intent,
                dagQuickIndex,
                new TraceSliceProposalAgentLimits(MaxStates: 5, MaxDeltas: 5),
                new[]
                {
                    "Select only ids available in dagQuickIndex.",
                    "Respect maxStates/maxDeltas = 5.",
                    "includeArtifactContents=false.",
                    "includeGitDiffs=false.",
                    "includeStdoutStderr=false.",
                    "includeJsonl=false.",
                    "Return JSON only.",
                    "No markdown fences or extra prose.",
                }), JsonOptions),
            expectedOutput: "TraceSliceProposal JSON");

        var result = await agent.ExecuteAsync(task);

        if (result.Status != AgentTaskStatus.Succeeded)
        {
            failures.Add($"[{name}] expected Succeeded but got {result.Status}. Errors: {string.Join(" | ", result.Errors)}");
            return;
        }

        var proposal = JsonSerializer.Deserialize<TraceSliceProposal>(result.Output!, JsonOptions);
        if (proposal is null)
        {
            failures.Add($"[{name}] expected output to deserialize to TraceSliceProposal.");
            return;
        }

        if (!string.Equals(proposal.Type, "rufus.trace-slice-proposal", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected type 'rufus.trace-slice-proposal' but got '{proposal.Type}'.");
        }
    }

    private static async Task RunMarkdownFencedNoLanguageCaseAsync(List<string> failures)
    {
        const string name = "trace-slice-proposal agent markdown fenced without language";
        var prompt = "PLT1 Complete TraceSlice LLM proposal smoke: explain one safe next step for this repository.";
        var intent = new RckTraceSliceProposalIntentProjection(
            Kind: "build-trace-slice",
            Summary: "Prepare a focused slice for the next safe repository step.",
            Source: "pi-intent-inference");
        var dagQuickIndex = CreateDagQuickIndex();
        var answerJson = "```\n" + BuildTraceSliceProposalAnswer(prompt, intent, dagQuickIndex, includeInvalidShape: false, includeUnsafePolicy: false, includeBadKind: false) + "\n```";
        var transport = new FakeTraceSliceProposalLlmTransport(success: true, answerJson: answerJson);
        var agent = new PiTraceSliceProposalAgent("/tmp/trace-slice-proposal-agent-check", transport: transport);
        var task = new AgentTask(
            id: "trace-slice-proposal-agent-markdown-fenced-no-language",
            kind: "propose-trace-slice",
            goal: "build an LLM-backed trace slice proposal",
            input: JsonSerializer.Serialize(new TraceSliceProposalAgentInput(
                prompt,
                intent,
                dagQuickIndex,
                new TraceSliceProposalAgentLimits(MaxStates: 5, MaxDeltas: 5),
                new[]
                {
                    "Select only ids available in dagQuickIndex.",
                    "Respect maxStates/maxDeltas = 5.",
                    "includeArtifactContents=false.",
                    "includeGitDiffs=false.",
                    "includeStdoutStderr=false.",
                    "includeJsonl=false.",
                    "Return JSON only.",
                    "No markdown fences or extra prose.",
                }), JsonOptions),
            expectedOutput: "TraceSliceProposal JSON");

        var result = await agent.ExecuteAsync(task);

        if (result.Status != AgentTaskStatus.Succeeded)
        {
            failures.Add($"[{name}] expected Succeeded but got {result.Status}. Errors: {string.Join(" | ", result.Errors)}");
            return;
        }

        var proposal = JsonSerializer.Deserialize<TraceSliceProposal>(result.Output!, JsonOptions);
        if (proposal is null)
        {
            failures.Add($"[{name}] expected output to deserialize to TraceSliceProposal.");
            return;
        }

        if (!string.Equals(proposal.Type, "rufus.trace-slice-proposal", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected type 'rufus.trace-slice-proposal' but got '{proposal.Type}'.");
        }
    }

    private static async Task RunInvalidJsonCaseAsync(List<string> failures)
    {
        const string prompt = "Test invalid JSON";
        var intent = new RckTraceSliceProposalIntentProjection("build-trace-slice", "Invalid JSON case.", "pi-intent-inference");
        var dagQuickIndex = CreateDagQuickIndex();
        var transport = new FakeTraceSliceProposalLlmTransport(success: true, answerJson: "not-json");
        var agent = new PiTraceSliceProposalAgent("/tmp/trace-slice-proposal-agent-check", transport: transport);
        var task = new AgentTask(
            id: "trace-slice-proposal-agent-invalid-json",
            kind: "propose-trace-slice",
            goal: "build an LLM-backed trace slice proposal",
            input: JsonSerializer.Serialize(new TraceSliceProposalAgentInput(
                prompt,
                intent,
                dagQuickIndex,
                new TraceSliceProposalAgentLimits(5, 5),
                Array.Empty<string>()), JsonOptions),
            expectedOutput: "TraceSliceProposal JSON");

        var result = await agent.ExecuteAsync(task);

        if (result.Status != AgentTaskStatus.Failed)
        {
            failures.Add($"[trace-slice-proposal agent invalid-json] expected Failed but got {result.Status}.");
        }

        if (result.Errors.Count == 0 || !result.Errors[0].Contains("invalid JSON", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"[trace-slice-proposal agent invalid-json] expected invalid JSON error but got: {string.Join(" | ", result.Errors)}");
        }
    }

    private static async Task RunInvalidShapeCaseAsync(List<string> failures)
    {
        const string prompt = "Test invalid shape";
        var intent = new RckTraceSliceProposalIntentProjection("build-trace-slice", "Invalid shape case.", "pi-intent-inference");
        var dagQuickIndex = CreateDagQuickIndex();
        var answerJson = BuildTraceSliceProposalAnswer(prompt, intent, dagQuickIndex, includeInvalidShape: true, includeUnsafePolicy: false, includeBadKind: false);
        var transport = new FakeTraceSliceProposalLlmTransport(success: true, answerJson: answerJson);
        var agent = new PiTraceSliceProposalAgent("/tmp/trace-slice-proposal-agent-check", transport: transport);
        var task = new AgentTask(
            id: "trace-slice-proposal-agent-invalid-shape",
            kind: "propose-trace-slice",
            goal: "build an LLM-backed trace slice proposal",
            input: JsonSerializer.Serialize(new TraceSliceProposalAgentInput(
                prompt,
                intent,
                dagQuickIndex,
                new TraceSliceProposalAgentLimits(5, 5),
                Array.Empty<string>()), JsonOptions),
            expectedOutput: "TraceSliceProposal JSON");

        var result = await agent.ExecuteAsync(task);

        if (result.Status != AgentTaskStatus.Failed)
        {
            failures.Add($"[trace-slice-proposal agent invalid-shape] expected Failed but got {result.Status}.");
        }

        if (result.Errors.Count == 0 || (!result.Errors[0].Contains("missing boolean property 'includeJsonl'", StringComparison.OrdinalIgnoreCase) && !result.Errors[0].Contains("invalid proposal payload", StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add($"[trace-slice-proposal agent invalid-shape] expected a shape validation error but got: {string.Join(" | ", result.Errors)}");
        }
    }

    private static async Task RunWrongTaskKindCaseAsync(List<string> failures)
    {
        const string prompt = "Test wrong task kind";
        var intent = new RckTraceSliceProposalIntentProjection("build-trace-slice", "Wrong task kind case.", "pi-intent-inference");
        var dagQuickIndex = CreateDagQuickIndex();
        var transport = new FakeTraceSliceProposalLlmTransport(success: true, answerJson: "{}")
        {
            AllowEmpty = true,
        };
        var agent = new PiTraceSliceProposalAgent("/tmp/trace-slice-proposal-agent-check", transport: transport);
        var task = new AgentTask(
            id: "trace-slice-proposal-agent-wrong-kind",
            kind: "infer-intent",
            goal: "build an LLM-backed trace slice proposal",
            input: JsonSerializer.Serialize(new TraceSliceProposalAgentInput(
                prompt,
                intent,
                dagQuickIndex,
                new TraceSliceProposalAgentLimits(5, 5),
                Array.Empty<string>()), JsonOptions),
            expectedOutput: "TraceSliceProposal JSON");

        var result = await agent.ExecuteAsync(task);

        if (result.Status != AgentTaskStatus.Failed)
        {
            failures.Add($"[trace-slice-proposal agent wrong-kind] expected Failed but got {result.Status}.");
        }

        if (transport.CallCount != 0)
        {
            failures.Add($"[trace-slice-proposal agent wrong-kind] expected transport not to be called but got {transport.CallCount} calls.");
        }

        if (result.Errors.Count == 0 || !result.Errors[0].Contains("Kind='propose-trace-slice'", StringComparison.Ordinal))
        {
            failures.Add($"[trace-slice-proposal agent wrong-kind] expected kind error but got: {string.Join(" | ", result.Errors)}");
        }
    }

    private static RckTraceSliceProposalDagQuickIndex CreateDagQuickIndex()
    {
        return new RckTraceSliceProposalDagQuickIndex(
            HeadStateId: "state-05",
            RecentStateIds: new[] { "state-01", "state-02", "state-03", "state-04", "state-05" },
            RecentDeltaIds: new[] { "delta-01", "delta-02", "delta-03", "delta-04", "delta-05" },
            Anchors: new[]
            {
                new RckTraceSliceProposalAnchorMetadata("anchor-01", "state-03", "important branch point", "important branch point", null, IsRecentChain: true),
            });
    }

    private static string BuildTraceSliceProposalAnswer(
        string prompt,
        RckTraceSliceProposalIntentProjection intent,
        RckTraceSliceProposalDagQuickIndex dagQuickIndex,
        bool includeInvalidShape,
        bool includeUnsafePolicy,
        bool includeBadKind)
    {
        var proposal = new Dictionary<string, object?>
        {
            ["type"] = includeBadKind ? "rufus.trace-slice-proposal-typo" : "rufus.trace-slice-proposal",
            ["schemaVersion"] = 1,
            ["prompt"] = new Dictionary<string, object?>
            {
                ["text"] = prompt,
                ["isExcerpt"] = false,
            },
            ["intent"] = new Dictionary<string, object?>
            {
                ["kind"] = intent.Kind,
                ["summary"] = intent.Summary,
                ["source"] = intent.Source,
            },
            ["requestedSelection"] = new Dictionary<string, object?>
            {
                ["stateIds"] = dagQuickIndex.RecentStateIds.Take(5).ToArray(),
                ["deltaIds"] = dagQuickIndex.RecentDeltaIds.Take(5).ToArray(),
                ["anchorIds"] = Array.Empty<string>(),
                ["artifactRefs"] = Array.Empty<string>(),
            },
            ["requestedMaterializationPolicy"] = new Dictionary<string, object?>
            {
                ["includeStatePayloads"] = true,
                ["includeDeltaDecodedOps"] = true,
                ["includeArtifactContents"] = includeUnsafePolicy,
                ["includeGitDiffs"] = includeUnsafePolicy,
                ["includeStdoutStderr"] = includeUnsafePolicy,
                ["includeJsonl"] = includeUnsafePolicy,
            },
            ["rationale"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["target"] = "state-05",
                    ["reason"] = "Useful breadcrumb for the next safe repository step.",
                },
            },
            ["confidence"] = 0.91,
            ["warnings"] = Array.Empty<string>(),
        };

        if (includeInvalidShape)
        {
            ((Dictionary<string, object?>)proposal["requestedMaterializationPolicy"]!).Remove("includeJsonl");
        }

        return JsonSerializer.Serialize(proposal, JsonOptions);
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
        public string? LastWorkingDirectory { get; private set; }
        public string? LastPrompt { get; private set; }
        public string? LastModel { get; private set; }
        public bool AllowEmpty { get; init; }

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

            if (!AllowEmpty && string.IsNullOrWhiteSpace(_answerJson))
            {
                return Task.FromResult(new PiJsonAskResult(false, prompt, string.Empty, "empty answer", "pi", model));
            }

            return Task.FromResult(new PiJsonAskResult(true, prompt, _answerJson, null, "pi", model));
        }
    }
}
