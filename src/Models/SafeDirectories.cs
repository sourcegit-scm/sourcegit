using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGit.Models
{
    /// <summary>
    ///     Helpers for the `safe.directory` protection introduced by git 2.35.2 (CVE-2022-24765).
    ///     Git refuses to use a repository whose top-level directory is owned by another user (which is
    ///     always the case for network shares / UNC paths) unless the path is listed in the `safe.directory` config.
    /// </summary>
    public static partial class SafeDirectories
    {
        public static bool IsUntrustedRepository(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return false;

            return output.Contains("detected dubious ownership", StringComparison.OrdinalIgnoreCase) ||
                   output.Contains("unsafe repository", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     `-c safe.directory=` is only respected by git 2.38 and later. Older versions (2.35.2 - 2.37.x)
        ///     can only read the exception from the system/global config.
        /// </summary>
        public static bool SupportsSessionTrust()
        {
            return Native.OS.GitVersion >= GitVersions.SAFE_DIRECTORY_COMMAND_LINE;
        }

        /// <summary>
        ///     Tries to get the value that should be written into the `safe.directory` config.
        ///     Prefers the value suggested by git itself (which knows the correct form for the current platform),
        ///     and falls back to building a value from the given path.
        /// </summary>
        public static bool TryGetSafeDirectoryValue(string path, string output, out string value)
        {
            value = ParseSuggestedValue(output);
            if (!string.IsNullOrEmpty(value))
                return true;

            var normalized = Normalize(path);
            if (string.IsNullOrEmpty(normalized))
                return false;

            // Git for Windows uses the `%(prefix)/` prefix for UNC paths.
            value = OperatingSystem.IsWindows() && normalized.StartsWith("//", StringComparison.Ordinal) ? $"%(prefix)/{normalized}" : normalized;
            return true;
        }

        /// <summary>
        ///     Trusts a directory only for the current session. The exception will be passed to git with
        ///     `-c safe.directory=<value>` for every command executed under that directory, but it will
        ///     NOT be persisted into the user's git config.
        /// </summary>
        public static void AddSessionTrust(string workingDirectory, string safeDirectory)
        {
            var normalized = Normalize(workingDirectory);
            if (string.IsNullOrEmpty(normalized) || string.IsNullOrEmpty(safeDirectory))
                return;

            lock (s_sessionTrusted)
            {
                foreach (var one in s_sessionTrusted)
                {
                    if (one.WorkingDirectory.Equals(normalized, s_comparison) && one.Value.Equals(safeDirectory, StringComparison.Ordinal))
                        return;
                }

                s_sessionTrusted.Add(new TrustEntry(normalized, safeDirectory));
            }
        }

        public static List<string> GetSessionSafeDirectories(string workingDirectory)
        {
            var outs = new List<string>();
            var normalized = Normalize(workingDirectory);
            if (string.IsNullOrEmpty(normalized))
                return outs;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            lock (s_sessionTrusted)
            {
                foreach (var one in s_sessionTrusted)
                {
                    if (seen.Contains(one.Value))
                        continue;

                    if (normalized.Equals(one.WorkingDirectory, s_comparison) ||
                        normalized.StartsWith(one.WorkingDirectory + "/", s_comparison))
                    {
                        seen.Add(one.Value);
                        outs.Add(one.Value);
                    }
                }
            }

            return outs;
        }

        /// <summary>
        ///     Builds the `-c safe.directory=` arguments for the given working directory. Commands that do not
        ///     inherit `Commands.Command` should prepend the result to their own arguments, so that repositories
        ///     trusted for this session work there too.
        /// </summary>
        public static string GetSessionSafeDirectoryArgs(string workingDirectory)
        {
            var values = GetSessionSafeDirectories(workingDirectory);
            if (values.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            foreach (var one in values)
                builder.Append("-c safe.directory=").Append(one.Quoted()).Append(' ');

            return builder.ToString();
        }

        private static string Normalize(string path)
        {
            return path?.Replace('\\', '/').TrimEnd('/') ?? string.Empty;
        }

        private static string ParseSuggestedValue(string output)
        {
            if (string.IsNullOrEmpty(output))
                return string.Empty;

            var match = REG_SAFE_DIRECTORY_HINT().Match(output);
            if (!match.Success)
                return string.Empty;

            return Dequote(match.Groups["value"].Value.Trim());
        }

        /// <summary>
        ///     Git quotes the suggested value with `sq_quote_buf()`, which escapes `'` and `!` as `'\''` and
        ///     `'\!'`. Decodes it back to the plain path (see `quote.c` of git).
        /// </summary>
        private static string Dequote(string value)
        {
            if (value.Length > 1 && value[0] == '\'')
            {
                var builder = new StringBuilder(value.Length);
                for (var i = 1; i < value.Length; i++)
                {
                    var c = value[i];
                    if (c != '\'')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (i == value.Length - 1)
                        return builder.ToString();

                    if (i + 3 < value.Length && value[i + 1] == '\\' && (value[i + 2] == '\'' || value[i + 2] == '!') && value[i + 3] == '\'')
                    {
                        builder.Append(value[i + 2]);
                        i += 3;
                        continue;
                    }

                    return string.Empty;
                }

                return string.Empty;
            }

            if (value.Length > 1 && value[0] == '"' && value[^1] == '"')
                return value[1..^1];

            return value;
        }

        private record TrustEntry(string WorkingDirectory, string Value);

        private static readonly StringComparison s_comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        private static readonly List<TrustEntry> s_sessionTrusted = [];

        [GeneratedRegex(@"--add\s+safe\.directory\s+(?<value>[^\r\n]+)", RegexOptions.Multiline)]
        private static partial Regex REG_SAFE_DIRECTORY_HINT();
    }
}
