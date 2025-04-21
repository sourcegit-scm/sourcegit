using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QueryStashes : Command
    {
        public QueryStashes(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "stash list --format=%H%n%P%n%ct%n%gd%n%s";
        }

        public List<Models.Stash> Result()
        {
            var outs = new List<Models.Stash>();
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return outs;

            var nextPartIdx = 0;
            var start = 0;
            var end = rs.StdOut.IndexOf('\n', start);
            while (end > 0)
            {
                var line = rs.StdOut.Substring(start, end - start);

                switch (nextPartIdx)
                {
                    case 0:
                        _current = new Models.Stash() { SHA = line };
                        outs.Add(_current);
                        break;
                    case 1:
                        ParseParent(line);
                        break;
                    case 2:
                        _current.Time = ulong.Parse(line);
                        break;
                    case 3:
                        _current.Name = line;
                        break;
                    case 4:
                        _current.Message = line;
                        break;
                }

                nextPartIdx++;
                if (nextPartIdx > 4)
                    nextPartIdx = 0;

                start = end + 1;
                end = rs.StdOut.IndexOf('\n', start);
            }

            if (start < rs.StdOut.Length)
                _current.Message = rs.StdOut.Substring(start);

            return outs;
        }

        private void ParseParent(string data)
        {
            if (data.Length < 8)
                return;

            _current.Parents.AddRange(data.Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries));
        }

        private Models.Stash _current = null;
    }
}
