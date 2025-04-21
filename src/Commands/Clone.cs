namespace SourceGit.Commands
{
    public class Clone : Command
    {
        public Clone(string ctx, string path, string url, string localName, string sshKey, string extraArgs)
        {
            Context = ctx;
            WorkingDirectory = path;
            SSHKey = sshKey;
            Args = "clone --progress --verbose ";

            if (!string.IsNullOrEmpty(extraArgs))
                Args += $"{extraArgs} ";

            Args += $"{url} ";

            if (!string.IsNullOrEmpty(localName))
                Args += localName;
        }
    }
}
