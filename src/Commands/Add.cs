using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class Add : Command
    {
        public Add(string repo, List<Models.Change> changes = null)
        {
            WorkingDirectory = repo;
            Context = repo;

            if (changes == null || changes.Count == 0)
            {
                Args = "add .";
            }
            else
            {
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
}