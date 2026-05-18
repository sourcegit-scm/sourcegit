using System.Text;

namespace SourceGit.Commands
{
    public class InteractiveRebase : Command
    {
        public InteractiveRebase(string repo, string basedOn, bool autoStash, bool noVerify)
        {
            WorkingDirectory = repo;
            Context = repo;
            Editor = EditorType.RebaseEditor;

            var builder = new StringBuilder(512);
            builder.Append("rebase -i --autosquash ");
            if (autoStash)
                builder.Append("--autostash ");
            if (noVerify)
                builder.Append("--no-verify ");

            Args = builder.Append(basedOn).ToString();
        }
    }
}
