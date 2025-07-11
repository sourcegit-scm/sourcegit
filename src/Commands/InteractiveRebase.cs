namespace SourceGit.Commands
{
    public class InteractiveRebase : Command
    {
        public InteractiveRebase(string repo, string basedOn, bool autoStash)
        {
            WorkingDirectory = repo;
            Context = repo;
            Editor = EditorType.RebaseEditor;
            Args = "rebase -i --autosquash ";
            if (autoStash)
                Args += "--autostash ";
            Args += basedOn;
        }
    }
}
