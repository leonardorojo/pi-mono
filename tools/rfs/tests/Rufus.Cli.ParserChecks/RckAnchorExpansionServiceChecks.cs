using System.Text.Json;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.ParserChecks;

internal static class RckAnchorExpansionServiceChecks
{
    public static Task RunAsync(List<string> failures)
    {
        RunExpandsValidAnchorAsync(failures);
        RunRespectsMaxStatesAsync(failures);
        RunRespectsMaxDeltasAsync(failures);
        RunMissingAnchorFallbackAsync(failures);
        RunMissingAnchorStateFallbackAsync(failures);
        RunNoAnchorsFallbackAsync(failures);
        RunPartialAnchorGuidedAsync(failures);
        RunDeterministicAsync(failures);
        RunNoLeakageAsync(failures);
        return Task.CompletedTask;
    }

    private static void RunExpandsValidAnchorAsync(List<string> failures)
    {
        const string name = "anchor expansion expands valid anchor";
        var index = CreateQuickIndex(includeParentLineage: true);
        var result = RckAnchorExpansionService.Expand(new RckAnchorExpansionRequest(
            SelectedAnchorIds: new[] { "anchor-a" },
            QuickIndex: index,
            MaxStates: 10,
            MaxDeltas: 10,
            Policy: new RckAnchorExpansionPolicy(
                IncludeIncomingDeltas: true,
                IncludeOutgoingDeltas: true,
                IncludeNeighborStates: true,
                FallbackToRecentChain: true,
                IncludeParentAnchorLineage: true)));

        Expect(result.Success, $"[{name}] expected success.", failures);
        Expect(result.Strategy == "anchor-guided", $"[{name}] expected anchor-guided strategy.", failures);
        Expect(result.AnchorIds.SequenceEqual(new[] { "anchor-a", "anchor-parent" }), $"[{name}] expected anchor lineage.", failures);
        Expect(result.StateIds.SequenceEqual(new[] { "state-2", "state-1", "state-3" }), $"[{name}] expected structured state expansion.", failures);
        Expect(result.DeltaIds.SequenceEqual(new[] { "delta-1", "delta-2" }), $"[{name}] expected structured delta expansion.", failures);
        Expect(result.Warnings.Count == 0, $"[{name}] expected no warnings.", failures);
    }

    private static void RunRespectsMaxStatesAsync(List<string> failures)
    {
        const string name = "anchor expansion respects maxStates";
        var index = CreateQuickIndex();
        var result = RckAnchorExpansionService.Expand(new RckAnchorExpansionRequest(
            SelectedAnchorIds: new[] { "anchor-a" },
            QuickIndex: index,
            MaxStates: 2,
            MaxDeltas: 10,
            Policy: new RckAnchorExpansionPolicy()));

        Expect(result.Success, $"[{name}] expected success.", failures);
        Expect(result.StateIds.Count == 2, $"[{name}] expected truncation to two states.", failures);
        Expect(result.Warnings.Any(w => w.Contains("maxStates", StringComparison.OrdinalIgnoreCase)), $"[{name}] expected maxStates warning.", failures);
        Expect(result.ExpansionEvidence.Any(e => e.Kind == "truncated-max-states"), $"[{name}] expected truncation evidence.", failures);
    }

    private static void RunRespectsMaxDeltasAsync(List<string> failures)
    {
        const string name = "anchor expansion respects maxDeltas";
        var index = CreateQuickIndex();
        var result = RckAnchorExpansionService.Expand(new RckAnchorExpansionRequest(
            SelectedAnchorIds: new[] { "anchor-a" },
            QuickIndex: index,
            MaxStates: 10,
            MaxDeltas: 1,
            Policy: new RckAnchorExpansionPolicy()));

        Expect(result.Success, $"[{name}] expected success.", failures);
        Expect(result.DeltaIds.Count == 1, $"[{name}] expected truncation to one delta.", failures);
        Expect(result.Warnings.Any(w => w.Contains("maxDeltas", StringComparison.OrdinalIgnoreCase)), $"[{name}] expected maxDeltas warning.", failures);
        Expect(result.ExpansionEvidence.Any(e => e.Kind == "truncated-max-deltas"), $"[{name}] expected truncation evidence.", failures);
    }

