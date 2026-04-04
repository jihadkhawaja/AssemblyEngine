using System.Text.Json;
using System.Text.Json.Serialization;

namespace SampleGame;

internal static class SampleSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    static SampleSettingsStore()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public static SampleSettings Load(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<SampleSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    settings.Sanitize();
                    Save(path, settings);
                    return settings;
                }
            }
            catch (JsonException)
            {
            }
        }

        var defaults = new SampleSettings();
        Save(path, defaults);
        return defaults;
    }

    public static void Save(string path, SampleSettings settings)
    {
        settings.Sanitize();

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
    }
}