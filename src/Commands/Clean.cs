using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class Clean : Command
    {
        public Clean(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = ["clean", "-qfd"];
        }

        public Clean(string repo, List<string> files)
        {
            Args = ["clean", "-qfd", "--", ..files];

            WorkingDirectory = repo;
            Context = repo;
        }
    }
}
