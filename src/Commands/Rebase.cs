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

    public class InteractiveRebase : Command
    {
        public InteractiveRebase(string repo, string basedOn)
        {
            WorkingDirectory = repo;
            Context = repo;
            Editor = EditorType.RebaseEditor;
            Args = $"rebase -i --autosquash {basedOn}";
        }
    }
}
