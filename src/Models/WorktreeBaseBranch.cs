using System;
using System.Collections.Generic;

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
        public static WorktreeBaseBranchKind GetKind(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch))
                return WorktreeBaseBranchKind.None;

            if (branch.Equals("develop", StringComparison.OrdinalIgnoreCase))
                return WorktreeBaseBranchKind.Develop;

            if (branch.Equals("master", StringComparison.OrdinalIgnoreCase))
                return WorktreeBaseBranchKind.Master;

            if (branch.StartsWith("release/", StringComparison.OrdinalIgnoreCase))
                return WorktreeBaseBranchKind.Release;

            return WorktreeBaseBranchKind.None;
        }

        public static string SelectBestCandidate(IEnumerable<WorktreeBaseBranchCandidate> candidates)
        {
            var bestBranch = string.Empty;
            var bestDistance = int.MaxValue;

            foreach (var candidate in candidates)
            {
                if (GetKind(candidate.Branch) == WorktreeBaseBranchKind.None)
                    continue;

                if (candidate.Distance < bestDistance)
                {
                    bestBranch = candidate.Branch;
                    bestDistance = candidate.Distance;
                }
            }

            return bestBranch;
        }
    }
}
