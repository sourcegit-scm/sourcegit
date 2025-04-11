﻿using System;
using System.Collections.Generic;

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
            Args = "branch -l --all -v --format=\"%(refname)%00%(objectname)%00%(HEAD)%00%(upstream)%00%(upstream:trackshort)\"";
        }

        public List<Models.Branch> Result()
        {
            var branches = new List<Models.Branch>();
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return branches;

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            var remoteBranches = new HashSet<string>();
            foreach (var line in lines)
            {
                var b = ParseLine(line);
                if (b != null)
                {
                    branches.Add(b);
                    if (!b.IsLocal)
                        remoteBranches.Add(b.FullName);
                }
            }

            foreach (var b in branches)
            {
                if (b.IsLocal && !string.IsNullOrEmpty(b.Upstream))
                    b.IsUpsteamGone = !remoteBranches.Contains(b.Upstream);
            }

            return branches;
        }

        private Models.Branch ParseLine(string line)
        {
            var parts = line.Split('\0');
            if (parts.Length != 5)
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
                var shortNameIdx = name.IndexOf('/', StringComparison.Ordinal);
                if (shortNameIdx < 0)
                    return null;

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
            branch.IsUpsteamGone = false;

            if (branch.IsLocal && !string.IsNullOrEmpty(parts[4]) && !parts[4].Equals("=", StringComparison.Ordinal))
                branch.TrackStatus = new QueryTrackStatus(WorkingDirectory, branch.Name, branch.Upstream).Result();
            else
                branch.TrackStatus = new Models.BranchTrackStatus();

            return branch;
        }
    }
}
