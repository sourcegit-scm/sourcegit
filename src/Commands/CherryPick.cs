namespace SourceGit.Commands
{
    public class CherryPick : Command
    {
        public CherryPick(string repo, string commits, bool noCommit, bool appendSourceToMessage, string extraParams)
        {
            WorkingDirectory = repo;
            Context = repo;

            Args = "cherry-pick ";
            if (noCommit)
                Args += "-n ";
            if (appendSourceToMessage)
                Args += "-x ";
            if (!string.IsNullOrEmpty(extraParams))
                Args += $"{extraParams} ";
            Args += commits;
        }
    }
}
