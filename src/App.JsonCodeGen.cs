using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SourceGit
{
    [JsonSourceGenerationOptions(WriteIndented = true, IgnoreReadOnlyFields = true, IgnoreReadOnlyProperties = true)]
    [JsonSerializable(typeof(Models.Version))]
    [JsonSerializable(typeof(Models.JetBrainsState))]
    [JsonSerializable(typeof(List<Models.InteractiveRebaseJob>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(ViewModels.Preference))]
    internal partial class JsonCodeGen : JsonSerializerContext { }
}
