namespace Rufus.Agenting.Json;

public static class LlmJsonOutputNormalizer
{
    public static string Normalize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return TryNormalize(json, out var normalizedJson, out _)
            ? normalizedJson
            : json.Trim();
    }

    public static bool TryNormalize(string json, out string normalizedJson, out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(json);

        normalizedJson = string.Empty;
        errorMessage = null;

        var trimmed = json.Trim();
        if (trimmed.Length == 0)
        {
            errorMessage = "LLM output is empty.";
            return false;
        }

        if (TryExtractFirstJsonObject(trimmed, out normalizedJson))
        {
            return true;
        }

        errorMessage = "LLM output does not contain a JSON object.";
        return false;
    }

    private static bool TryExtractFirstJsonObject(string text, out string normalizedJson)
    {
        normalizedJson = string.Empty;

        var startIndex = -1;
        var depth = 0;
        var inString = false;
        var escapeNext = false;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];

            if (startIndex < 0)
            {
                if (current == '{')
                {
                    startIndex = index;
                    depth = 1;
                    inString = false;
                    escapeNext = false;
                }

                continue;
            }

            if (inString)
            {
                if (escapeNext)
                {
                    escapeNext = false;
                    continue;
                }

                if (current == '\\')
                {
                    escapeNext = true;
                    continue;
                }

                if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (current == '"')
            {
                inString = true;
                continue;
            }

            if (current == '{')
            {
                depth++;
                continue;
            }

            if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    normalizedJson = text[startIndex..(index + 1)];
                    return true;
                }
            }
        }

        return false;
    }
}
