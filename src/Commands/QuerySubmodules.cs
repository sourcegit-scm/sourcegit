using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class QuerySubmodules : Command
    {
        [GeneratedRegex(@"^[\-\+ ][0-9a-f]+\s(.*)\s\(.*\)$")]
        private static partial Regex REG_FORMAT1();
        [GeneratedRegex(@"^[\-\+ ][0-9a-f]+\s(.*)$")]
        private static partial Regex REG_FORMAT2();

        public QuerySubmodules(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "submodule status";
        }

        public List<Models.Submodule> Result()
        {
            Exec();
            new UpdateSubmoduleStatus(WorkingDirectory, _submodules).Result();
            return _submodules;
        }

        protected override void OnReadline(string line)
        {
            var match = REG_FORMAT1().Match(line);
            if (match.Success)
            {
                _submodules.Add(new Models.Submodule() { Path = match.Groups[1].Value });
                return;
            }

            match = REG_FORMAT2().Match(line);
            if (match.Success)
            {
                _submodules.Add(new Models.Submodule() { Path = match.Groups[1].Value });
            }
        }

        private readonly List<Models.Submodule> _submodules = new List<Models.Submodule>();
    }
}
