using System.Text;

namespace DevBoard.Commands
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
            builder.Append(url.Quoted()).Append(' ');
            if (!string.IsNullOrEmpty(localName))
                builder.Append(localName.Quoted());

            Args = builder.ToString();
        }
    }
}
