using System;
using System.Text.RegularExpressions;

namespace SourceGit.Models {
    /// <summary>
    ///     Git用户
    /// </summary>
    public class User {
        private static readonly Regex REG_FORMAT = new Regex(@"\w+ (.*) <(.*)> (\d{10}) [\+\-]\d+");

        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Time { get; set; } = "";

        public void Parse(string data) {
            var match = REG_FORMAT.Match(data);
            if (!match.Success) return;

            var time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(int.Parse(match.Groups[3].Value));

            Name = match.Groups[1].Value;
            Email = match.Groups[2].Value;
            Time = time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
