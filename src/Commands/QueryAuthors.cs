using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryAuthors : Command
    {
        public QueryAuthors(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;
            Args = "log -100000 --all --format=%aN±%aE";
        }

        public async Task<List<Models.User>> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return [];

            var start = 0;
            var end = rs.StdOut.IndexOf('\n', start);
            var added = new HashSet<string>();
            var users = new List<Models.User>();
            while (end > 0)
            {
                var line = rs.StdOut.Substring(start, end - start);
                if (!string.IsNullOrEmpty(line) && !added.Contains(line))
                {
                    var user = Models.User.FindOrAdd(line);
                    users.Add(user);
                    added.Add(line);
                }

                start = end + 1;
                if (start >= rs.StdOut.Length - 1)
                    break;

                end = rs.StdOut.IndexOf('\n', start);
            }

            return users;
        }
    }
}
