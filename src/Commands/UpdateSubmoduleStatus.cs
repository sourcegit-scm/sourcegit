using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class UpdateSubmoduleStatus : Command
    {
        [GeneratedRegex(@"^\s?[\w\?]{1,4}\s+(.+)$")]
        private static partial Regex REG_FORMAT();

        public UpdateSubmoduleStatus(string repo, List<Models.Submodule> submodules)
        {
            var pathes = new StringBuilder();
            foreach (var submodule in submodules)
                pathes.Append($"\"{submodule.Path}\" ");

            _submodules = submodules;

            WorkingDirectory = repo;
            Context = repo;
            Args = $"status -uno --porcelain -- {pathes}";
        }

        public void Result()
        {
            Exec();

            foreach (var submodule in _submodules)
                submodule.IsDirty = _changed.Contains(submodule.Path);
        }

        protected override void OnReadline(string line)
        {
            var match = REG_FORMAT().Match(line);
            if (match.Success)
                _changed.Add(match.Groups[1].Value);
        }

        private List<Models.Submodule> _submodules = null;
        private HashSet<string> _changed = new HashSet<string>();
    }
}
