using System.Text.Json;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.ParserChecks;

internal static class RckDagQuickIndexV1BuilderChecks
{
    public static async Task RunAsync(List<string> failures)
    {
        await RunBuilderHappyPathAsync(failures);
        await RunBuilderNoAnchorsAsync(failures);
        await RunBuilderDeterministicOrderingAsync(failures);
        await RunBuilderNoPayloadLeakageAsync(failures);
    }

    private static async Task RunBuilderHappyPathAsync(List<string> failures)
    {
        const string name = "dag quick index v1 builder happy path";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateDagFixture(tempRoot);

            var result = RckDagQuickIndexV1Builder.Build(tempRoot);
            Expect(result.Success, $"[{name}] expected success.", failures);
            if (!result.Success || result.DagQuickIndex is null)
            {
                return;
            }

            Expect(result.DagQuickIndex.HeadStateId == "state-head", $"[{name}] expected headStateId=state-head.", failures);
            Expect(result.DagQuickIndex.RecentStateIds.Count > 0, $"[{name}] expected recentStateIds.", failures);
            Expect(result.DagQuickIndex.RecentDeltaIds.Count > 0, $"[{name}] expected recentDeltaIds.", failures);
            Expect(result.DagQuickIndex.Anchors.Count == 2, $"[{name}] expected two anchors.", failures);
            Expect(result.DagQuickIndex.States.Count == 3, $"[{name}] expected three states.", failures);
            Expect(result.DagQuickIndex.Deltas.Count == 2, $"[{name}] expected two deltas.", failures);

            var anchor = result.DagQuickIndex.Anchors.Single();
            Expect(anchor.StateId == "state-head", $"[{name}] expected anchor stateId=state-head.", failures);
            Expect(anchor.IncomingDeltaIds.SequenceEqual(new[] { "delta-main" }), $"[{name}] expected anchor incoming deltas.", failures);
            Expect(anchor.OutgoingDeltaIds.SequenceEqual(new[] { "delta-back" }), $"[{name}] expected anchor outgoing deltas.", failures);
            Expect(anchor.IsRecentChain, $"[{name}] expected anchor to be marked recent chain.", failures);
            Expect(anchor.ParentAnchorIds.Count == 0, $"[{name}] expected empty parentAnchorIds.", failures);
            Expect(anchor.DistanceToHead == 0, $"[{name}] expected anchor distance 0.", failures);

            var stateHead = result.DagQuickIndex.States.Single(state => state.Id == "state-head");
            Expect(stateHead.AttachedAnchorIds.SequenceEqual(new[] { "anchor-head" }), $"[{name}] expected attached anchor ids.", failures);
            Expect(stateHead.IncomingDeltaIds.SequenceEqual(new[] { "delta-main" }), $"[{name}] expected incoming delta ids.", failures);
            Expect(stateHead.OutgoingDeltaIds.SequenceEqual(new[] { "delta-back" }), $"[{name}] expected outgoing delta ids.", failures);
            Expect(stateHead.DistanceToHead == 0, $"[{name}] expected head distance 0.", failures);

            var deltaMain = result.DagQuickIndex.Deltas.Single(delta => delta.Id == "delta-main");
            Expect(deltaMain.FromStateId == "state-base", $"[{name}] expected delta-main fromStateId=state-base.", failures);
            Expect(deltaMain.ToStateId == "state-head", $"[{name}] expected delta-main toStateId=state-head.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunBuilderNoAnchorsAsync(List<string> failures)
    {
        const string name = "dag quick index v1 builder no anchors";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateDagFixture(tempRoot, includeAnchors: false);

            var result = RckDagQuickIndexV1Builder.Build(tempRoot);
            Expect(result.Success, $"[{name}] expected success.", failures);
            if (!result.Success || result.DagQuickIndex is null)
            {
                return;
            }

            Expect(result.DagQuickIndex.Anchors.Count == 0, $"[{name}] expected no anchors.", failures);
            Expect(result.DagQuickIndex.States.Count == 3, $"[{name}] expected states to still be populated.", failures);
            Expect(result.DagQuickIndex.Deltas.Count == 2, $"[{name}] expected deltas to still be populated.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunBuilderDeterministicOrderingAsync(List<string> failures)
    {
        const string name = "dag quick index v1 builder deterministic ordering";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateDagFixture(tempRoot);

            var resultA = RckDagQuickIndexV1Builder.Build(tempRoot);
            var resultB = RckDagQuickIndexV1Builder.Build(tempRoot);

            Expect(resultA.Success && resultB.Success, $"[{name}] expected repeated success.", failures);
            if (!resultA.Success || !resultB.Success || resultA.DagQuickIndex is null || resultB.DagQuickIndex is null)
            {
                return;
            }

            Expect(resultA.DagQuickIndex.Anchors.Select(anchor => anchor.Id).SequenceEqual(resultB.DagQuickIndex.Anchors.Select(anchor => anchor.Id)), $"[{name}] expected deterministic anchor order.", failures);
            Expect(resultA.DagQuickIndex.States.Select(state => state.Id).SequenceEqual(resultB.DagQuickIndex.States.Select(state => state.Id)), $"[{name}] expected deterministic state order.", failures);
            Expect(resultA.DagQuickIndex.Deltas.Select(delta => delta.Id).SequenceEqual(resultB.DagQuickIndex.Deltas.Select(delta => delta.Id)), $"[{name}] expected deterministic delta order.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunBuilderNoPayloadLeakageAsync(List<string> failures)
    {
        const string name = "dag quick index v1 builder no payload leakage";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateDagFixture(tempRoot);

            var result = RckDagQuickIndexV1Builder.Build(tempRoot);
            Expect(result.Success, $"[{name}] expected success.", failures);
            if (!result.Success || result.DagQuickIndex is null)
            {
                return;
            }

            var json = JsonSerializer.Serialize(result.DagQuickIndex);
            foreach (var fragment in new[] { "payloadCanonicalJson", "stdout", "stderr", "diff --git", "valueJson", "jsonl" })
            {
                Expect(!json.Contains(fragment, StringComparison.OrdinalIgnoreCase), $"[{name}] expected no '{fragment}' leakage.", failures);
            }
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void CreateDagFixture(string tempRoot, bool includeAnchors = true)
    {
        var rfsRoot = Path.Combine(tempRoot, ".rfs");
        var rckRoot = Path.Combine(rfsRoot, "rck");
        var statesRoot = Path.Combine(rckRoot, "states");
        var deltasRoot = Path.Combine(rckRoot, "deltas");
        var anchorsRoot = Path.Combine(rckRoot, "anchors");

        Directory.CreateDirectory(statesRoot);
        Directory.CreateDirectory(deltasRoot);
        Directory.CreateDirectory(anchorsRoot);
        File.WriteAllText(Path.Combine(rckRoot, "HEAD"), "state-head" + Environment.NewLine);

        static string StatePayload(string label) => JsonSerializer.Serialize(new { type = "fixture.state", label });

        File.WriteAllText(
            Path.Combine(statesRoot, "state-base.json"),
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["type"] = "rufus.rck.state",
                ["id"] = "state-base",
                ["payloadCanonicalJson"] = StatePayload("base"),
                ["refs"] = Array.Empty<object>(),
                ["meta"] = new Dictionary<string, object?>
                {
                    ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                    ["CreatedBy"] = "fixture",
                    ["Label"] = "base",
                    ["Reason"] = "dag quick index fixture",
                },
            }));

        File.WriteAllText(
            Path.Combine(statesRoot, "state-mid.json"),
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["type"] = "rufus.rck.state",
                ["id"] = "state-mid",
                ["payloadCanonicalJson"] = StatePayload("mid"),
                ["refs"] = Array.Empty<object>(),
                ["meta"] = new Dictionary<string, object?>
                {
                    ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                    ["CreatedBy"] = "fixture",
                    ["Label"] = "mid",
                    ["Reason"] = "dag quick index fixture",
                },
            }));

        File.WriteAllText(
            Path.Combine(statesRoot, "state-head.json"),
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["type"] = "rufus.rck.state",
                ["id"] = "state-head",
                ["payloadCanonicalJson"] = StatePayload("head"),
                ["refs"] = Array.Empty<object>(),
                ["meta"] = new Dictionary<string, object?>
                {
                    ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                    ["CreatedBy"] = "fixture",
                    ["Label"] = "head",
                    ["Reason"] = "dag quick index fixture",
                },
            }));

        File.WriteAllText(
            Path.Combine(deltasRoot, "delta-main.json"),
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["type"] = "rufus.rck.delta",
                ["id"] = "delta-main",
                ["fromStateId"] = "state-mid",
                ["toStateId"] = "state-head",
                ["ops"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["kind"] = "replace",
                        ["path"] = "notes/selected.md",
                        ["valueJson"] = JsonSerializer.Serialize(new { text = "selected" }),
                    },
                },
                ["refs"] = Array.Empty<object>(),
                ["evidenceRefs"] = Array.Empty<object>(),
                ["meta"] = new Dictionary<string, object?>
                {
                    ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                    ["CreatedBy"] = "fixture",
                    ["Label"] = "delta main",
                    ["Reason"] = "dag quick index fixture",
                },
            }));

        File.WriteAllText(
            Path.Combine(deltasRoot, "delta-back.json"),
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["type"] = "rufus.rck.delta",
                ["id"] = "delta-back",
                ["fromStateId"] = "state-head",
                ["toStateId"] = "state-mid",
                ["ops"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["kind"] = "replace",
                        ["path"] = "notes/back.md",
                        ["valueJson"] = JsonSerializer.Serialize(new { text = "back" }),
                    },
                },
                ["refs"] = Array.Empty<object>(),
                ["evidenceRefs"] = Array.Empty<object>(),
                ["meta"] = new Dictionary<string, object?>
                {
                    ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                    ["CreatedBy"] = "fixture",
                    ["Label"] = "delta back",
                    ["Reason"] = "dag quick index fixture",
                },
            }));

        if (includeAnchors)
        {
            File.WriteAllText(
                Path.Combine(anchorsRoot, "anchor-mid.json"),
                JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["schemaVersion"] = 1,
                    ["type"] = "rufus.rck.anchor",
                    ["id"] = "anchor-mid",
                    ["stateId"] = "state-mid",
                    ["parentAnchorIds"] = Array.Empty<object>(),
                    ["meta"] = new Dictionary<string, object?>
                    {
                        ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                        ["CreatedBy"] = "fixture",
                        ["Label"] = "anchor mid",
                        ["Reason"] = "dag quick index fixture",
                    },
                }));

            File.WriteAllText(
                Path.Combine(anchorsRoot, "anchor-head.json"),
                JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["schemaVersion"] = 1,
                    ["type"] = "rufus.rck.anchor",
                    ["id"] = "anchor-head",
                    ["stateId"] = "state-head",
                    ["parentAnchorIds"] = new[] { "anchor-mid" },
                    ["meta"] = new Dictionary<string, object?>
                    {
                        ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                        ["CreatedBy"] = "fixture",
                        ["Label"] = "anchor head",
                        ["Reason"] = "dag quick index fixture",
                    },
                }));
        }
    }

    private static string CreateTempRoot(string prefix)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void Expect(bool condition, string failure, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(failure);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
