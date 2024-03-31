namespace SourceGit.Commands
{
    public class Rebase : Command
    {
        public Rebase(string repo, string basedOn, bool autoStash)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "rebase ";
            if (autoStash)
                Args += "--autostash ";
            Args += basedOn;
        }
    }
}
