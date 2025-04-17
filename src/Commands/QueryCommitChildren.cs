﻿using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QueryCommitChildren : Command
    {
        public QueryCommitChildren(string repo, string commit, int max)
        {
            WorkingDirectory = repo;
            Context = repo;
            _commit = commit;
            Args = $"rev-list -{max} --parents --branches --remotes --ancestry-path ^{commit}";
        }

        public List<string> Result()
        {
            var rs = ReadToEnd();
            var outs = new List<string>();
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains(_commit))
                        outs.Add(line.Substring(0, 40));
                }
            }

            return outs;
        }

        private string _commit;
    }
}
