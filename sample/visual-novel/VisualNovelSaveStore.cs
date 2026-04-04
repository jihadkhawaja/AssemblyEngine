using System.Text.Json;

namespace VisualNovelSample;

internal static class VisualNovelSaveStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static bool TryLoad(string path, out VisualNovelSaveState? state)
    {
        state = null;
        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            state = JsonSerializer.Deserialize<VisualNovelSaveState>(json, JsonOptions);
            return state is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static void Save(string path, VisualNovelSaveState state)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
    }
}