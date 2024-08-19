using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit
{
    public class ColorConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return Color.Parse(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    public class GridLengthConverter : JsonConverter<GridLength>
    {
        public override GridLength Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var size = reader.GetDouble();
            return new GridLength(size, GridUnitType.Pixel);
        }

        public override void Write(Utf8JsonWriter writer, GridLength value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.Value);
        }
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        IgnoreReadOnlyFields = true,
        IgnoreReadOnlyProperties = true,
        Converters = [
            typeof(ColorConverter),
            typeof(GridLengthConverter),
        ]
    )]
    [JsonSerializable(typeof(Models.ExternalToolPaths))]
    [JsonSerializable(typeof(Models.InteractiveRebaseJobCollection))]
    [JsonSerializable(typeof(Models.JetBrainsState))]
    [JsonSerializable(typeof(Models.ThemeOverrides))]
    [JsonSerializable(typeof(Models.Version))]
    [JsonSerializable(typeof(Models.RepositorySettings))]
    [JsonSerializable(typeof(ViewModels.Preference))]
    internal partial class JsonCodeGen : JsonSerializerContext { }
}
