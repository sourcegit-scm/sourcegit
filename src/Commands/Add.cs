using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class Add : Command
    {
        public Add(string repo, bool includeUntracked)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = includeUntracked ? "add ." : "add -u .";
        }

        public Add(string repo, List<Models.Change> changes)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder.Append("add --");
            foreach (var c in changes)
            {
                builder.Append(" \"");
                builder.Append(c.Path);
                builder.Append("\"");
            }
            Args = builder.ToString();
        }
    }
}
