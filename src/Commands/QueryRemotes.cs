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
            Args = "remote -v";
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
                URL = match.Groups[0].Value.Contains("fetch") ? match.Groups[2].Value : "",
                PushURL = match.Groups[0].Value.Contains("push") ? match.Groups[2].Value : "",
            };
            var alreadyFound = _loaded.Find(x => x.Name == remote.Name);
            if (alreadyFound != null && !string.IsNullOrEmpty(remote.PushURL))
            {
                alreadyFound.PushURL = alreadyFound.URL != remote.PushURL ? remote.PushURL : "";
            }

            if (alreadyFound != null)
                return;
            _loaded.Add(remote);
        }

        private readonly List<Models.Remote> _loaded = new List<Models.Remote>();
    }
}
