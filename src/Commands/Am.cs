using System.Text;

namespace SourceGit.Commands
{
    public class Am : Command
    {
        public Am(string repo, string file, bool ignoreWhitespace, string whitespaceMode, string extra)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(1024);
            builder.Append("am ");

            if (ignoreWhitespace)
                builder.Append("--ignore-whitespace ");
            else
                builder.Append("--whitespace=").Append(whitespaceMode).Append(' ');

            if (!string.IsNullOrEmpty(extra))
                builder.Append(extra).Append(' ');

            Args = builder.Append(file.Quoted()).ToString();
        }
    }
}
