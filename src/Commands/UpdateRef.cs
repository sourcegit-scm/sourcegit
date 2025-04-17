namespace SourceGit.Commands
{
    public class UpdateRef : Command
    {
        public UpdateRef(string repo, string refName, string toRevision)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"update-ref {refName} {toRevision}";
        }
    }
}
