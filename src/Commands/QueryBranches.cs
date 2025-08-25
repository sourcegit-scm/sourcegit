using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryBranches : Command
    {
        private const string PREFIX_LOCAL = "refs/heads/";
        private const string PREFIX_REMOTE = "refs/remotes/";
        private const string PREFIX_DETACHED_AT = "(HEAD detached at";
        private const string PREFIX_DETACHED_FROM = "(HEAD detached from";

        public QueryBranches(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "branch -l --all -v --format=\"%(refname)%00%(committerdate:unix)%00%(objectname)%00%(HEAD)%00%(upstream)%00%(upstream:trackshort)\"";
        }

        public async Task<List<Models.Branch>> GetResultAsync()
        {
            var branches = new List<Models.Branch>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return branches;

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            var remoteHeads = new Dictionary<string, string>();
            foreach (var line in lines)
            {
                var b = ParseLine(line);
                if (b != null)
                {
                    branches.Add(b);
                    if (!b.IsLocal)
                        remoteHeads.Add(b.FullName, b.Head);
                }
            }

            foreach (var b in branches)
            {
                if (b.IsLocal && !string.IsNullOrEmpty(b.Upstream))
                {
                    if (remoteHeads.TryGetValue(b.Upstream, out var upstreamHead))
                    {
                        b.IsUpstreamGone = false;
                        b.TrackStatus ??= await new QueryTrackStatus(WorkingDirectory, b.Head, upstreamHead).GetResultAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        b.IsUpstreamGone = true;
                        b.TrackStatus ??= new Models.BranchTrackStatus();
                    }
                }
            }

            return branches;
        }

        private Models.Branch ParseLine(string line)
        {
            var parts = line.Split('\0');
            if (parts.Length != 6)
                return null;

            var branch = new Models.Branch();
            var refName = parts[0];
            if (refName.EndsWith("/HEAD", StringComparison.Ordinal))
                return null;

            branch.IsDetachedHead = refName.StartsWith(PREFIX_DETACHED_AT, StringComparison.Ordinal) ||
                refName.StartsWith(PREFIX_DETACHED_FROM, StringComparison.Ordinal);

            if (refName.StartsWith(PREFIX_LOCAL, StringComparison.Ordinal))
            {
                branch.Name = refName.Substring(PREFIX_LOCAL.Length);
                branch.IsLocal = true;
            }
            else if (refName.StartsWith(PREFIX_REMOTE, StringComparison.Ordinal))
            {
                var name = refName.Substring(PREFIX_REMOTE.Length);
                var nameParts = name.Split('/', 2);
                if (nameParts.Length != 2)
                    return null;

                branch.Remote = nameParts[0];
                branch.Name = nameParts[1];
                branch.IsLocal = false;
            }
            else
            {
                branch.Name = refName;
                branch.IsLocal = true;
            }

            ulong.TryParse(parts[1], out var committerDate);

            branch.FullName = refName;
            branch.CommitterDate = committerDate;
            branch.Head = parts[2];
            branch.IsCurrent = parts[3] == "*";
            branch.Upstream = parts[4];
            branch.IsUpstreamGone = false;

            if (!branch.IsLocal ||
                string.IsNullOrEmpty(branch.Upstream) ||
                string.IsNullOrEmpty(parts[5]) ||
                parts[5].Equals("=", StringComparison.Ordinal))
                branch.TrackStatus = new Models.BranchTrackStatus();

            return branch;
        }
    }
}
