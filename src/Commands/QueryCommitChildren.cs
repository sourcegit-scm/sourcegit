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

        protected override void OnReadline(string line)
        {
            if (line.Contains(_commit))
                _lines.Add(line);
        }

        public IEnumerable<string> Result()
        {
            Exec();
            return _lines;
        }

        private string _commit;
        private List<string> _lines = new List<string>();
    }
}
