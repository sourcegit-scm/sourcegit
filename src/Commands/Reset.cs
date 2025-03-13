using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class Reset : Command
    {
        public Reset(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "reset";
        }

        public Reset(string repo, List<Models.Change> changes)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder.Append("reset --");
            foreach (var c in changes)
            {
                builder.Append(" \"");
                builder.Append(c.Path);
                builder.Append("\"");
            }
            Args = builder.ToString();
        }

        public Reset(string repo, string revision, string mode)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"reset {mode} {revision}";
        }
    }
}
