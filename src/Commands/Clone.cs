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

            var builder = new StringBuilder("clone --progress --verbose ");
            if (!string.IsNullOrEmpty(extraArgs))
                builder.Append(extraArgs).Append(' ');
            builder.Append(url);
            if (!string.IsNullOrEmpty(localName))
                builder.Append(' ').Append(localName);
            Args = builder.ToString();
        }
    }
}
