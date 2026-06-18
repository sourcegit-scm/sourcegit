using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.AI
{
    public class AIDiffContextBuilder
    {
        private const int MAX_CHAR_COUNT = 100_000;
        private const int MAX_LINE_COUNT = 3_000;
        private const int MAX_FILE_DIFF_CHARS = 20_000;

        public async Task<AIDiffContextData> CollectWorkingTreeAsync(string repoPath)
        {
            var data = new AIDiffContextData
            {
                Scenario = DiffScenario.WorkingTree
            };

            var unstagedStat = await RunGitAsync(repoPath, "diff --stat");
            var stagedStat = await RunGitAsync(repoPath, "diff --cached --stat");
            data.UnstagedStatText = unstagedStat;
            data.StagedStatText = stagedStat;
            data.DiffStatText = MergeStat(stagedStat, unstagedStat);

            data.UnstagedNameStatus = await RunGitAsync(repoPath, "diff --name-status");
            data.StagedNameStatus = await RunGitAsync(repoPath, "diff --cached --name-status");
            data.NameStatusText = MergeNameStatus(data.StagedNameStatus, data.UnstagedNameStatus);

            var unstagedPatch = await RunGitAsync(repoPath, "diff --no-color --no-ext-diff --full-index --patch");
            var stagedPatch = await RunGitAsync(repoPath, "diff --cached --no-color --no-ext-diff --full-index --patch");

            var combined = CombinePatches(stagedPatch, unstagedPatch);
            combined = RemoveBinaryAndLFSSections(combined, data.SkippedBinaryFiles);
            combined = RemoveLargeFileSections(combined, data.SkippedLargeFiles);

            if (combined.Length > MAX_CHAR_COUNT || CountLines(combined) > MAX_LINE_COUNT)
            {
                data.FullDiffText = string.Empty;
                data.IsTruncated = true;
            }
            else
            {
                data.FullDiffText = combined;
            }

            ParseStatSummary(data);
            return data;
        }

        public async Task<AIDiffContextData> CollectCommitRangeAsync(string repoPath, string fromSHA, string toSHA)
        {
            if (!IsValidSHA(fromSHA))
                throw new ArgumentException($"Invalid from SHA: {fromSHA}");
            if (!IsValidSHA(toSHA))
                throw new ArgumentException($"Invalid to SHA: {toSHA}");

            var data = new AIDiffContextData
            {
                Scenario = DiffScenario.CommitRange,
                FromSHA = fromSHA,
                ToSHA = toSHA
            };

            data.CommitLogText = await RunGitAsync(repoPath, $"log --pretty=format:\"%h %s\" {fromSHA}..{toSHA}");
            data.DiffStatText = await RunGitAsync(repoPath, $"diff --stat {fromSHA} {toSHA}");
            data.NameStatusText = await RunGitAsync(repoPath, $"diff --name-status {fromSHA} {toSHA}");

            var patch = await RunGitAsync(repoPath, $"diff --no-color --no-ext-diff --full-index --patch {fromSHA} {toSHA}");
            patch = RemoveBinaryAndLFSSections(patch, data.SkippedBinaryFiles);
            patch = RemoveLargeFileSections(patch, data.SkippedLargeFiles);

            if (patch.Length > MAX_CHAR_COUNT || CountLines(patch) > MAX_LINE_COUNT)
            {
                data.FullDiffText = string.Empty;
                data.IsTruncated = true;
            }
            else
            {
                data.FullDiffText = patch;
            }

            ParseStatSummary(data);
            return data;
        }

        public async Task<(string fromSHA, string toSHA)> ResolveCommitDirectionAsync(string repoPath, string shaA, string shaB)
        {
            if (!IsValidSHA(shaA) || !IsValidSHA(shaB))
                return (shaA, shaB);

            var exitA = await RunGitExitCodeAsync(repoPath, $"merge-base --is-ancestor {shaA} {shaB}");
            if (exitA == 0)
                return (shaA, shaB);

            var exitB = await RunGitExitCodeAsync(repoPath, $"merge-base --is-ancestor {shaB} {shaA}");
            if (exitB == 0)
                return (shaB, shaA);

            return (shaA, shaB);
        }

        private async Task<string> RunGitAsync(string repoPath, string args)
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = Native.OS.GitExecutable,
                Arguments = $"--no-pager -c core.quotepath=off {args}",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync();
            var error = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            if (proc.ExitCode != 0 && !string.IsNullOrEmpty(error))
                return string.Empty;
            return output ?? string.Empty;
        }

        private async Task<int> RunGitExitCodeAsync(string repoPath, string args)
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = Native.OS.GitExecutable,
                Arguments = $"--no-pager {args}",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            proc.Start();
            await proc.WaitForExitAsync();
            return proc.ExitCode;
        }

        private static bool IsValidSHA(string sha)
        {
            if (string.IsNullOrEmpty(sha))
                return false;
            if (sha.Length < 6 || sha.Length > 64)
                return false;
            foreach (var c in sha)
                if (!char.IsAsciiHexDigit(c))
                    return false;
            return true;
        }

        private static string MergeStat(string staged, string unstaged)
        {
            if (string.IsNullOrEmpty(staged) && string.IsNullOrEmpty(unstaged))
                return string.Empty;
            if (string.IsNullOrEmpty(staged))
                return unstaged;
            if (string.IsNullOrEmpty(unstaged))
                return staged;
            return staged.TrimEnd() + "\n" + unstaged.TrimEnd();
        }

        private static string MergeNameStatus(string staged, string unstaged)
        {
            if (string.IsNullOrEmpty(staged) && string.IsNullOrEmpty(unstaged))
                return string.Empty;
            if (string.IsNullOrEmpty(staged))
                return unstaged;
            if (string.IsNullOrEmpty(unstaged))
                return staged;
            return staged.TrimEnd() + "\n" + unstaged.TrimEnd();
        }

        private static string CombinePatches(string staged, string unstaged)
        {
            if (string.IsNullOrEmpty(staged) && string.IsNullOrEmpty(unstaged))
                return string.Empty;
            if (string.IsNullOrEmpty(staged))
                return unstaged;
            if (string.IsNullOrEmpty(unstaged))
                return staged;

            var builder = new StringBuilder();
            builder.AppendLine("=== STAGED CHANGES ===");
            builder.Append(staged.TrimEnd());
            builder.AppendLine();
            builder.AppendLine("=== UNSTAGED CHANGES ===");
            builder.Append(unstaged.TrimEnd());
            return builder.ToString();
        }

        private static string RemoveBinaryAndLFSSections(string patch, List<string> skipped)
        {
            if (string.IsNullOrEmpty(patch))
                return patch;

            var lines = patch.Split('\n');
            var result = new List<string>();
            var inBinary = false;
            var inLFS = false;
            var currentFile = string.Empty;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.StartsWith("diff --git ", StringComparison.Ordinal))
                {
                    inBinary = false;
                    inLFS = false;

                    var parts = line.Split(' ');
                    if (parts.Length >= 3)
                        currentFile = parts[3].TrimStart("a/".ToCharArray());
                    else
                        currentFile = string.Empty;
                }

                if (line.StartsWith("Binary", StringComparison.Ordinal))
                {
                    inBinary = true;
                    if (!string.IsNullOrEmpty(currentFile))
                        skipped.Add(currentFile);
                    continue;
                }

                if (line.Contains("version https://git-lfs.github.com/spec/", StringComparison.Ordinal))
                {
                    inLFS = true;
                    if (!string.IsNullOrEmpty(currentFile) && !skipped.Contains(currentFile))
                        skipped.Add(currentFile);
                    continue;
                }

                if (inBinary || inLFS)
                {
                    if (line.StartsWith("diff --git ", StringComparison.Ordinal))
                    {
                        inBinary = false;
                        inLFS = false;
                        result.Add(line);
                    }
                    continue;
                }

                result.Add(line);
            }

            return string.Join("\n", result);
        }

        private static string RemoveLargeFileSections(string patch, List<string> skipped)
        {
            if (string.IsNullOrEmpty(patch))
                return patch;

            var lines = patch.Split('\n');
            var result = new List<string>();
            var currentFile = string.Empty;
            var sectionStart = -1;
            var sectionChars = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.StartsWith("diff --git ", StringComparison.Ordinal))
                {
                    if (sectionStart >= 0 && sectionChars > MAX_FILE_DIFF_CHARS)
                    {
                        for (int j = sectionStart; j < i; j++)
                        {
                            if (result.Count > 0)
                                result.RemoveAt(result.Count - 1);
                        }
                        if (!string.IsNullOrEmpty(currentFile))
                            skipped.Add(currentFile);
                    }

                    var parts = line.Split(' ');
                    currentFile = parts.Length >= 4 ? parts[3].TrimStart("a/".ToCharArray()) : string.Empty;
                    sectionStart = result.Count;
                    sectionChars = 0;
                    result.Add(line);
                    continue;
                }

                result.Add(line);
                sectionChars += line.Length + 1;

                if (i == lines.Length - 1 && sectionStart >= 0 && sectionChars > MAX_FILE_DIFF_CHARS)
                {
                    for (int j = sectionStart; j < result.Count; )
                    {
                        if (result.Count > 0)
                            result.RemoveAt(result.Count - 1);
                    }
                    if (!string.IsNullOrEmpty(currentFile))
                        skipped.Add(currentFile);
                }
            }

            return string.Join("\n", result);
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            var count = 1;
            foreach (var c in text)
                if (c == '\n')
                    count++;
            return count;
        }

        private static void ParseStatSummary(AIDiffContextData data)
        {
            var text = data.DiffStatText;
            if (string.IsNullOrEmpty(text))
                return;

            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains(" file changed", StringComparison.Ordinal) ||
                    line.Contains(" files changed", StringComparison.Ordinal))
                {
                    var parts = line.Split(',');
                    foreach (var part in parts)
                    {
                        var trimmed = part.Trim();
                        if (trimmed.Contains(" file", StringComparison.Ordinal))
                        {
                            var numStr = trimmed.Split(' ')[0];
                            if (int.TryParse(numStr, out var files))
                                data.TotalFiles = files;
                        }
                        else if (trimmed.Contains(" insertion", StringComparison.Ordinal))
                        {
                            var numStr = trimmed.Split(' ')[0];
                            if (int.TryParse(numStr, out var ins))
                                data.TotalInsertions = ins;
                        }
                        else if (trimmed.Contains(" deletion", StringComparison.Ordinal))
                        {
                            var numStr = trimmed.Split(' ')[0];
                            if (int.TryParse(numStr, out var del))
                                data.TotalDeletions = del;
                        }
                    }
                    break;
                }
            }
        }
    }
}
