using System.Text.Json.Serialization;

namespace SourceGit.Models
{
    public class LFSLockOwner
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class LFSLock
    {
        [JsonPropertyName("id")]
        public string ID { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("owner")]
        public LFSLockOwner Owner { get; set; } = null;
    }
}
