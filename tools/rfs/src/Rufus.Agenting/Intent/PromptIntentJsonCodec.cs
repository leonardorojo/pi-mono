using System.Text.Json;

namespace Rufus.Agenting.Intent;

public static class PromptIntentJsonCodec
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly HashSet<string> AllowedPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "intent",
        "summary",
        "entities",
        "constraints",
    };

    public static bool TryParse(string json, out PromptIntent? promptIntent, out string? errorMessage)
    {
        promptIntent = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            errorMessage = "PromptIntent JSON is empty.";
            return false;
        }

        var normalizedJson = NormalizeJson(json);

        try
        {
            using var document = JsonDocument.Parse(normalizedJson);
            promptIntent = ReadPromptIntent(document.RootElement);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid PromptIntent JSON: {ex.Message}";
            return false;
        }
    }

    public static PromptIntent Parse(string json)
    {
        if (TryParse(json, out var promptIntent, out var errorMessage))
        {
            return promptIntent!;
        }

        throw new JsonException(errorMessage);
    }

    public static string Write(PromptIntent promptIntent)
    {
        ArgumentNullException.ThrowIfNull(promptIntent);
        return JsonSerializer.Serialize(promptIntent, WriteOptions);
    }

    private static PromptIntent ReadPromptIntent(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("PromptIntent JSON must be an object.");
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!AllowedPropertyNames.Contains(property.Name))
            {
                throw new JsonException($"Unexpected property '{property.Name}' in PromptIntent JSON.");
            }
        }

        var intent = GetRequiredString(root, "intent");
        var summary = GetRequiredString(root, "summary");
        var entities = GetStringArray(root, "entities");
        var constraints = GetStringArray(root, "constraints");

        return new PromptIntent(intent, summary, entities, constraints);
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(root, propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"PromptIntent JSON is missing string property '{propertyName}'.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException($"PromptIntent JSON property '{propertyName}' cannot be empty.");
        }

        return value;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(root, propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"PromptIntent JSON is missing array property '{propertyName}'.");
        }

        var values = new List<string>();
        var index = 0;
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new JsonException($"PromptIntent JSON array '{propertyName}' contains a non-string value at index {index}.");
            }

            var value = item.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new JsonException($"PromptIntent JSON array '{propertyName}' contains an empty string at index {index}.");
            }

            values.Add(value);
            index++;
        }

        return values;
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

    private static string NormalizeJson(string json)
    {
        var trimmed = json.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var lines = trimmed.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        if (lines.Length < 3 || !lines[0].StartsWith("```", StringComparison.Ordinal) || !string.Equals(lines[^1].Trim(), "```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return string.Join(Environment.NewLine, lines[1..^1]).Trim();
    }
}
