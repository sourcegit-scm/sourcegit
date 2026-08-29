using System;
using System.Collections.Generic;
using System.IO;

namespace SourceGit.Models
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
        private const string MetadataFile = "sourcegit.worktree-base";

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

        public static string ReadPersisted(string gitDir)
        {
            if (string.IsNullOrEmpty(gitDir))
                return string.Empty;

            try
            {
                var path = Path.Combine(gitDir, MetadataFile);
                return File.Exists(path) ? Normalize(File.ReadAllText(path)) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void WritePersisted(string gitDir, string branch)
        {
            var normalized = Normalize(branch);
            if (string.IsNullOrEmpty(gitDir) || GetKind(normalized) == WorktreeBaseBranchKind.None)
                return;

            try
            {
                Directory.CreateDirectory(gitDir);
                File.WriteAllText(Path.Combine(gitDir, MetadataFile), normalized);
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
