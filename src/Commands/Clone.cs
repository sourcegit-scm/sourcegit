using System.Text;

namespace SourceGit.Commands
{
    public class Clone : Command
    {
        public Clone(string ctx, string path, string url, string localName, string sshKey, string extraArgs)
        {
            Context = ctx;
            WorkingDirectory = path;
            SSHKey = sshKey;

            var builder = new StringBuilder(1024);
            builder.Append("clone --progress --verbose ");
            if (!string.IsNullOrEmpty(extraArgs))
                builder.Append(extraArgs).Append(' ');
            builder.Append(url).Append(' ');
            if (!string.IsNullOrEmpty(localName))
                builder.Append(localName);

            Args = builder.ToString();
        }
    }
}
