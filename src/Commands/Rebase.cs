using System.Text;

namespace SourceGit.Commands
{
    public class Rebase : Command
    {
        public Rebase(string repo, string basedOn, bool autoStash, bool noVerify)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(512);
            builder.Append("rebase ");
            if (autoStash)
                builder.Append("--autostash ");
            if (noVerify)
                builder.Append("--no-verify ");

            Args = builder.Append(basedOn).ToString();
        }
    }
}
