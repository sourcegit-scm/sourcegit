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
                Args = ["add", "."];
            }
            else
            {
                Args.AddRange(["add", "--"]);

                foreach (var c in changes)
                    Args.Add(c.Path);
            }
        }
    }
}
