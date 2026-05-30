using System.Text.Json;
using Rufus.Agenting;
using Rufus.Agenting.TraceSlice;
using Rufus.Cli;
using Rufus.Cli.TraceSlice;
using Rufus.Cli.PiIntegration;
using Rufus.Cli.Tui;
using Rufus.RCK.Workspace;

internal static class RfsPromptDumpChecks
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task RunAsync(List<string> failures)
    {
        await RunTryDumpDisabledReturnsNullAsync(failures);
        await RunTryDumpEnabledCreatesFileAsync(failures);
        await RunTryDumpMetadataIsCorrectAsync(failures);
        await RunExecuteAnchorSelectionNoDumpWhenDisabledAsync(failures);
        await RunExecuteAnchorSelectionDumpWhenEnabledAsync(failures);
    }

    private static Task RunTryDumpDisabledReturnsNullAsync(List<string> failures)
    {
        var original = Environment.GetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS");
        Environment.SetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS", null);
        try
        {
            var result = RfsPromptDump.TryDump(
                stage: "traceslice-anchors",
                prompt: "test prompt",
                model: "deepseek-chat",
                source: "pi-trace-slice-proposal",
                workingDirectory: "/tmp/test");

            if (result is not null)
            {
                failures.Add("[prompt-dump disabled] expected null but got a dump path.");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS", original);
        }

        return Task.CompletedTask;
    }

    private static Task RunTryDumpEnabledCreatesFileAsync(List<string> failures)
    {
        var original = Environment.GetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS");
        Environment.SetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS", "1");
        try
        {
            var prompt = "Return only JSON.\nDo not use markdown fences.\n\nRequest JSON:\n{\"userPrompt\":\"test\"}";
            var result = RfsPromptDump.TryDump(
                stage: "traceslice-anchors",
                prompt: prompt,
                model: "deepseek-chat",
                source: "pi-trace-slice-proposal",
                workingDirectory: "/tmp/test");

            if (result is null)
            {
                failures.Add("[prompt-dump enabled] expected a dump path but got null.");
                return Task.CompletedTask;
            }

            if (!File.Exists(result))
            {
                failures.Add($"[prompt-dump enabled] dump file not found at {result}.");
                return Task.CompletedTask;
            }

            var content = File.ReadAllText(result);
            if (!content.Contains(prompt, StringComparison.Ordinal))
            {
                failures.Add("[prompt-dump enabled] dump file does not contain the expected prompt.");
            }

            try { File.Delete(result); } catch { /* best-effort */ }
        }
        finally
        {
            Environment.SetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS", original);
        }

        return Task.CompletedTask;
    }

    private static Task RunTryDumpMetadataIsCorrectAsync(List<string> failures)
    {
        var original = Environment.GetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS");
        Environment.SetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS", "1");
        try
        {
            var prompt = "the exact prompt body";
            var result = RfsPromptDump.TryDump(
                stage: "traceslice-anchors",
                prompt: prompt,
                model: "deepseek-chat",
                source: "pi-trace-slice-proposal",
                workingDirectory: "/tmp/test");

            if (result is null)
            {
                failures.Add("[prompt-dump metadata] expected a dump path but got null.");
                return Task.CompletedTask;
            }

            if (!File.Exists(result))
            {
                failures.Add($"[prompt-dump metadata] dump file not found at {result}.");
                return Task.CompletedTask;
            }

            var content = File.ReadAllText(result);

            static void ExpectHeader(List<string> f, string content, string header)
            {
                if (!content.Contains(header, StringComparison.Ordinal))
                {
                    f.Add($"[prompt-dump metadata] expected header '{header}' not found in dump.");
                }
            }

            ExpectHeader(failures, content, "# stage=traceslice-anchors");
            ExpectHeader(failures, content, "# model=deepseek-chat");
            ExpectHeader(failures, content, "# source=pi-trace-slice-proposal");
            ExpectHeader(failures, content, $"# promptLen={prompt.Length}");
            ExpectHeader(failures, content, "# workingDirectory=/tmp/test");

            try { File.Delete(result); } catch { /* best-effort */ }
        }
        finally
        {
            Environment.SetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS", original);
        }

        return Task.CompletedTask;
    }

    private static async Task RunExecuteAnchorSelectionNoDumpWhenDisabledAsync(List<string> failures)
    {
        var original = Environment.GetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS");
        Environment.SetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS", null);
        try
        {
            var prompt = "Pick the structural entry-point anchor only.";
            var intent = new RckTraceSliceProposalIntentProjection(
                Kind: "question",
                Summary: "A factual question about the repo.",
                Source: "pi-intent-inference");
            var dagQuickIndex = CreateDagQuickIndex();
            var answerJson = BuildAnchorSelectionAnswer(
                selectedAnchorIds: Array.Empty<string>(),
                fallbackStrategy: "recent-chain",
                rationale: new[] { ("recent-chain", "Factual question, no anchors relevant.") },
                warnings: Array.Empty<string>(),
                confidence: 0.9,
                schemaVersion: 1,
                type: "rufus.anchor-selection");

            var transport = new FakeTraceSliceProposalLlmTransport(answerJson);
            var agent = new PiTraceSliceProposalAgent("/tmp/anchor-selection-dump-disabled", transport: transport);

            string? dumpedPath = null;
            var task = new AgentTask(
                id: "anchor-selection-dump-disabled",
                kind: "select-trace-anchors",
                goal: "build an internal anchor selection",
                input: JsonSerializer.Serialize(new TraceSliceAnchorSelectionAgentInput(
                    prompt,
                    intent,
                    dagQuickIndex,
                    new[] { "Return JSON only.", "No markdown fences." }), JsonOptions),
                expectedOutput: "RckAnchorSelection JSON");

            var result = await agent.ExecuteAnchorSelectionAsync(task,
                onPromptDumped: path => { dumpedPath = path; });

            if (result.Status != AgentTaskStatus.Succeeded)
            {
                failures.Add($"[prompt-dump disabled-agent] expected Succeeded but got {result.Status}.");
            }

            if (transport.CallCount != 1)
            {
                failures.Add($"[prompt-dump disabled-agent] expected one transport call but got {transport.CallCount}.");
            }

            if (dumpedPath is not null)
            {
                failures.Add("[prompt-dump disabled-agent] expected no dump callback but callback was invoked.");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS", original);
        }
    }

    private static async Task RunExecuteAnchorSelectionDumpWhenEnabledAsync(List<string> failures)
    {
        var original = Environment.GetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS");
        Environment.SetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS", "1");
        try
        {
            var prompt = "Pick the structural entry-point anchor only.";
            var intent = new RckTraceSliceProposalIntentProjection(
                Kind: "build-trace-slice",
                Summary: "Build a trace slice.",
                Source: "pi-intent-inference");
            var dagQuickIndex = CreateDagQuickIndex();
            var answerJson = BuildAnchorSelectionAnswer(
                selectedAnchorIds: new[] { "anchor-a" },
                fallbackStrategy: "none",
                rationale: new[] { ("anchor-a", "Best entry point.") },
                warnings: Array.Empty<string>(),
                confidence: 0.94,
                schemaVersion: 1,
                type: "rufus.anchor-selection");

            var transport = new FakeTraceSliceProposalLlmTransport(answerJson);
            var agent = new PiTraceSliceProposalAgent("/tmp/anchor-selection-dump-enabled", transport: transport);

            string? dumpedPath = null;
            var task = new AgentTask(
                id: "anchor-selection-dump-enabled",
                kind: "select-trace-anchors",
                goal: "build an internal anchor selection",
                input: JsonSerializer.Serialize(new TraceSliceAnchorSelectionAgentInput(
                    prompt,
                    intent,
                    dagQuickIndex,
                    new[] { "Return JSON only.", "No markdown fences." }), JsonOptions),
                expectedOutput: "RckAnchorSelection JSON");

            var result = await agent.ExecuteAnchorSelectionAsync(task,
                onPromptDumped: path => { dumpedPath = path; });

            if (result.Status != AgentTaskStatus.Succeeded)
            {
                failures.Add($"[prompt-dump enabled-agent] expected Succeeded but got {result.Status}.");
            }

            if (transport.CallCount != 1)
            {
                failures.Add($"[prompt-dump enabled-agent] expected one transport call but got {transport.CallCount}.");
            }

            if (dumpedPath is null)
            {
                failures.Add("[prompt-dump enabled-agent] expected dump callback but none was invoked.");
            }
            else
            {
                if (!File.Exists(dumpedPath))
                {
                    failures.Add($"[prompt-dump enabled-agent] dump file not found at {dumpedPath}.");
                }
                else
                {
                    var content = File.ReadAllText(dumpedPath);
                    if (!content.Contains("Return JSON only.", StringComparison.Ordinal))
                    {
                        failures.Add("[prompt-dump enabled-agent] dump file does not contain expected prompt content.");
                    }

                    if (!content.Contains("# stage=traceslice-anchors", StringComparison.Ordinal))
                    {
                        failures.Add("[prompt-dump enabled-agent] dump file missing stage header.");
                    }

                    if (!content.Contains("# model=claude-sonnet-4.5", StringComparison.Ordinal))
                    {
                        failures.Add("[prompt-dump enabled-agent] dump file missing model header.");
                    }

                    if (!content.Contains("# source=pi-trace-slice-proposal", StringComparison.Ordinal))
                    {
                        failures.Add("[prompt-dump enabled-agent] dump file missing source header.");
                    }

                    try { File.Delete(dumpedPath); } catch { /* best-effort */ }
                }
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("RFS_TRACE_SLICE_DUMP_PROMPTS", original);
        }
    }

    private static RckDagQuickIndexV1 CreateDagQuickIndex()
    {
        return new RckDagQuickIndexV1(
            HeadStateId: "state-03",
            RecentStateIds: new[] { "state-03", "state-02", "state-01" },
            RecentDeltaIds: new[] { "delta-02", "delta-01" },
            Anchors: new[]
            {
                new RckDagAnchorCandidate("anchor-a", "state-02", "anchor A", "branch point A", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), true, Array.Empty<string>(), 1, new[] { "delta-1" }, new[] { "delta-2" }),
                new RckDagAnchorCandidate("anchor-b", "state-01", "anchor B", "branch point B", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), false, Array.Empty<string>(), 2, Array.Empty<string>(), new[] { "delta-1" }),
            },
            States: new[]
            {
                new RckDagStateCandidate("state-01", "state-01", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), Array.Empty<string>(), Array.Empty<string>(), new[] { "delta-1" }, 2, null, null, null),
                new RckDagStateCandidate("state-02", "state-02", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), new[] { "anchor-a" }, new[] { "delta-1" }, new[] { "delta-2" }, 1, null, null, null),
                new RckDagStateCandidate("state-03", "state-03", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), Array.Empty<string>(), new[] { "delta-2" }, Array.Empty<string>(), 0, null, null, null),
            },
            Deltas: new[]
            {
                new RckDagDeltaCandidate("delta-1", "state-01", "state-02", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), "replace:notes/1", "evidence 1"),
                new RckDagDeltaCandidate("delta-2", "state-02", "state-03", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), "replace:notes/2", "evidence 2"),
            });
    }

    internal static string BuildAnchorSelectionAnswer(
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

    private sealed class FakeTraceSliceProposalLlmTransport : ITraceSliceProposalLlmTransport
    {
        private readonly string _answerJson;

        public FakeTraceSliceProposalLlmTransport(string answerJson)
        {
            _answerJson = answerJson;
        }

        public int CallCount { get; private set; }
        public string? LastPrompt { get; private set; }

        public Task<PiJsonAskResult> AskAsync(string workingDirectory, string prompt, string model, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastPrompt = prompt;
            return Task.FromResult(new PiJsonAskResult(true, prompt, _answerJson, null, "pi", model));
        }
    }
}
