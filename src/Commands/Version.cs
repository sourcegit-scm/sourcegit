using System;

namespace SourceGit.Commands {
    /// <summary>
    ///     检测git是否可用，并获取git版本信息
    /// </summary>
    public class Version : Command {
        const string GitVersionPrefix = "git version ";
        public string Query() {
            Args = $"--version";
            var result = ReadToEnd();
            if (!result.IsSuccess || string.IsNullOrEmpty(result.Output)) return null;
            var version = result.Output.Trim();
            if (!version.StartsWith(GitVersionPrefix, StringComparison.Ordinal)) return null;
            return version.Substring(GitVersionPrefix.Length);
        }
    }
}
