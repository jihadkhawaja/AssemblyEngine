using AssemblyEngine.Core;
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
        JsonOptions.Converters.Add(new GraphicsBackendJsonConverter());
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

    private sealed class GraphicsBackendJsonConverter : JsonConverter<GraphicsBackend>
    {
        public override GraphicsBackend Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (!string.IsNullOrWhiteSpace(value)
                    && Enum.TryParse<GraphicsBackend>(value, ignoreCase: true, out var backend)
                    && Enum.IsDefined(backend))
                {
                    return backend;
                }
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numericValue))
            {
                var backend = (GraphicsBackend)numericValue;
                if (Enum.IsDefined(backend))
                    return backend;
            }

            return GraphicsBackend.Software;
        }

        public override void Write(Utf8JsonWriter writer, GraphicsBackend value, JsonSerializerOptions options)
        {
            var backend = Enum.IsDefined(value) ? value : GraphicsBackend.Software;
            writer.WriteStringValue(backend.ToString().ToLowerInvariant());
        }
    }
}