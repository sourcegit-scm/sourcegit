using System;
using System.Collections.Generic;
using System.IO;

namespace DevBoard.Models
{
    public enum WorktreeBaseBranchKind
    {
        None = 0,
        Develop,
        Master,
        Release,
    }

    public readonly record struct WorktreeBaseBranchCandidate(string Branch, int Distance);

    public static class WorktreeBaseBranch
    {
        private const string MetadataFile = "devboard.worktree-base";

        public static string Normalize(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch))
                return string.Empty;

            var normalized = branch.Trim();
            if (normalized.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("refs/heads/".Length);
            else if (normalized.StartsWith("refs/remotes/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("refs/remotes/".Length);

            if (normalized.StartsWith("origin/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("origin/".Length);

            return normalized;
        }

        public static WorktreeBaseBranchKind GetKind(string branch)
        {
            var normalized = Normalize(branch);
            if (string.IsNullOrEmpty(normalized))
                return WorktreeBaseBranchKind.None;

            if (normalized.Equals("develop", StringComparison.OrdinalIgnoreCase))
                return WorktreeBaseBranchKind.Develop;

            if (normalized.Equals("master", StringComparison.OrdinalIgnoreCase))
                return WorktreeBaseBranchKind.Master;

            if (normalized.StartsWith("release/", StringComparison.OrdinalIgnoreCase))
                return WorktreeBaseBranchKind.Release;

            return WorktreeBaseBranchKind.None;
        }

        public static string GetBadgeColor(WorktreeBaseBranchKind kind)
        {
            return kind switch
            {
                WorktreeBaseBranchKind.Develop => "#E5484D",
                WorktreeBaseBranchKind.Master => "#D6409F",
                WorktreeBaseBranchKind.Release => "#F76B15",
                _ => "Transparent",
            };
        }

        public static string ReadPersisted(string gitDir, string currentBranch)
        {
            if (string.IsNullOrEmpty(gitDir) || string.IsNullOrWhiteSpace(currentBranch))
                return string.Empty;

            try
            {
                var path = Path.Combine(gitDir, MetadataFile);
                if (!File.Exists(path))
                    return string.Empty;

                var lines = File.ReadAllLines(path);
                if (lines.Length < 2)
                    return string.Empty;

                var persistedBranch = Normalize(lines[0]);
                var normalizedCurrent = Normalize(currentBranch);
                if (!persistedBranch.Equals(normalizedCurrent, StringComparison.OrdinalIgnoreCase))
                    return string.Empty;

                var baseBranch = Normalize(lines[1]);
                return GetKind(baseBranch) != WorktreeBaseBranchKind.None ? baseBranch : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void WritePersisted(string gitDir, string worktreeBranch, string baseBranch)
        {
            var normalizedWorktreeBranch = Normalize(worktreeBranch);
            var normalizedBaseBranch = Normalize(baseBranch);
            if (string.IsNullOrEmpty(gitDir) ||
                string.IsNullOrEmpty(normalizedWorktreeBranch) ||
                GetKind(normalizedBaseBranch) == WorktreeBaseBranchKind.None)
                return;

            try
            {
                Directory.CreateDirectory(gitDir);
                File.WriteAllLines(Path.Combine(gitDir, MetadataFile), [normalizedWorktreeBranch, normalizedBaseBranch]);
            }
            catch
            {
                // Metadata is optional. Inference will be used on the next open.
            }
        }

        public static string SelectBestCandidate(IEnumerable<WorktreeBaseBranchCandidate> candidates)
        {
            var bestBranch = string.Empty;
            var bestDistance = int.MaxValue;

            foreach (var candidate in candidates)
            {
                var branch = Normalize(candidate.Branch);
                if (GetKind(branch) == WorktreeBaseBranchKind.None)
                    continue;

                if (candidate.Distance < bestDistance)
                {
                    bestBranch = branch;
                    bestDistance = candidate.Distance;
                }
            }

            return bestBranch;
        }
    }
}
