using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#if NET6_0
using System.Net.Http;
#else
using System.Net;
using System.Text;
#endif

namespace SourceGit.Models {

    /// <summary>
    ///     Github开放API中Release信息格式
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
                Task.Run(async () => {
                    try {
#if NET6_0
                        var req = new HttpClient();
                        var rsp = await req.GetAsync("https://api.github.com/repos/sourcegit-scm/sourcegit/releases/latest");
                        rsp.EnsureSuccessStatusCode();

                        var raw = await rsp.Content.ReadAsStringAsync();
#else
                        var web = new WebClient() { Encoding = Encoding.UTF8 };
                        var raw = await web.DownloadStringTaskAsync("https://api.github.com/repos/sourcegit-scm/sourcegit/releases/latest");
#endif
                        var ver = JsonSerializer.Deserialize<Version>(raw);
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
