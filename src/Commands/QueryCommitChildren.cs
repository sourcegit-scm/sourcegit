using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QueryCommitChildren : Command
    {
        public QueryCommitChildren(string repo, string commit, string filters)
        {
            WorkingDirectory = repo;
            Context = repo;
            _commit = commit;
            if (string.IsNullOrEmpty(filters))
                filters = "--all";
            Args = $"rev-list --parents {filters} ^{commit}";
        }

        public IEnumerable<string> Result()
        {
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                yield break;

            foreach (string s in rs.StdOut.Split('\n', StringSplitOptions.None))
            {
                if (s.Contains(_commit))
                    yield return s.Substring(0, 40);
            }
        }

        private string _commit;
    }
}
