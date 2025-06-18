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
            Args = $"stash list -z --no-show-signature --format=\"%H%n%P%n%ct%n%gd%n%B\"";
        }

        public List<Models.Stash> Result()
        {
            var outs = new List<Models.Stash>();
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return outs;

            var items = rs.StdOut.Split('\0', System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in items)
            {
                var current = new Models.Stash();

                var nextPartIdx = 0;
                var start = 0;
                var end = item.IndexOf('\n', start);
                while (end > 0 && nextPartIdx < 4)
                {
                    var line = item.Substring(start, end - start);

                    switch (nextPartIdx)
                    {
                        case 0:
                            current.SHA = line;
                            break;
                        case 1:
                            ParseParent(line, ref current);
                            break;
                        case 2:
                            current.Time = ulong.Parse(line);
                            break;
                        case 3:
                            current.Name = line;
                            break;
                    }

                    nextPartIdx++;

                    start = end + 1;
                    if (start >= item.Length - 1)
                        break;

                    end = item.IndexOf('\n', start);
                }

                if (start < item.Length)
                    current.Message = item.Substring(start);

                outs.Add(current);
            }
            return outs;
        }

        private void ParseParent(string data, ref Models.Stash current)
        {
            if (data.Length < 8)
                return;

            current.Parents.AddRange(data.Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
