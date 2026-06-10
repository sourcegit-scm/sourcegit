using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public record GitIgnoreFile(string DisplayName, string FullPath, string Pattern, bool IsLocalOnly)
    {
        public static List<GitIgnoreFile> GetSupported(string repo, string gitDir, string pattern)
        {
            var supported = new List<GitIgnoreFile>();

            // .gitignore in repository root.
            supported.Add(new(".gitignore", $"{repo}/.gitignore", pattern, false));

            // If pattern points to a file/directory that in sub-directory of repository, we should also support .gitignore in that sub-directory.
            var normalizedPattern = pattern.Replace('\\', '/').TrimEnd('/');
            var lastDirIdx = normalizedPattern.LastIndexOf('/');
            if (lastDirIdx > 0)
            {
                var parentDir = normalizedPattern.Substring(0, lastDirIdx);
                var overridedPattern = normalizedPattern.Substring(lastDirIdx + 1);
                supported.Add(new($"{parentDir}/.gitignore", $"{repo}/{parentDir}/.gitignore", overridedPattern, false));
            }

            // .git/info/exclude in git directory.
            var normalizedGitDir = gitDir.Replace('\\', '/');
            var testGitDir = $"{repo}/.git".Replace('\\', '/');
            if (normalizedGitDir.Equals(testGitDir, StringComparison.Ordinal))
                supported.Add(new(".git/info/exclude", $"{normalizedGitDir}/info/exclude", pattern, true));
            else
                supported.Add(new(".git/info/exclude", $"{gitDir}/info/exclude", pattern, true));

            return supported;
        }
    }
}
