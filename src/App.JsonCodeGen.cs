using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SourceGit
{
    [JsonSourceGenerationOptions(WriteIndented = true, IgnoreReadOnlyFields = true, IgnoreReadOnlyProperties = true)]
    [JsonSerializable(typeof(List<Models.InteractiveRebaseJob>))]
    [JsonSerializable(typeof(Models.JetBrainsState))]
    [JsonSerializable(typeof(Models.Version))]
    [JsonSerializable(typeof(Models.CustomColorSchema))]
    [JsonSerializable(typeof(ViewModels.Preference))]
    [JsonSerializable(typeof(ViewModels.RepositorySettings))]
    internal partial class JsonCodeGen : JsonSerializerContext { }
}
