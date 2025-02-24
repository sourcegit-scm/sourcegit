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

        public Add(string repo, List<string> changes)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder.Append("add --");
            foreach (var c in changes)
            {
                builder.Append(" \"");
                builder.Append(c);
                builder.Append("\"");
            }
            Args = builder.ToString();
        }

        public Add(string repo, string pathspecFromFile)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"add --pathspec-from-file=\"{pathspecFromFile}\"";
        }
    }
}
