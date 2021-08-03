using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

#if NET48
using Newtonsoft.Json;
#else
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

namespace SourceGit.Models {
    /// <summary>
    ///     崩溃日志上报
    /// </summary>
    public class Issue {
#if NET48
        [JsonProperty(PropertyName = "access_token")]
        public string AccessToken { get; set; }
        [JsonProperty(PropertyName = "repo")]
        public string Repo { get; set; }
        [JsonProperty(PropertyName = "title")]
        public string Title { get; set; }
        [JsonProperty(PropertyName = "body")]
        public string Body { get; set; }
#else
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
        [JsonPropertyName("repo")]
        public string Repo { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("body")]
        public string Body { get; set; }
#endif

        /// <summary>
        ///     创建Gitee平台ISSUE
        /// </summary>
        /// <param name="e"></param>
        public static void Create(System.Exception e) {
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

                var req = WebRequest.CreateHttp("https://gitee.com/api/v5/repos/sourcegit/issues");
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Headers.Add("charset", "UTF-8");
                req.Timeout = 1000;

                using (var writer = req.GetRequestStream()) {
#if NET48
                    var data = JsonConvert.SerializeObject(issue);
                    var raw = Encoding.UTF8.GetBytes(data);
                    writer.Write(raw, 0, raw.Length);
#else
                    var data = JsonSerializer.Serialize(issue);
                    writer.Write(Encoding.UTF8.GetBytes(data));
#endif
                }

                req.GetResponse();
            } catch {}
        }
    }
}
