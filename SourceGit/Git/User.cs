using System;
using System.Text.RegularExpressions;

namespace SourceGit.Git {

    /// <summary>
    ///     Git user.
    /// </summary>
    public class User {
        private static readonly Regex FORMAT = new Regex(@"\w+ (.*) <([\w\.\-_]+@[\w\.\-_]+)> (\d{10}) [\+\-]\d+");

        /// <summary>
        ///     Name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        ///     Email.
        /// </summary>
        public string Email { get; set; } = "";

        /// <summary>
        ///     Operation time.
        /// </summary>
        public string Time { get; set; } = "";

        /// <summary>
        ///     Parse user from raw string.
        /// </summary>
        /// <param name="data">Raw string</param>
        public void Parse(string data) {
            var match = FORMAT.Match(data);
            if (!match.Success) return;

            var time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(int.Parse(match.Groups[3].Value));

            Name = match.Groups[1].Value;
            Email = match.Groups[2].Value;
            Time = time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
