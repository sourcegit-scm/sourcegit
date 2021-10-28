using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

#if NET6_0_OR_GREATER
using System.Net.Http;
#else
using System.Net;
using System.Text;
#endif

namespace SourceGit.Models {
    /// <summary>
    ///     崩溃日志上报
    /// </summary>
    public class Issue {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
        [JsonPropertyName("repo")]
        public string Repo { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("body")]
        public string Body { get; set; }

        /// <summary>
        ///     创建Gitee平台ISSUE
        /// </summary>
        /// <param name="e"></param>
        public static void Create(System.Exception e) {
            if (!Preference.Instance.General.EnableCrashReport) return;

            try {
                var issue = new Issue();
                issue.AccessToken = "d0d56410f13a3826b87fb0868d5a26ce"; // 这是我个人的Token，仅启用ISSUE创建功能，请不要使用
                issue.Repo = "sourcegit";
                issue.Title = "CrashReport: " + e.Message;
                issue.Body = string.Format(
                    "{0}\n\n**Base Information:**\n\n| Windows OS | {1} |\n|---|---|\n| Version | {2} |\n| Platform | {3} |\n\n**Source:** {4}\n\n**StackTrace:**\n\n ```\n{5}\n```\n",
                    e.Message,
                    Environment.OSVersion.ToString(),
                    Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                    AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName,
                    e.Source,
                    e.StackTrace);

#if NET6_0_OR_GREATER
                var req = new HttpClient();
                req.DefaultRequestHeaders.Add("Content-Type", "application/json");
                req.DefaultRequestHeaders.Add("charset", "UTF-8");
                req.Timeout = TimeSpan.FromSeconds(1);
                req.PostAsync("https://gitee.com/api/v5/repos/sourcegit/issues", new StringContent(JsonSerializer.Serialize(issue))).Wait();
#else
                var req = WebRequest.CreateHttp("https://gitee.com/api/v5/repos/sourcegit/issues");
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Headers.Add("charset", "UTF-8");
                req.Timeout = 1000;

                using (var writer = req.GetRequestStream()) {
                    var data = JsonSerializer.Serialize(issue);
                    var raw = Encoding.UTF8.GetBytes(data);
                    writer.Write(raw, 0, raw.Length);
                }

                req.GetResponse();
#endif
            } catch { }
        }
    }
}
