using System.Text.Json;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.ParserChecks;

internal static class RckConversationalMemoryInputBuilderChecks
{
    public static async Task RunAsync(List<string> failures)
    {
        await RunHappyPathAsync(failures);
        await RunMaxInteractionsCaseAsync(failures);
        await RunPromptTruncationCaseAsync(failures);
        await RunTotalBudgetCaseAsync(failures);
        await RunAnswerSummaryCaseAsync(failures);
        await RunUnsafeFieldLeakageCaseAsync(failures);
        await RunEmptyHistoryCaseAsync(failures);
        await RunDeterministicOrderingCaseAsync(failures);
    }

    private static async Task RunHappyPathAsync(List<string> failures)
    {
        const string name = "conversational memory builder happy path";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateConversationFixture(tempRoot, interactionCount: 4, includeGenesis: true);

            var limits = new RckConversationalMemoryLimits(MaxInteractions: 8, MaxPromptChars: 120, MaxTotalChars: 1_000);
            var result = RckConversationalMemoryInputBuilder.Build(tempRoot, "Current prompt for continuity.", limits);

            Expect(result.Success, $"[{name}] expected success.", failures);
            if (!result.Success || result.Input is null)
            {
                return;
            }

            Expect(string.Equals(result.Input.CurrentPrompt, "Current prompt for continuity.", StringComparison.Ordinal), $"[{name}] expected currentPrompt to be preserved.", failures);
            Expect(result.Input.RecentInteractions.Count == 4, $"[{name}] expected 4 recent interactions after excluding genesis.", failures);
            Expect(result.Input.RecentInteractions[0].StateId == "state-4", $"[{name}] expected newest interaction first.", failures);
            Expect(result.Input.RecentInteractions[0].DeltaId == "delta-4", $"[{name}] expected delta id for newest interaction.", failures);
            Expect(result.Input.RecentInteractions[0].Mode == "tui-complete", $"[{name}] expected newest mode.", failures);
            Expect(result.Input.RecentInteractions[0].Prompt == "Prompt 4", $"[{name}] expected prompt 4.", failures);
            Expect(result.Input.RecentInteractions[0].AnswerSummary == "Answer summary 4", $"[{name}] expected answer summary 4.", failures);
            Expect(result.Input.RecentInteractions[0].CreatedAtUtc == DateTimeOffset.Parse("2026-01-04T00:00:00.0000000+00:00", null, System.Globalization.DateTimeStyles.RoundtripKind), $"[{name}] expected createdAtUtc for newest interaction.", failures);
            Expect(result.Warnings.Count == 0, $"[{name}] expected no warnings.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunMaxInteractionsCaseAsync(List<string> failures)
    {
        const string name = "conversational memory builder respects max interactions";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateConversationFixture(tempRoot, interactionCount: 10, includeGenesis: true);

            var limits = new RckConversationalMemoryLimits(MaxInteractions: 3, MaxPromptChars: 120, MaxTotalChars: 2_000);
            var result = RckConversationalMemoryInputBuilder.Build(tempRoot, "Current prompt for continuity.", limits);

            Expect(result.Success, $"[{name}] expected success.", failures);
            if (!result.Success || result.Input is null)
            {
                return;
            }

            Expect(result.Input.RecentInteractions.Count == 3, $"[{name}] expected 3 interactions.", failures);
            Expect(result.Input.RecentInteractions.Select(interaction => interaction.StateId).SequenceEqual(new[] { "state-10", "state-9", "state-8" }), $"[{name}] expected newest-first truncation.", failures);
            Expect(result.Warnings.Contains("recent-interactions-truncated"), $"[{name}] expected a truncation warning.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunPromptTruncationCaseAsync(List<string> failures)
    {
        const string name = "conversational memory builder truncates prompts";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateConversationFixture(tempRoot, interactionCount: 2, includeGenesis: true, promptLength: 80);

            var limits = new RckConversationalMemoryLimits(MaxInteractions: 8, MaxPromptChars: 12, MaxTotalChars: 2_000);
            var result = RckConversationalMemoryInputBuilder.Build(tempRoot, "Current prompt for continuity.", limits);

            Expect(result.Success, $"[{name}] expected success.", failures);
            if (!result.Success || result.Input is null)
            {
                return;
            }

            var newestPrompt = result.Input.RecentInteractions[0].Prompt;
            Expect(newestPrompt.Length == 12, $"[{name}] expected prompt truncation to 12 chars but got {newestPrompt.Length}.", failures);
            Expect(result.Warnings.Contains("prompt-truncated"), $"[{name}] expected a prompt truncation warning.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunTotalBudgetCaseAsync(List<string> failures)
    {
        const string name = "conversational memory builder respects total budget";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateConversationFixture(tempRoot, interactionCount: 5, includeGenesis: true, promptLength: 24, answerSummaryLength: 26);

            var limits = new RckConversationalMemoryLimits(MaxInteractions: 8, MaxPromptChars: 120, MaxTotalChars: 110);
            var result = RckConversationalMemoryInputBuilder.Build(tempRoot, "Current prompt for continuity.", limits);

            Expect(result.Success, $"[{name}] expected success.", failures);
            if (!result.Success || result.Input is null)
            {
                return;
            }

            var totalChars = result.Input.RecentInteractions.Sum(interaction => interaction.Prompt.Length + interaction.AnswerSummary.Length);
            Expect(totalChars <= 110, $"[{name}] expected total prompt+summary chars <= 110 but got {totalChars}.", failures);
            Expect(result.Input.RecentInteractions.Count < 5, $"[{name}] expected at least one interaction to be cut by total budget.", failures);
            Expect(result.Warnings.Contains("total-budget-truncated"), $"[{name}] expected a total budget truncation warning.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunAnswerSummaryCaseAsync(List<string> failures)
    {
        const string name = "conversational memory builder uses answerSummary not full answer";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateConversationFixture(tempRoot, interactionCount: 2, includeGenesis: true, fullAnswerSuffix: "FULL-ANSWER-SHOULD-NOT-LEAK");

            var limits = new RckConversationalMemoryLimits(MaxInteractions: 8, MaxPromptChars: 120, MaxTotalChars: 2_000);
            var result = RckConversationalMemoryInputBuilder.Build(tempRoot, "Current prompt for continuity.", limits);

            Expect(result.Success, $"[{name}] expected success.", failures);
            if (!result.Success || result.Input is null)
            {
                return;
            }

            var json = JsonSerializer.Serialize(result.Input);
            Expect(!json.Contains("FULL-ANSWER-SHOULD-NOT-LEAK", StringComparison.Ordinal), $"[{name}] expected full answer text not to appear in the output.", failures);
            Expect(json.Contains("Answer summary 2", StringComparison.Ordinal), $"[{name}] expected the summary text to appear in the output.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunUnsafeFieldLeakageCaseAsync(List<string> failures)
    {
        const string name = "conversational memory builder excludes unsafe fields";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateConversationFixture(tempRoot, interactionCount: 2, includeGenesis: true);

            var limits = new RckConversationalMemoryLimits(MaxInteractions: 8, MaxPromptChars: 120, MaxTotalChars: 2_000);
            var result = RckConversationalMemoryInputBuilder.Build(tempRoot, "Current prompt for continuity.", limits);

            Expect(result.Success, $"[{name}] expected success.", failures);
            if (!result.Success || result.Input is null)
            {
                return;
            }

            var json = JsonSerializer.Serialize(result.Input);
            foreach (var fragment in new[] { "payloadCanonicalJson", "diff --git", "stdout", "stderr", "message_update", "message_end", "assistantMessageEvent", "selectedStateIds", "selectedDeltaIds", "selectedAnchorIds" })
            {
                Expect(!json.Contains(fragment, StringComparison.OrdinalIgnoreCase), $"[{name}] expected no '{fragment}' leakage.", failures);
            }
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunEmptyHistoryCaseAsync(List<string> failures)
    {
        const string name = "conversational memory builder handles empty history";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateGenesisOnlyFixture(tempRoot);

            var limits = new RckConversationalMemoryLimits(MaxInteractions: 8, MaxPromptChars: 120, MaxTotalChars: 2_000);
            var result = RckConversationalMemoryInputBuilder.Build(tempRoot, "Current prompt for continuity.", limits);

            Expect(result.Success, $"[{name}] expected success.", failures);
            if (!result.Success || result.Input is null)
            {
                return;
            }

            Expect(result.Input.RecentInteractions.Count == 0, $"[{name}] expected no recent interactions.", failures);
            Expect(result.Warnings.Contains("no-recent-interactions"), $"[{name}] expected a no-recent-interactions warning.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunDeterministicOrderingCaseAsync(List<string> failures)
    {
        const string name = "conversational memory builder deterministic ordering";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateConversationFixture(tempRoot, interactionCount: 4, includeGenesis: true);

            var limits = new RckConversationalMemoryLimits(MaxInteractions: 8, MaxPromptChars: 120, MaxTotalChars: 2_000);
            var resultA = RckConversationalMemoryInputBuilder.Build(tempRoot, "Current prompt for continuity.", limits);
            var resultB = RckConversationalMemoryInputBuilder.Build(tempRoot, "Current prompt for continuity.", limits);

            Expect(resultA.Success && resultB.Success, $"[{name}] expected repeated success.", failures);
            if (!resultA.Success || !resultB.Success || resultA.Input is null || resultB.Input is null)
            {
                return;
            }

            Expect(resultA.Input.RecentInteractions.Select(interaction => interaction.StateId).SequenceEqual(resultB.Input.RecentInteractions.Select(interaction => interaction.StateId)), $"[{name}] expected deterministic interaction ordering.", failures);
            Expect(resultA.Input.RecentInteractions.Select(interaction => interaction.DeltaId).SequenceEqual(resultB.Input.RecentInteractions.Select(interaction => interaction.DeltaId)), $"[{name}] expected deterministic delta ordering.", failures);
            Expect(resultA.Warnings.SequenceEqual(resultB.Warnings), $"[{name}] expected deterministic warnings.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void CreateGenesisOnlyFixture(string tempRoot)
    {
        CreateConversationFixture(tempRoot, interactionCount: 0, includeGenesis: true);
    }

    private static void CreateConversationFixture(
        string tempRoot,
        int interactionCount,
        bool includeGenesis,
        int promptLength = 18,
        int answerSummaryLength = 20,
        string fullAnswerSuffix = "FULL-ANSWER")
    {
        var rckRoot = Path.Combine(tempRoot, ".rfs", "rck");
        var statesRoot = Path.Combine(rckRoot, "states");
        var deltasRoot = Path.Combine(rckRoot, "deltas");
        Directory.CreateDirectory(statesRoot);
        Directory.CreateDirectory(deltasRoot);

        var previousStateId = includeGenesis ? "state-genesis" : $"state-{Math.Max(interactionCount - 1, 0)}";
        if (includeGenesis)
        {
            WriteState(statesRoot, "state-genesis", "rufus.initial-state", null, null, null, null, "fixture", "genesis", "fixture");
        }

        for (var index = 1; index <= interactionCount; index++)
        {
            var stateId = $"state-{index}";
            var deltaId = $"delta-{index}";
            var prompt = new string((char)('A' + ((index - 1) % 26)), promptLength);
            var answerSummary = $"Answer summary {index}";
            if (answerSummaryLength > answerSummary.Length)
            {
                answerSummary = answerSummary.PadRight(answerSummaryLength, (char)('0' + ((index - 1) % 10)));
            }
            else if (answerSummaryLength > 0)
            {
                answerSummary = answerSummary[..Math.Min(answerSummary.Length, answerSummaryLength)];
            }

            var fullAnswer = $"{answerSummary} :: {fullAnswerSuffix}::{index}";
            WriteState(statesRoot, stateId, "rufus.interaction-state", "tui-complete", prompt, fullAnswer, answerSummary, "github-copilot", "gpt-5.4-mini", index);
            WriteDelta(deltasRoot, deltaId, previousStateId, stateId, "tui-complete", prompt, answerSummary, "github-copilot", "gpt-5.4-mini", index);
            previousStateId = stateId;
        }

        var headStateId = interactionCount > 0 ? $"state-{interactionCount}" : "state-genesis";
        File.WriteAllText(Path.Combine(rckRoot, "HEAD"), headStateId + Environment.NewLine);
    }

    private static void WriteState(
        string statesRoot,
        string stateId,
        string type,
        string? mode,
        string? prompt,
        string? answer,
        string? answerSummary,
        string? provider,
        string? model,
        object? marker)
    {
        var payload = new Dictionary<string, object?>
        {
            ["type"] = type,
            ["schemaVersion"] = 1,
            ["interaction"] = new Dictionary<string, object?>
            {
                ["mode"] = mode,
                ["prompt"] = prompt,
                ["answer"] = answer,
                ["answerSummary"] = answerSummary,
                ["provider"] = provider,
                ["model"] = model,
                ["pipelineSummary"] = new Dictionary<string, object?>
                {
                    ["kind"] = marker is int index ? "complete" : "genesis",
                    ["usesRckContext"] = true,
                    ["usesTraceSlice"] = true,
                    ["usesContextPack"] = true,
                    ["validationStatus"] = null,
                },
            },
            ["git"] = new Dictionary<string, object?>
            {
                ["branch"] = "feature/rufus-cli-design",
                ["commit"] = "abcdef1234567890",
                ["dirty"] = false,
            },
            ["artifacts"] = Array.Empty<object>(),
        };

        var envelope = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["type"] = "rufus.rck.state",
            ["id"] = stateId,
            ["payloadCanonicalJson"] = JsonSerializer.Serialize(payload),
            ["refs"] = Array.Empty<object>(),
            ["meta"] = new Dictionary<string, object?>
            {
                ["createdAtUtc"] = BuildCreatedAt(marker),
                ["CreatedBy"] = "fixture",
                ["Label"] = stateId,
                ["Reason"] = "conversational memory fixture",
            },
        };

        File.WriteAllText(Path.Combine(statesRoot, $"{stateId}.json"), JsonSerializer.Serialize(envelope));
    }

    private static void WriteDelta(
        string deltasRoot,
        string deltaId,
        string fromStateId,
        string toStateId,
        string mode,
        string prompt,
        string answerSummary,
        string? provider,
        string? model,
        object? marker)
    {
        var value = new Dictionary<string, object?>
        {
            ["type"] = "rufus.interaction-delta",
            ["schemaVersion"] = 1,
            ["change"] = new Dictionary<string, object?>
            {
                ["summary"] = "Recorded a new LLM interaction.",
                ["fromStateId"] = fromStateId,
                ["toStateId"] = toStateId,
                ["changes"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["path"] = "/interaction",
                        ["kind"] = "updated",
                        ["summary"] = "Recorded a new LLM interaction.",
                    },
                },
            },
            ["cause"] = new Dictionary<string, object?>
            {
                ["type"] = "llm-interaction",
                ["mode"] = mode,
                ["prompt"] = prompt,
                ["answer"] = answerSummary,
                ["provider"] = provider,
                ["model"] = model,
                ["pipelineSummary"] = new Dictionary<string, object?>
                {
                    ["kind"] = marker is int index ? "complete" : "genesis",
                    ["usesRckContext"] = true,
                    ["usesTraceSlice"] = true,
                    ["usesContextPack"] = true,
                    ["validationStatus"] = null,
                },
            },
            ["evidence"] = new Dictionary<string, object?>
            {
                ["tools"] = Array.Empty<object>(),
                ["artifacts"] = Array.Empty<object>(),
            },
        };

        var envelope = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["type"] = "rufus.rck.delta",
            ["id"] = deltaId,
            ["fromStateId"] = fromStateId,
            ["toStateId"] = toStateId,
            ["ops"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["kind"] = "replace",
                    ["path"] = "/interaction",
                    ["valueJson"] = JsonSerializer.Serialize(value),
                },
            },
            ["refs"] = Array.Empty<object>(),
            ["evidenceRefs"] = Array.Empty<object>(),
            ["meta"] = new Dictionary<string, object?>
            {
                ["createdAtUtc"] = BuildCreatedAt(marker),
                ["CreatedBy"] = "fixture",
                ["Label"] = deltaId,
                ["Reason"] = "conversational memory fixture",
            },
        };

        File.WriteAllText(Path.Combine(deltasRoot, $"{deltaId}.json"), JsonSerializer.Serialize(envelope));
    }

    private static string BuildCreatedAt(object? marker)
        => marker is int index
            ? new DateTimeOffset(2026, 1, index, 0, 0, 0, TimeSpan.Zero).ToString("O")
            : new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).ToString("O");

    private static string CreateTempRoot(string name)
    {
        var safeName = new string(name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        var path = Path.Combine(Path.GetTempPath(), $"rck-conversational-memory-{safeName}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
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
        }
    }

    private static void Expect(bool condition, string message, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }
}
