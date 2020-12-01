using System;
using System.Text.Json.Serialization;

namespace SourceGit.Git {

    /// <summary>
    ///     Version information.
    /// </summary>
    public class Version {
        [JsonPropertyName("id")]
        public ulong Id { get; set; }
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; }
        [JsonPropertyName("target_commitish")]
        public string CommitSHA { get; set; }
        [JsonPropertyName("prerelease")]
        public bool PreRelease { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("body")]
        public string Body { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
