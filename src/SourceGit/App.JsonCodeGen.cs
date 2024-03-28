using System.Text.Json.Serialization;

namespace SourceGit
{
    [JsonSourceGenerationOptions(WriteIndented = true, IgnoreReadOnlyFields = true, IgnoreReadOnlyProperties = true)]
    [JsonSerializable(typeof(Models.Version))]
    [JsonSerializable(typeof(ViewModels.Preference))]
    internal partial class JsonCodeGen : JsonSerializerContext { }
}