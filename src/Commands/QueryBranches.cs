using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QueryBranches : Command
    {
        private const string PREFIX_LOCAL = "refs/heads/";
        private const string PREFIX_REMOTE = "refs/remotes/";
        private const string PREFIX_DETACHED = "(HEAD detached at";

        public QueryBranches(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "branch -l --all -v --format=\"%(refname)$%(objectname)$%(HEAD)$%(upstream)$%(upstream:trackshort)\"";
        }

        public List<Models.Branch> Result()
        {
            Exec();

            foreach (var b in _needQueryTrackStatus)
                b.TrackStatus = new QueryTrackStatus(WorkingDirectory, b.Name, b.Upstream).Result();

            return _branches;
        }

        protected override void OnReadline(string line)
        {
            var parts = line.Split('$');
            if (parts.Length != 5)
                return;

            var branch = new Models.Branch();
            var refName = parts[0];
            if (refName.EndsWith("/HEAD", StringComparison.Ordinal))
                return;

            if (refName.StartsWith(PREFIX_DETACHED, StringComparison.Ordinal))
            {
                branch.IsDetachedHead = true;
            }

            if (refName.StartsWith(PREFIX_LOCAL, StringComparison.Ordinal))
            {
                branch.Name = refName.Substring(PREFIX_LOCAL.Length);
                branch.IsLocal = true;
            }
            else if (refName.StartsWith(PREFIX_REMOTE, StringComparison.Ordinal))
            {
                var name = refName.Substring(PREFIX_REMOTE.Length);
                var shortNameIdx = name.IndexOf('/', StringComparison.Ordinal);
                if (shortNameIdx < 0)
                    return;

                branch.Remote = name.Substring(0, shortNameIdx);
                branch.Name = name.Substring(branch.Remote.Length + 1);
                branch.IsLocal = false;
            }
            else
            {
                branch.Name = refName;
                branch.IsLocal = true;
            }

            branch.FullName = refName;
            branch.Head = parts[1];
            branch.IsCurrent = parts[2] == "*";
            branch.Upstream = parts[3];

            if (branch.IsLocal && !string.IsNullOrEmpty(parts[4]) && !parts[4].Equals("=", StringComparison.Ordinal))
                _needQueryTrackStatus.Add(branch);
            else
                branch.TrackStatus = new Models.BranchTrackStatus();

            _branches.Add(branch);
        }

        private readonly List<Models.Branch> _branches = new List<Models.Branch>();
        private List<Models.Branch> _needQueryTrackStatus = new List<Models.Branch>();
    }
}
