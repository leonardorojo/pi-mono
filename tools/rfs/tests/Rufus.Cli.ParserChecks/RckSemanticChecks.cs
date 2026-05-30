using System.Text.Json;
using Rufus.RCK.Semantic;

namespace Rufus.Cli.ParserChecks;

internal static class RckSemanticChecks
{
    public static async Task RunAsync(List<string> failures)
    {
        await Task.Yield();
        RunSingleAnchor(failures);
        RunTwoAnchors(failures);
        RunOrderingByCreatedAtUtc(failures);
        RunStableIds(failures);
        RunTopicNormalization(failures);
        RunEmptyAnchors(failures);
        RunJsonStoreRoundtrip(failures);
        RunWorkspaceRebuild(failures);
        RunWorkspaceShowWithoutProjection(failures);
        RunWorkspaceShowWithProjection(failures);
    }

    private static void RunSingleAnchor(List<string> failures)
    {
        const string name = "rck semantic single anchor";
        var anchors = new List<RckSemanticAnchorInput>
        {
            new("anchor-1", "Initial setup", "state-1", new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero)),
        };

        var projection = RckSemanticProjectionBuilder.BuildFromAnchors(anchors);

        Expect(projection.SchemaVersion == 1, $"[{name}] expected schema version 1.", failures);
        Expect(projection.Nodes.Count == 1, $"[{name}] expected 1 node, got {projection.Nodes.Count}.", failures);
        Expect(projection.Deltas.Count == 0, $"[{name}] expected 0 deltas, got {projection.Deltas.Count}.", failures);

