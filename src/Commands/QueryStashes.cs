using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QueryStashes : Command
    {
        public QueryStashes(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "stash list --pretty=format:%H%n%ct%n%gd%n%s";
        }

        public List<Models.Stash> Result()
        {
            Exec();
            return _stashes;
        }

        protected override void OnReadline(string line)
        {
            switch (_nextLineIdx)
            {
                case 0:
                    _current = new Models.Stash() { SHA = line };
                    _stashes.Add(_current);
                    break;
                case 1:
                    _current.Time = ulong.Parse(line);
                    break;
                case 2:
                    _current.Name = line;
                    break;
                case 3:
                    _current.Message = line;
                    break;
            }

            _nextLineIdx++;
            if (_nextLineIdx > 3)
                _nextLineIdx = 0;
        }

        private readonly List<Models.Stash> _stashes = new List<Models.Stash>();
        private Models.Stash _current = null;
        private int _nextLineIdx = 0;
    }
}
