using System.Text;

namespace SourceGit.Commands
{
    public class Restore : Command
    {
        public Restore(string repo, string pathspecFile, bool isStaged)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder.Append("restore --progress ");
            builder.Append(isStaged ? "--staged " : "--worktree --recurse-submodules ");
            builder.Append("--pathspec-from-file=").Append(pathspecFile.Quoted());

            Args = builder.ToString();
        }
    }
}