    private static void RunMissingAnchorFallbackAsync(List<string> failures)
    {
        const string name = "anchor expansion missing anchor fallback";
        var index = CreateQuickIndex();
        var result = RckAnchorExpansionService.Expand(new RckAnchorExpansionRequest(
            SelectedAnchorIds: new[] { "missing-anchor" },
            QuickIndex: index,
            MaxStates: 10,
            MaxDeltas: 10,
            Policy: new RckAnchorExpansionPolicy(FallbackToRecentChain: true)));

        Expect(result.Success, $"[{name}] expected success.", failures);
        Expect(result.Strategy == "recent-chain-fallback", $"[{name}] expected recent-chain-fallback strategy.", failures);
        Expect(result.Warnings.Any(w => w.Contains("missing anchor", StringComparison.OrdinalIgnoreCase)), $"[{name}] expected missing anchor warning.", failures);
        Expect(result.StateIds.SequenceEqual(new[] { "state-3", "state-2" }), $"[{name}] expected recent chain states.", failures);
        Expect(result.DeltaIds.SequenceEqual(new[] { "delta-2", "delta-1" }), $"[{name}] expected recent chain deltas.", failures);
    }

    private static void RunMissingAnchorStateFallbackAsync(List<string> failures)
    {
        const string name = "anchor expansion missing anchor state fallback";
        var index = CreateQuickIndex(includeBrokenAnchor: true);
        var result = RckAnchorExpansionService.Expand(new RckAnchorExpansionRequest(
            SelectedAnchorIds: new[] { "anchor-broken" },
            QuickIndex: index,
            MaxStates: 10,
            MaxDeltas: 10,
            Policy: new RckAnchorExpansionPolicy(FallbackToRecentChain: true)));

        Expect(result.Success, $"[{name}] expected success.", failures);
        Expect(result.Strategy == "recent-chain-fallback", $"[{name}] expected recent-chain-fallback strategy.", failures);
        Expect(result.Warnings.Any(w => w.Contains("missing state", StringComparison.OrdinalIgnoreCase)), $"[{name}] expected missing anchor state warning.", failures);
        Expect(result.AnchorIds.SequenceEqual(new[] { "anchor-broken" }), $"[{name}] expected anchor id retained for traceability.", failures);
    }

    private static void RunNoAnchorsFallbackAsync(List<string> failures)
    {
        const string name = "anchor expansion no anchors fallback";
        var index = CreateQuickIndex();
        var result = RckAnchorExpansionService.Expand(new RckAnchorExpansionRequest(
            SelectedAnchorIds: Array.Empty<string>(),
            QuickIndex: index,
            MaxStates: 10,
            MaxDeltas: 10,
            Policy: new RckAnchorExpansionPolicy(FallbackToRecentChain: true)));

        Expect(result.Success, $"[{name}] expected success.", failures);
        Expect(result.Strategy == "recent-chain-fallback", $"[{name}] expected recent-chain-fallback strategy.", failures);
        Expect(result.AnchorIds.Count == 0, $"[{name}] expected empty anchor set.", failures);
        Expect(result.StateIds.Count > 0, $"[{name}] expected fallback states.", failures);
        Expect(result.Warnings.Any(w => w.Contains("recent chain fallback", StringComparison.OrdinalIgnoreCase)), $"[{name}] expected fallback warning.", failures);
    }

    private static void RunPartialAnchorGuidedAsync(List<string> failures)
    {
        const string name = "anchor expansion partial anchor guided";
        var index = CreateQuickIndex();
        var result = RckAnchorExpansionService.Expand(new RckAnchorExpansionRequest(
            SelectedAnchorIds: new[] { "anchor-a", "missing-anchor" },
            QuickIndex: index,
            MaxStates: 10,
            MaxDeltas: 10,
            Policy: new RckAnchorExpansionPolicy()));

        Expect(result.Success, $"[{name}] expected success.", failures);
        Expect(result.Strategy == "partial-anchor-guided", $"[{name}] expected partial-anchor-guided strategy.", failures);
        Expect(result.AnchorIds.Contains("anchor-a", StringComparer.Ordinal), $"[{name}] expected valid anchor retained.", failures);
        Expect(result.Warnings.Any(w => w.Contains("missing anchor", StringComparison.OrdinalIgnoreCase)), $"[{name}] expected missing anchor warning.", failures);
    }

