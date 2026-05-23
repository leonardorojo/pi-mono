using System.Text;
using System.Text.Json;

namespace Rufus.RCK.Workspace;

public sealed record RckSimpleContext(
    string Type,
    int SchemaVersion,
    RckSimpleContextPrompt Prompt,
    RckSimpleContextBudget Budget,
    RckSimpleContextGit Git,
    IReadOnlyList<RckSimpleContextRecentInteraction> RecentInteractions,
    IReadOnlyList<RckSimpleContextAnchorRef> Anchors,
    IReadOnlyList<RckSimpleContextArtifactRef> Artifacts,
    IReadOnlyList<string> Omissions,
    RckSimpleContextGuardrails Guardrails)
{
    public const string DefaultType = "rufus.simple-context";
    public const int DefaultSchemaVersion = 1;

    public string Render()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"type\": {JsonSerializer.Serialize(Type)},");
        sb.AppendLine($"  \"schemaVersion\": {SchemaVersion},");

        sb.AppendLine("  \"prompt\": {");
        sb.AppendLine($"    \"text\": {RenderMultiLineJsonString(Prompt.Text)},");
        sb.AppendLine($"    \"isExcerpt\": {Prompt.IsExcerpt.ToString().ToLowerInvariant()}");
        sb.AppendLine("  },");

        sb.AppendLine("  \"budget\": {");
        sb.AppendLine($"    \"targetChars\": {Budget.TargetChars},");
        sb.AppendLine($"    \"maxChars\": {Budget.MaxChars},");
        sb.AppendLine($"    \"hardMaxChars\": {Budget.HardMaxChars},");
        sb.AppendLine($"    \"estimatedChars\": {Budget.EstimatedChars},");
        sb.AppendLine($"    \"estimatedTokens\": {Budget.EstimatedTokens},");
        sb.AppendLine($"    \"truncated\": {Budget.Truncated.ToString().ToLowerInvariant()}");
        sb.AppendLine("  },");

        sb.AppendLine("  \"git\": {");
        sb.AppendLine($"    \"branch\": {RenderNullableString(Git.Branch)},");
        sb.AppendLine($"    \"commit\": {RenderNullableString(Git.Commit)},");
        sb.AppendLine($"    \"dirty\": {Git.Dirty.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    \"changedArtifactsCount\": {Git.ChangedArtifactsCount}");
        if (Git.ChangedArtifacts.Count > 0)
        {
            sb.AppendLine("    ,\"changedArtifacts\": [");
            AppendArtifactRefs(sb, Git.ChangedArtifacts, indent: 6);
            sb.AppendLine("    ]");
        }
        sb.AppendLine("  },");

        sb.AppendLine("  \"recentInteractions\": [");
        AppendRecentInteractions(sb, RecentInteractions);
        sb.AppendLine("  ],");

        sb.AppendLine("  \"anchors\": [");
        AppendAnchors(sb, Anchors);
        sb.AppendLine("  ],");

        sb.AppendLine("  \"artifacts\": [");
        AppendArtifactRefs(sb, Artifacts, indent: 4);
        sb.AppendLine("  ],");

        sb.AppendLine("  \"omissions\": [");
        AppendStrings(sb, Omissions, indent: 4);
        sb.AppendLine("  ],");

        sb.AppendLine("  \"guardrails\": {");
        sb.AppendLine($"    \"includeFileContents\": {Guardrails.IncludeFileContents.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    \"includeGitDiffs\": {Guardrails.IncludeGitDiffs.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    \"includeJsonl\": {Guardrails.IncludeJsonl.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    \"includeStdoutStderr\": {Guardrails.IncludeStdoutStderr.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    \"includeToolOutputs\": {Guardrails.IncludeToolOutputs.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    \"includeFullContextPack\": {Guardrails.IncludeFullContextPack.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    \"includeFullTraceSlice\": {Guardrails.IncludeFullTraceSlice.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    \"includePayloadCanonicalJson\": {Guardrails.IncludePayloadCanonicalJson.ToString().ToLowerInvariant()}");
        sb.AppendLine("  }");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendRecentInteractions(StringBuilder sb, IReadOnlyList<RckSimpleContextRecentInteraction> interactions)
    {
        for (var index = 0; index < interactions.Count; index++)
        {
            var interaction = interactions[index];
            sb.AppendLine("    {");
            sb.AppendLine($"      \"stateId\": {JsonSerializer.Serialize(interaction.StateId)},");
            sb.AppendLine($"      \"stateShortId\": {JsonSerializer.Serialize(interaction.StateShortId)},");
            sb.AppendLine($"      \"deltaId\": {RenderNullableString(interaction.DeltaId)},");
            sb.AppendLine($"      \"deltaShortId\": {RenderNullableString(interaction.DeltaShortId)},");
            sb.AppendLine($"      \"mode\": {JsonSerializer.Serialize(interaction.Mode)},");
            sb.AppendLine("      \"prompt\": {");
            sb.AppendLine($"        \"text\": {RenderMultiLineJsonString(interaction.Prompt.Text)},");
            sb.AppendLine($"        \"isExcerpt\": {interaction.Prompt.IsExcerpt.ToString().ToLowerInvariant()}");
            sb.AppendLine("      },");
            sb.AppendLine($"      \"answerSummary\": {RenderMultiLineJsonString(interaction.AnswerSummary)},");
            sb.AppendLine($"      \"createdAtUtc\": {RenderNullableString(interaction.CreatedAtUtc?.ToUniversalTime().ToString("o"))},");
            sb.AppendLine($"      \"gitCommit\": {RenderNullableString(interaction.GitCommit)},");
            sb.AppendLine("      \"artifactRefs\": [");
            AppendArtifactRefs(sb, interaction.ArtifactRefs, indent: 8);
            sb.AppendLine("      ]");
            sb.Append("    }");
            if (index < interactions.Count - 1)
            {
                sb.Append(',');
            }
            sb.AppendLine();
        }
    }

    private static void AppendAnchors(StringBuilder sb, IReadOnlyList<RckSimpleContextAnchorRef> anchors)
    {
        for (var index = 0; index < anchors.Count; index++)
        {
            var anchor = anchors[index];
            sb.AppendLine("    {");
            sb.AppendLine($"      \"id\": {JsonSerializer.Serialize(anchor.Id)},");
            sb.AppendLine($"      \"shortId\": {JsonSerializer.Serialize(anchor.ShortId)},");
            sb.AppendLine($"      \"label\": {RenderNullableString(anchor.Label)},");
            sb.AppendLine($"      \"createdAtUtc\": {JsonSerializer.Serialize(anchor.CreatedAtUtc.ToUniversalTime().ToString("o"))},");
            sb.AppendLine($"      \"stateId\": {JsonSerializer.Serialize(anchor.StateId)},");
            sb.AppendLine($"      \"stateShortId\": {JsonSerializer.Serialize(anchor.StateShortId)}");
            sb.Append("    }");
            if (index < anchors.Count - 1)
            {
                sb.Append(',');
            }
            sb.AppendLine();
        }
    }

    private static void AppendArtifactRefs(StringBuilder sb, IReadOnlyList<RckSimpleContextArtifactRef> artifacts, int indent)
    {
        var pad = new string(' ', indent);
        for (var index = 0; index < artifacts.Count; index++)
        {
            var artifact = artifacts[index];
            sb.AppendLine($"{pad}{{");
            sb.AppendLine($"{pad}  \"path\": {JsonSerializer.Serialize(artifact.Path)},");
            sb.AppendLine($"{pad}  \"status\": {JsonSerializer.Serialize(artifact.Status)},");
            sb.AppendLine($"{pad}  \"kind\": {RenderNullableString(artifact.Kind)},");
            sb.AppendLine($"{pad}  \"sizeBytes\": {RenderNullableNumber(artifact.SizeBytes)}");
            sb.Append($"{pad}}}");
            if (index < artifacts.Count - 1)
            {
                sb.Append(',');
            }
            sb.AppendLine();
        }
    }

    private static void AppendStrings(StringBuilder sb, IReadOnlyList<string> values, int indent)
    {
        var pad = new string(' ', indent);
        for (var index = 0; index < values.Count; index++)
        {
            sb.Append($"{pad}{JsonSerializer.Serialize(values[index])}");
            if (index < values.Count - 1)
            {
                sb.Append(',');
            }
            sb.AppendLine();
        }
    }

    private static string RenderNullableString(string? value)
        => value is null ? "null" : JsonSerializer.Serialize(value);

    private static string RenderNullableNumber(long? value)
        => value is null ? "null" : value.Value.ToString();

    private static string RenderMultiLineJsonString(string? value)
    {
        if (value is null)
        {
            return "null";
        }

        return JsonSerializer.Serialize(value);
    }
}
