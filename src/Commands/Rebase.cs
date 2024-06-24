using System.Diagnostics;

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
            var exec = Process.GetCurrentProcess().MainModule.FileName;
            var editor = $"\\\"{exec}\\\" --rebase-editor";

            WorkingDirectory = repo;
            Context = repo;
            Args = $"-c core.editor=\"{editor}\" -c sequence.editor=\"{editor}\" -c rebase.abbreviateCommands=true rebase -i --autosquash {basedOn}";
        }
    }
}
