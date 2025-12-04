using System.Text;

namespace SourceGit.Commands
{
    public class Revert : Command
    {
        public Revert(string repo, string commit, bool autoCommit)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(512);
            builder
                .Append("revert -m 1 ")
                .Append(commit)
                .Append(" --no-edit");
            if (!autoCommit)
                builder.Append(" --no-commit");

            Args = builder.ToString();
        }
    }
}