        var node = projection.Nodes[0];
        Expect(node.AnchorId == "anchor-1", $"[{name}] expected AnchorId=anchor-1, got {node.AnchorId}.", failures);
        Expect(node.StateId == "state-1", $"[{name}] expected StateId=state-1, got {node.StateId}.", failures);
        Expect(node.AnchorName == "Initial setup", $"[{name}] expected AnchorName='Initial setup'.", failures);
        Expect(node.Summary == "Initial setup", $"[{name}] expected Summary='Initial setup'.", failures);
        Expect(node.Id.Length == 16, $"[{name}] expected node id length 16.", failures);
    }

    private static void RunTwoAnchors(List<string> failures)
    {
        const string name = "rck semantic two anchors";
        var anchors = new List<RckSemanticAnchorInput>
        {
            new("anchor-a", "First anchor", "state-a", new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero)),
            new("anchor-b", "Second anchor", "state-b", new DateTimeOffset(2025, 6, 1, 13, 0, 0, TimeSpan.Zero)),
        };

        var projection = RckSemanticProjectionBuilder.BuildFromAnchors(anchors);

        Expect(projection.Nodes.Count == 2, $"[{name}] expected 2 nodes, got {projection.Nodes.Count}.", failures);
        Expect(projection.Deltas.Count == 1, $"[{name}] expected 1 delta, got {projection.Deltas.Count}.", failures);

        var node0 = projection.Nodes[0];
        var node1 = projection.Nodes[1];
        Expect(node0.AnchorId == "anchor-a", $"[{name}] node0 expected anchor-a.", failures);
        Expect(node1.AnchorId == "anchor-b", $"[{name}] node1 expected anchor-b.", failures);

        var delta = projection.Deltas[0];
        Expect(delta.FromAnchorId == "anchor-a", $"[{name}] delta FromAnchorId expected anchor-a.", failures);
        Expect(delta.ToAnchorId == "anchor-b", $"[{name}] delta ToAnchorId expected anchor-b.", failures);
        Expect(delta.FromNodeId == node0.Id, $"[{name}] delta FromNodeId must match node0.Id.", failures);
        Expect(delta.ToNodeId == node1.Id, $"[{name}] delta ToNodeId must match node1.Id.", failures);
        Expect(delta.SourceStateIds.Contains("state-a"), $"[{name}] SourceStateIds should contain state-a.", failures);
        Expect(delta.SourceStateIds.Contains("state-b"), $"[{name}] SourceStateIds should contain state-b.", failures);
        Expect(delta.SourceDeltaIds.Count == 0, $"[{name}] SourceDeltaIds should be empty in v0.", failures);
        Expect(delta.Id.Length == 16, $"[{name}] expected delta id length 16.", failures);
        Expect(delta.Summary.Contains("First anchor"), $"[{name}] delta summary should mention from anchor.", failures);
        Expect(delta.Summary.Contains("Second anchor"), $"[{name}] delta summary should mention to anchor.", failures);
    }

    private static void RunOrderingByCreatedAtUtc(List<string> failures)
    {
        const string name = "rck semantic ordering";
        var anchors = new List<RckSemanticAnchorInput>
        {
            new("anchor-c", "Third", "state-c", new DateTimeOffset(2025, 6, 1, 14, 0, 0, TimeSpan.Zero)),
            new("anchor-a", "First", "state-a", new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero)),
            new("anchor-b", "Second", "state-b", new DateTimeOffset(2025, 6, 1, 13, 0, 0, TimeSpan.Zero)),
        };

        var projection = RckSemanticProjectionBuilder.BuildFromAnchors(anchors);

        Expect(projection.Nodes.Count == 3, $"[{name}] expected 3 nodes.", failures);
        Expect(projection.Nodes[0].AnchorId == "anchor-a", $"[{name}] node0 should be anchor-a (earliest).", failures);
        Expect(projection.Nodes[1].AnchorId == "anchor-b", $"[{name}] node1 should be anchor-b.", failures);
        Expect(projection.Nodes[2].AnchorId == "anchor-c", $"[{name}] node2 should be anchor-c (latest).", failures);

        Expect(projection.Deltas.Count == 2, $"[{name}] expected 2 deltas.", failures);
        Expect(projection.Deltas[0].FromAnchorId == "anchor-a" && projection.Deltas[0].ToAnchorId == "anchor-b",
            $"[{name}] delta0 should be a->b.", failures);
        Expect(projection.Deltas[1].FromAnchorId == "anchor-b" && projection.Deltas[1].ToAnchorId == "anchor-c",
            $"[{name}] delta1 should be b->c.", failures);
    }

    private static void RunStableIds(List<string> failures)
    {
        const string name = "rck semantic stable ids";
        var anchors = new List<RckSemanticAnchorInput>
        {
            new("anchor-x", "X", "state-x", new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            new("anchor-y", "Y", "state-y", new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero)),
        };

        var p1 = RckSemanticProjectionBuilder.BuildFromAnchors(anchors);
        var p2 = RckSemanticProjectionBuilder.BuildFromAnchors(anchors);

        Expect(p1.Nodes.Count == p2.Nodes.Count, $"[{name}] node counts must match.", failures);
        Expect(p1.Deltas.Count == p2.Deltas.Count, $"[{name}] delta counts must match.", failures);

        for (var i = 0; i < p1.Nodes.Count; i++)
        {
            Expect(p1.Nodes[i].Id == p2.Nodes[i].Id,
                $"[{name}] node[{i}] id must be stable: {p1.Nodes[i].Id} vs {p2.Nodes[i].Id}.", failures);
        }

        for (var i = 0; i < p1.Deltas.Count; i++)
        {
            Expect(p1.Deltas[i].Id == p2.Deltas[i].Id,
                $"[{name}] delta[{i}] id must be stable: {p1.Deltas[i].Id} vs {p2.Deltas[i].Id}.", failures);
        }
    }

    private static void RunTopicNormalization(List<string> failures)
    {
        const string name = "rck semantic topic normalization";
        var anchors = new List<RckSemanticAnchorInput>
        {
            new("anchor-1", "Continuidad conversacional vs TraceSlice estructural", "state-1",
                new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        };

        var projection = RckSemanticProjectionBuilder.BuildFromAnchors(anchors);
        var topics = projection.Nodes[0].Topics;

        Expect(topics.Count > 0, $"[{name}] expected non-empty topics.", failures);

        // Check expected normalized tokens
        var expected = new[] { "continuidad", "conversacional", "vs", "traceslice", "estructural" };
        foreach (var token in expected)
        {
            Expect(topics.Contains(token, StringComparer.OrdinalIgnoreCase),
                $"[{name}] topics should contain '{token}'. Got: [{string.Join(", ", topics)}]", failures);
        }

        // All tokens should be lowercase
        foreach (var topic in topics)
        {
            Expect(topic == topic.ToLowerInvariant(),
                $"[{name}] topic '{topic}' should be lowercase.", failures);
        }
    }

    private static void RunEmptyAnchors(List<string> failures)
    {
        const string name = "rck semantic empty anchors";
        var anchors = Array.Empty<RckSemanticAnchorInput>();

        var projection = RckSemanticProjectionBuilder.BuildFromAnchors(anchors);

        Expect(projection.Nodes.Count == 0, $"[{name}] expected 0 nodes.", failures);
        Expect(projection.Deltas.Count == 0, $"[{name}] expected 0 deltas.", failures);
        Expect(projection.SchemaVersion == 1, $"[{name}] expected schema version 1.", failures);
    }

    private static void RunJsonStoreRoundtrip(List<string> failures)
    {
        const string name = "rck semantic json store roundtrip";
        var anchors = new List<RckSemanticAnchorInput>
        {
            new("anchor-1", "Setup", "state-1", new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            new("anchor-2", "Feature X", "state-2", new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero)),
        };

        var original = RckSemanticProjectionBuilder.BuildFromAnchors(anchors);
        var tempPath = Path.Combine(Path.GetTempPath(), $"rck-semantic-test-{Guid.NewGuid():N}.json");

        try
        {
            RckSemanticProjectionJsonStore.Write(tempPath, original);
            Expect(File.Exists(tempPath), $"[{name}] file should exist after write.", failures);

            var restored = RckSemanticProjectionJsonStore.Read(tempPath);

            Expect(restored.SchemaVersion == original.SchemaVersion,
                $"[{name}] schema version must match.", failures);
            Expect(restored.Nodes.Count == original.Nodes.Count,
                $"[{name}] node count must match after roundtrip.", failures);
            Expect(restored.Deltas.Count == original.Deltas.Count,
                $"[{name}] delta count must match after roundtrip.", failures);

            for (var i = 0; i < original.Nodes.Count; i++)
            {
                Expect(restored.Nodes[i].Id == original.Nodes[i].Id,
                    $"[{name}] node[{i}] Id must survive roundtrip.", failures);
                Expect(restored.Nodes[i].AnchorId == original.Nodes[i].AnchorId,
                    $"[{name}] node[{i}] AnchorId must survive roundtrip.", failures);
                Expect(restored.Nodes[i].Topics.Count == original.Nodes[i].Topics.Count,
                    $"[{name}] node[{i}] topic count must survive roundtrip.", failures);
            }

            for (var i = 0; i < original.Deltas.Count; i++)
            {
                Expect(restored.Deltas[i].Id == original.Deltas[i].Id,
                    $"[{name}] delta[{i}] Id must survive roundtrip.", failures);
                Expect(restored.Deltas[i].FromAnchorId == original.Deltas[i].FromAnchorId,
                    $"[{name}] delta[{i}] FromAnchorId must survive roundtrip.", failures);
                Expect(restored.Deltas[i].ToAnchorId == original.Deltas[i].ToAnchorId,
                    $"[{name}] delta[{i}] ToAnchorId must survive roundtrip.", failures);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static void RunWorkspaceRebuild(List<string> failures)
    {
        const string name = "rck semantic workspace rebuild";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateWorkspaceFixture(tempRoot, anchorCount: 2);

            var result = RckSemanticWorkspaceAdapter.RebuildProjection(tempRoot);

            Expect(result.Success, $"[{name}] rebuild should succeed.", failures);
            Expect(result.NodeCount == 2, $"[{name}] expected 2 nodes, got {result.NodeCount}.", failures);
            Expect(result.DeltaCount == 1, $"[{name}] expected 1 delta, got {result.DeltaCount}.", failures);
            Expect(!string.IsNullOrWhiteSpace(result.OutputPath), $"[{name}] output path should not be empty.", failures);

            var projectionPath = Path.Combine(tempRoot, ".rfs", "semantic", "projection.json");
            Expect(File.Exists(projectionPath), $"[{name}] projection.json should exist.", failures);

            // Verify .rfs/rck was NOT modified (no new files created there)
            var rckStatesPath = Path.Combine(tempRoot, ".rfs", "rck", "states");
            var stateFiles = Directory.GetFiles(rckStatesPath, "*.json");
            Expect(stateFiles.Length == 1, $"[{name}] states should remain at 1, got {stateFiles.Length}.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void RunWorkspaceShowWithoutProjection(List<string> failures)
    {
        const string name = "rck semantic show without projection";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateWorkspaceFixture(tempRoot, anchorCount: 2);
            // Don't rebuild — so no projection exists

            var projection = RckSemanticWorkspaceAdapter.TryReadProjection(tempRoot);
            Expect(projection is null, $"[{name}] should return null when no projection exists.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void RunWorkspaceShowWithProjection(List<string> failures)
    {
        const string name = "rck semantic show with projection";
        var tempRoot = CreateTempRoot(name);
        try
        {
            CreateWorkspaceFixture(tempRoot, anchorCount: 3);

            var rebuildResult = RckSemanticWorkspaceAdapter.RebuildProjection(tempRoot);
            Expect(rebuildResult.Success, $"[{name}] rebuild prerequisite failed.", failures);
            if (!rebuildResult.Success) return;

            var projection = RckSemanticWorkspaceAdapter.TryReadProjection(tempRoot);
            Expect(projection is not null, $"[{name}] should return projection after rebuild.", failures);
            if (projection is null) return;

            Expect(projection.Nodes.Count == 3, $"[{name}] expected 3 nodes.", failures);
            Expect(projection.Deltas.Count == 2, $"[{name}] expected 2 deltas.", failures);
            Expect(projection.SchemaVersion == 1, $"[{name}] expected schema version 1.", failures);

            // Check anchor names are present
            var names = projection.Nodes.Select(n => n.AnchorName).ToArray();
            Expect(names.Contains("Anchor 1"), $"[{name}] should contain 'Anchor 1'.", failures);
            Expect(names.Contains("Anchor 2"), $"[{name}] should contain 'Anchor 2'.", failures);
            Expect(names.Contains("Anchor 3"), $"[{name}] should contain 'Anchor 3'.", failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void CreateWorkspaceFixture(string tempRoot, int anchorCount)
    {
        var rfsRoot = Path.Combine(tempRoot, ".rfs");
        var rckRoot = Path.Combine(rfsRoot, "rck");
        var statesRoot = Path.Combine(rckRoot, "states");
        var deltasRoot = Path.Combine(rckRoot, "deltas");
        var anchorsRoot = Path.Combine(rckRoot, "anchors");

        Directory.CreateDirectory(statesRoot);
        Directory.CreateDirectory(deltasRoot);
        Directory.CreateDirectory(anchorsRoot);

        // Create a single state
        var stateId = "s-" + Guid.NewGuid().ToString("N")[..8];
        File.WriteAllText(
            Path.Combine(statesRoot, $"{stateId}.json"),
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["type"] = "rufus.rck.state",
                ["id"] = stateId,
                ["payloadCanonicalJson"] = JsonSerializer.Serialize(new { type = "fixture.semantic" }),
                ["refs"] = Array.Empty<object>(),
                ["meta"] = new Dictionary<string, object?>
                {
                    ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                },
            }));

        File.WriteAllText(Path.Combine(rckRoot, "HEAD"), stateId + Environment.NewLine);

        // Create anchors with increasing timestamps
        var baseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= anchorCount; i++)
        {
            var anchorId = $"a-{Guid.NewGuid().ToString("N")[..8]}";
            var anchorTime = baseTime.AddHours(i);
            File.WriteAllText(
                Path.Combine(anchorsRoot, $"{anchorId}.json"),
                JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["schemaVersion"] = 1,
                    ["type"] = "rufus.rck.anchor",
                    ["id"] = anchorId,
                    ["stateId"] = stateId,
                    ["parentAnchorIds"] = Array.Empty<object>(),
                    ["meta"] = new Dictionary<string, object?>
                    {
                        ["createdAtUtc"] = anchorTime.ToString("O"),
                        ["Label"] = $"Anchor {i}",
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

    private static void Expect(bool condition, string message, List<string> failures)
    {
        if (!condition)
            failures.Add(message);
    }
}
