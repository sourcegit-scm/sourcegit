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
            Args = $"rev-list -{max} --parents --branches --remotes ^{commit}";
        }

        public List<string> Result()
        {
            Exec();
            return _lines;
        }

        protected override void OnReadline(string line)
        {
            if (line.Contains(_commit))
                _lines.Add(line.Substring(0, 40));
        }

        private string _commit;
        private List<string> _lines = new List<string>();
    }
}
