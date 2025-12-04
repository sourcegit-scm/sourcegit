using System.Text;

namespace SourceGit.Commands
{
    public class Rebase : Command
    {
        public Rebase(string repo, string basedOn, bool autoStash)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(512);
            builder.Append("rebase ");
            if (autoStash)
                builder.Append("--autostash ");

            Args = builder.Append(basedOn).ToString();
        }
    }
}
