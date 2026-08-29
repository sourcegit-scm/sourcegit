using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryWorktreeBaseBranch : Command
    {
        public QueryWorktreeBaseBranch(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;
        }

        public async Task<string> GetResultAsync(string currentBranch = null)
        {
            if (!await IsLinkedWorktreeAsync().ConfigureAwait(false))
                return string.Empty;

            currentBranch = string.IsNullOrEmpty(currentBranch)
                ? await GetCurrentBranchAsync().ConfigureAwait(false)
                : Models.WorktreeBaseBranch.Normalize(currentBranch);

            Args = "for-each-ref --format=\"%(refname:short)\" refs/heads";
            var refs = await ReadToEndAsync().ConfigureAwait(false);
            if (!refs.IsSuccess)
                return string.Empty;

            var candidates = new List<Models.WorktreeBaseBranchCandidate>();
            var lines = refs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var branch = Models.WorktreeBaseBranch.Normalize(line);
                if (Models.WorktreeBaseBranch.GetKind(branch) == Models.WorktreeBaseBranchKind.None ||
                    branch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
                    continue;

                Args = $"merge-base --is-ancestor {branch.Quoted()} HEAD";
                if (!(await ReadToEndAsync().ConfigureAwait(false)).IsSuccess)
                    continue;

                Args = $"rev-list --count {branch.Quoted()}..HEAD";
                var distance = await ReadToEndAsync().ConfigureAwait(false);
                if (distance.IsSuccess && int.TryParse(distance.StdOut.Trim(), out var count))
                    candidates.Add(new Models.WorktreeBaseBranchCandidate(branch, count));
            }

            return Models.WorktreeBaseBranch.SelectBestCandidate(candidates);
        }

        public async Task<string> GetCurrentBranchAsync()
        {
            Args = "symbolic-ref --quiet --short HEAD";
            var result = await ReadToEndAsync().ConfigureAwait(false);
            return result.IsSuccess ? Models.WorktreeBaseBranch.Normalize(result.StdOut.Trim()) : string.Empty;
        }

        public async Task<string> GetGitDirAsync()
        {
            Args = "rev-parse --absolute-git-dir";
            var result = await ReadToEndAsync().ConfigureAwait(false);
            return result.IsSuccess ? result.StdOut.Trim() : string.Empty;
        }

        private async Task<bool> IsLinkedWorktreeAsync()
        {
            var gitDir = await GetGitDirAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(gitDir))
                return false;

            Args = "rev-parse --path-format=absolute --git-common-dir";
            var common = await ReadToEndAsync().ConfigureAwait(false);
            if (!common.IsSuccess || string.IsNullOrWhiteSpace(common.StdOut))
                return false;

            try
            {
                var absoluteGitDir = Path.GetFullPath(gitDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var absoluteCommonDir = Path.GetFullPath(common.StdOut.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return !absoluteGitDir.Equals(absoluteCommonDir, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }
    }
}
