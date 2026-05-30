using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rufus.RCK.Semantic;

public static class RckSemanticProjectionJsonStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static void Write(string path, RckSemanticProjection projection)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(projection);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(projection, Options);
        File.WriteAllText(path, json);
    }

    public static RckSemanticProjection Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Projection file not found: {path}");

        var json = File.ReadAllText(path);
        var projection = JsonSerializer.Deserialize<RckSemanticProjection>(json, Options);
        return projection ?? throw new InvalidOperationException("Failed to deserialize semantic projection.");
    }
}
