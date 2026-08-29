using System;
using System.Collections.Generic;

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

        public string GetResult()
        {
            Args = "for-each-ref --format=\"%(refname:short)\" refs/heads";
            var refs = ReadToEnd();
            if (!refs.IsSuccess)
                return string.Empty;

            var candidates = new List<Models.WorktreeBaseBranchCandidate>();
            var lines = refs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var branch = Models.WorktreeBaseBranch.Normalize(line);
                if (Models.WorktreeBaseBranch.GetKind(branch) == Models.WorktreeBaseBranchKind.None)
                    continue;

                Args = $"merge-base --is-ancestor {branch.Quoted()} HEAD";
                if (!ReadToEnd().IsSuccess)
                    continue;

                Args = $"rev-list --count {branch.Quoted()}..HEAD";
                var distance = ReadToEnd();
                if (distance.IsSuccess && int.TryParse(distance.StdOut.Trim(), out var count))
                    candidates.Add(new Models.WorktreeBaseBranchCandidate(branch, count));
            }

            return Models.WorktreeBaseBranch.SelectBestCandidate(candidates);
        }
    }
}
