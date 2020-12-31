using Newtonsoft.Json;
using System;

namespace SourceGit.Git {

    /// <summary>
    ///     Version information.
    /// </summary>
    public class Version {
        [JsonProperty(PropertyName = "id")]
        public ulong Id { get; set; }
        [JsonProperty(PropertyName = "tag_name")]
        public string TagName { get; set; }
        [JsonProperty(PropertyName = "target_commitish")]
        public string CommitSHA { get; set; }
        [JsonProperty(PropertyName = "prerelease")]
        public bool PreRelease { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "body")]
        public string Body { get; set; }
        [JsonProperty(PropertyName = "created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
