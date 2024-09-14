using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class QueryRemotes : Command
    {
        [GeneratedRegex(@"^([\w\.\-]+)\s*(\S+).*$")]
        private static partial Regex REG_REMOTE();

        public QueryRemotes(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = ["remote", "-v"];
        }

        public List<Models.Remote> Result()
        {
            Exec();
            return _loaded;
        }

        protected override void OnReadline(string line)
        {
            var match = REG_REMOTE().Match(line);
            if (!match.Success)
                return;

            var remote = new Models.Remote()
            {
                Name = match.Groups[1].Value,
                URL = match.Groups[2].Value,
            };

            if (_loaded.Find(x => x.Name == remote.Name) != null)
                return;
            _loaded.Add(remote);
        }

        private readonly List<Models.Remote> _loaded = new List<Models.Remote>();
    }
}
