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
            Args = ["reset"];
        }

        public Reset(string repo, List<Models.Change> changes)
        {
            WorkingDirectory = repo;
            Context = repo;

            Args = ["reset", "--"];
            foreach (var c in changes)
            {
                Args.Add(c.Path);
            }
        }

        public Reset(string repo, string revision, string mode)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = ["reset", mode, revision];
        }
    }
}
