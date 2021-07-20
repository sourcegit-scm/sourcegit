using System;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

        public static void Check(Action<Version> onUpgradable) {
            if (!Preference.Instance.General.CheckForUpdate) return;

            var curDayOfYear = DateTime.Now.DayOfYear;
            var lastDayOfYear = Preference.Instance.General.LastCheckDay;
            if (lastDayOfYear != curDayOfYear) {
                Preference.Instance.General.LastCheckDay = curDayOfYear;
                Task.Run(() => {
                    try {
                        var web = new WebClient() { Encoding = Encoding.UTF8 };
                        var raw = web.DownloadString("https://gitee.com/api/v5/repos/sourcegit/sourcegit/releases/latest");
#if NET48
                        var ver = JsonConvert.DeserializeObject<Version>(raw);
#else
                        var ver = JsonSerializer.Deserialize<Version>(raw);
#endif
                        var cur = Assembly.GetExecutingAssembly().GetName().Version;

                        var matches = Regex.Match(ver.TagName, @"^v(\d+)\.(\d+).*");
                        if (!matches.Success) return;

                        var major = int.Parse(matches.Groups[1].Value);
                        var minor = int.Parse(matches.Groups[2].Value);
                        if (major > cur.Major || (major == cur.Major && minor > cur.Minor)) {
                            onUpgradable?.Invoke(ver);
                        }
                    } catch {
                    }
                });
            }
        }
    }
}
