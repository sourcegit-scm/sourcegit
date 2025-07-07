using System.Text;

namespace SourceGit.Commands
{
    public class Move : Command
    {
        public Move(string repo, string oldPath, string newPath, bool force)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder.Append("mv -v ");
            if (force)
                builder.Append("-f ");
            builder.Append('"');
            builder.Append(oldPath);
            builder.Append("\" \"");
            builder.Append(newPath);
            builder.Append('"');

            Args = builder.ToString();
        }
    }
}