    private static void RunDeterministicAsync(List<string> failures)
    {
        const string name = "anchor expansion deterministic";
        var index = CreateQuickIndex(includeParentLineage: true);
        var request = new RckAnchorExpansionRequest(
            SelectedAnchorIds: new[] { "anchor-a" },
            QuickIndex: index,
            MaxStates: 10,
            MaxDeltas: 10,
            Policy: new RckAnchorExpansionPolicy(
                IncludeIncomingDeltas: true,
                IncludeOutgoingDeltas: true,
                IncludeNeighborStates: true,
                FallbackToRecentChain: true,
                IncludeParentAnchorLineage: true));

        var first = RckAnchorExpansionService.Expand(request);
        var second = RckAnchorExpansionService.Expand(request);

        Expect(first.Success && second.Success, $"[{name}] expected repeated success.", failures);
        Expect(first.Strategy == second.Strategy, $"[{name}] expected identical strategy.", failures);
        Expect(first.AnchorIds.SequenceEqual(second.AnchorIds), $"[{name}] expected deterministic anchor order.", failures);
        Expect(first.StateIds.SequenceEqual(second.StateIds), $"[{name}] expected deterministic state order.", failures);
        Expect(first.DeltaIds.SequenceEqual(second.DeltaIds), $"[{name}] expected deterministic delta order.", failures);
        Expect(first.Warnings.SequenceEqual(second.Warnings), $"[{name}] expected deterministic warnings.", failures);
        Expect(first.ExpansionEvidence.Select(e => (e.Kind, e.SourceId, e.TargetId, e.Reason)).SequenceEqual(second.ExpansionEvidence.Select(e => (e.Kind, e.SourceId, e.TargetId, e.Reason))), $"[{name}] expected deterministic evidence.", failures);
    }

    private static void RunNoLeakageAsync(List<string> failures)
    {
        const string name = "anchor expansion no leakage";
        var index = CreateQuickIndex(includeParentLineage: true, includeBrokenAnchor: true);
        var result = RckAnchorExpansionService.Expand(new RckAnchorExpansionRequest(
            SelectedAnchorIds: new[] { "anchor-a", "missing-anchor", "anchor-broken" },
            QuickIndex: index,
            MaxStates: 10,
            MaxDeltas: 10,
            Policy: new RckAnchorExpansionPolicy(
                IncludeIncomingDeltas: true,
                IncludeOutgoingDeltas: true,
                IncludeNeighborStates: true,
                FallbackToRecentChain: true,
                IncludeParentAnchorLineage: true)));

        var json = JsonSerializer.Serialize(result);
        foreach (var fragment in new[]
                 {
                     "payloadCanonicalJson",
                     "diff --git",
                     "stdout",
                     "stderr",
                     "message_update",
                     "message_end",
                     "assistantMessageEvent",
                     "```",
                 })
        {
            Expect(!json.Contains(fragment, StringComparison.OrdinalIgnoreCase), $"[{name}] expected no '{fragment}' leakage.", failures);
        }
    }

    private static RckDagQuickIndexV1 CreateQuickIndex(bool includeParentLineage = false, bool includeBrokenAnchor = false)
    {
        var anchors = new List<RckDagAnchorCandidate>
        {
            new("anchor-a", "state-2", "anchor A", "reason A", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), true, includeParentLineage ? new[] { "anchor-parent" } : Array.Empty<string>(), 1, new[] { "delta-1" }, new[] { "delta-2" }),
            new("anchor-parent", "state-1", "anchor parent", "reason parent", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), true, Array.Empty<string>(), 2, Array.Empty<string>(), new[] { "delta-1" }),
        };

        if (includeBrokenAnchor)
        {
            anchors.Add(new RckDagAnchorCandidate("anchor-broken", "state-missing", "broken", "broken reason", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), false, Array.Empty<string>(), null, Array.Empty<string>(), Array.Empty<string>()));
        }

        return new RckDagQuickIndexV1(
            HeadStateId: "state-3",
            RecentStateIds: new[] { "state-3", "state-2", "state-1" },
            RecentDeltaIds: new[] { "delta-2", "delta-1" },
            Anchors: anchors,
            States: new[]
            {
                new RckDagStateCandidate("state-1", "state-1", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), Array.Empty<string>(), Array.Empty<string>(), new[] { "delta-1" }, 2, null, null, null),
                new RckDagStateCandidate("state-2", "state-2", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), new[] { "anchor-a" }, new[] { "delta-1" }, new[] { "delta-2" }, 1, null, null, null),
                new RckDagStateCandidate("state-3", "state-3", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), Array.Empty<string>(), new[] { "delta-2" }, Array.Empty<string>(), 0, null, null, null),
            },
            Deltas: new[]
            {
                new RckDagDeltaCandidate("delta-1", "state-1", "state-2", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), "replace:notes/1", "evidence 1"),
                new RckDagDeltaCandidate("delta-2", "state-2", "state-3", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"), "replace:notes/2", "evidence 2"),
            });
    }

    private static void Expect(bool condition, string failure, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(failure);
        }
    }
}
