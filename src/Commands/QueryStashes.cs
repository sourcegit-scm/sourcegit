using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QueryStashes : Command
    {
        public QueryStashes(string repo)
        {
            _boundary = $"-----BOUNDARY_OF_COMMIT{Guid.NewGuid()}-----";

            WorkingDirectory = repo;
            Context = repo;
            Args = $"stash list --no-show-signature --format=\"%H%n%P%n%ct%n%gd%n%B%n{_boundary}\"";
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
                    default:
                        var boundary = rs.StdOut.IndexOf(_boundary, end + 1, StringComparison.Ordinal);
                        if (boundary > end)
                        {
                            _current.Message = rs.StdOut.Substring(start, boundary - start - 1);
                            end = boundary + _boundary.Length;
                        }
                        else
                        {
                            _current.Message = rs.StdOut.Substring(start);
                            end = rs.StdOut.Length - 2;
                        }

                        nextPartIdx = -1;
                        break;
                }

                nextPartIdx++;

                start = end + 1;
                if (start >= rs.StdOut.Length - 1)
                    break;

                end = rs.StdOut.IndexOf('\n', start);
            }

            return outs;
        }

        private void ParseParent(string data)
        {
            if (data.Length < 8)
                return;

            _current.Parents.AddRange(data.Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries));
        }

        private Models.Stash _current = null;
        private readonly string _boundary;
    }
}
