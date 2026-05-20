using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Rufus.RCK.Core.Hashing;

/// <summary>
/// Produces a deterministic, structural JSON representation without interpreting semantics.
/// </summary>
public static class RckCanonicalJson
{
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        CommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
    };

    public static string Canonicalize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        try
        {
            using var document = JsonDocument.Parse(json, ParseOptions);
            return Canonicalize(document.RootElement);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Payload JSON must be valid JSON.", nameof(json), ex);
        }
    }

    public static string Canonicalize(JsonElement element)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        WriteElement(writer, element);
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (var entry in element.EnumerateObject()
                             .Select((property, index) => (property, index))
                             .OrderBy(entry => entry.property.Name, StringComparer.Ordinal)
                             .ThenBy(entry => entry.index))
                {
                    writer.WritePropertyName(entry.property.Name);
                    WriteElement(writer, entry.property.Value);
                }

                writer.WriteEndObject();
                return;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteElement(writer, item);
                }
                writer.WriteEndArray();
                return;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                return;

            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                return;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                return;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                return;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(element), element.ValueKind, "Unsupported JSON value kind.");
        }
    }
}
