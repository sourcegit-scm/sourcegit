using System;

#if NET48
using Newtonsoft.Json;
#else
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

namespace SourceGit.Models {

    /// <summary>
    ///     Gitee开放API中Release信息格式
    /// </summary>
    public class Version {
#if NET48
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
#else
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
#endif
        public string PublishTime {
            get { return CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"); }
        }

        public string IsPrerelease {
            get { return PreRelease ? "YES" : "NO"; }
        }

        public static Version Load(string data) {
#if NET48
            return JsonConvert.DeserializeObject<Version>(data);
#else
            return JsonSerializer.Deserialize<Version>(data);
#endif
        }
    }
}
