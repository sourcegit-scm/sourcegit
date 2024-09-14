using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class Restore : Command
    {
        public Restore(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = ["restore", ".", "--source=HEAD", "--staged", "--worktree", "--recurse-submodules"];
        }

        public Restore(string repo, IEnumerable<string> files, IEnumerable<string> extra)
        {
            WorkingDirectory = repo;
            Context = repo;

            Args = ["restore", ..extra, "--", ..files];
        }
    }
}
