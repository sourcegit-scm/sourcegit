using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class Clean : Command
    {
        public Clean(string repo, bool includeIgnored)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = includeIgnored ? "clean -qfdx" : "clean -qfd";
        }

        public Clean(string repo, List<string> files)
        {
            var builder = new StringBuilder();
            builder.Append("clean -qfd --");
            foreach (var f in files)
            {
                builder.Append(" \"");
                builder.Append(f);
                builder.Append("\"");
            }

            WorkingDirectory = repo;
            Context = repo;
            Args = builder.ToString();
        }
    }
}
