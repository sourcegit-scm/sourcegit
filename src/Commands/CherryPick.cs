using System.Text;

namespace SourceGit.Commands
{
    public class CherryPick : Command
    {
        public CherryPick(string repo, string commits, bool noCommit, bool appendSourceToMessage, string extraParams)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder("cherry-pick ");
            if (noCommit)
                builder.Append("-n ");
            if (appendSourceToMessage)
                builder.Append("-x ");
            if (!string.IsNullOrEmpty(extraParams))
                builder.Append(extraParams).Append(' ');
            Args = builder.Append(commits).ToString();
        }
    }
}
