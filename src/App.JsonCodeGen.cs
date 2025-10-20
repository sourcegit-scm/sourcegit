using System;
using System.Collections.Generic;
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

    public class DataGridLengthConverter : JsonConverter<DataGridLength>
    {
        public override DataGridLength Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var size = reader.GetDouble();
            return new DataGridLength(size, DataGridLengthUnitType.Pixel, 0, size);
        }

        public override void Write(Utf8JsonWriter writer, DataGridLength value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.DisplayValue);
        }
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        IgnoreReadOnlyFields = true,
        IgnoreReadOnlyProperties = true,
        Converters = [
            typeof(ColorConverter),
            typeof(GridLengthConverter),
            typeof(DataGridLengthConverter),
        ]
    )]
    [JsonSerializable(typeof(Models.ExternalToolPaths))]
    [JsonSerializable(typeof(Models.InteractiveRebaseJobCollection))]
    [JsonSerializable(typeof(Models.JetBrainsState))]
    [JsonSerializable(typeof(Models.ThemeOverrides))]
    [JsonSerializable(typeof(Models.Version))]
    [JsonSerializable(typeof(Models.RepositorySettings))]
    [JsonSerializable(typeof(List<Models.ConventionalCommitType>))]
    [JsonSerializable(typeof(List<Models.LFSLock>))]
    [JsonSerializable(typeof(List<Models.VisualStudioInstance>))]
    [JsonSerializable(typeof(ViewModels.Preferences))]
    internal partial class JsonCodeGen : JsonSerializerContext { }
}
