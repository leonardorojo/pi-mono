using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace Rufus.RCK.Workspace;

public sealed record RckConversationalMemory(
    string Type,
    int SchemaVersion,
    string Summary,
    string ActiveTopic,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> RecentDecisions,
    IReadOnlyList<string> ContinuityHints,
    IReadOnlyList<string> Warnings);

public static class RckConversationalMemoryJsonCodec
{
    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly HashSet<string> AllowedPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "type",
        "schemaVersion",
        "summary",
        "activeTopic",
        "openQuestions",
        "recentDecisions",
        "continuityHints",
        "warnings",
    };

    public static bool TryParse(string json, out RckConversationalMemory? conversationalMemory, out string? errorMessage)
    {
        conversationalMemory = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            errorMessage = "ConversationalMemory JSON is empty.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            foreach (var property in root.EnumerateObject())
            {
                if (!AllowedPropertyNames.Contains(property.Name))
                {
                    throw new JsonException($"Unexpected property '{property.Name}' in ConversationalMemory JSON.");
                }
            }

            var type = GetRequiredString(root, "type");
            if (!string.Equals(type, "rufus.conversational-memory", StringComparison.Ordinal))
            {
                errorMessage = "ConversationalMemory JSON must declare type='rufus.conversational-memory'.";
                return false;
            }

            var schemaVersion = GetRequiredInt32(root, "schemaVersion");
            if (schemaVersion != 1)
            {
                errorMessage = "ConversationalMemory JSON must declare schemaVersion=1.";
                return false;
            }

            var summary = GetRequiredString(root, "summary");
            var activeTopic = GetRequiredString(root, "activeTopic");
            var openQuestions = ReadStringArray(root, "openQuestions");
            var recentDecisions = ReadStringArray(root, "recentDecisions");
            var continuityHints = ReadStringArray(root, "continuityHints");
            var warnings = ReadStringArray(root, "warnings");

            if (!TryValidateNoForbiddenContent(root, out errorMessage))
            {
                return false;
            }

            conversationalMemory = new RckConversationalMemory(
                Type: type,
                SchemaVersion: schemaVersion,
                Summary: summary,
                ActiveTopic: activeTopic,
                OpenQuestions: openQuestions,
                RecentDecisions: recentDecisions,
                ContinuityHints: continuityHints,
                Warnings: warnings);
            return true;
        }
        catch (JsonException ex)
        {
            errorMessage = $"Invalid ConversationalMemory JSON: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid ConversationalMemory payload: {ex.Message}";
            return false;
        }
    }

    public static RckConversationalMemory Parse(string json)
    {
        if (TryParse(json, out var conversationalMemory, out var errorMessage))
        {
            return conversationalMemory!;
        }

        throw new JsonException(errorMessage);
    }

    public static string Write(RckConversationalMemory conversationalMemory)
    {
        ArgumentNullException.ThrowIfNull(conversationalMemory);
        return JsonSerializer.Serialize(conversationalMemory, WriteOptions);
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(root, propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"ConversationalMemory JSON is missing string property '{propertyName}'.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException($"ConversationalMemory JSON property '{propertyName}' cannot be empty.");
        }

        return value;
    }

    private static int GetRequiredInt32(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(root, propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException($"ConversationalMemory JSON is missing numeric property '{propertyName}'.");
        }

        return property.GetInt32();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(root, propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"ConversationalMemory JSON is missing array property '{propertyName}'.");
        }

        var values = new List<string>();
        var index = 0;
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new JsonException($"ConversationalMemory JSON array '{propertyName}' contains a non-string value at index {index}.");
            }

            var value = item.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new JsonException($"ConversationalMemory JSON array '{propertyName}' contains an empty string at index {index}.");
            }

            values.Add(value);
            index++;
        }

        return values;
    }

    private static bool TryValidateNoForbiddenContent(JsonElement element, out string? errorMessage)
    {
        foreach (var stringValue in EnumerateStringValues(element))
        {
            if (ContainsForbiddenFragment(stringValue, out var fragment))
            {
                errorMessage = $"ConversationalMemory JSON contains forbidden fragment '{fragment}'.";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    private static IEnumerable<string> EnumerateStringValues(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                yield return element.GetString() ?? string.Empty;
                yield break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var value in EnumerateStringValues(property.Value))
                    {
                        yield return value;
                    }
                }
                yield break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var value in EnumerateStringValues(item))
                    {
                        yield return value;
                    }
                }
                yield break;

            default:
                yield break;
        }
    }

    private static bool ContainsForbiddenFragment(string value, out string fragment)
    {
        foreach (var candidate in new[]
        {
            "diff --git",
            "stdout",
            "stderr",
            "message_update",
            "message_end",
            "assistantMessageEvent",
            "payloadCanonicalJson",
            "selectedStateIds",
            "selectedDeltaIds",
            "selectedAnchorIds",
            ".rfs/rck",
            "```",
        })
        {
            if (value.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                fragment = candidate;
                return true;
            }
        }

        fragment = string.Empty;
        return false;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string propertyName, out JsonElement property)
    {
        foreach (var candidate in root.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}
