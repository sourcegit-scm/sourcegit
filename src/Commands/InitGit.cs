using System.Text;

namespace SourceGit.Commands
{
    public class InitGit : Command
    {
        public InitGit(string ctx, string path, string localName, string branchName, bool bareRepo)
        {
            Context = ctx;
            WorkingDirectory = path;

            var builder = new StringBuilder(1024);
            builder.Append("init ");
            
            if (bareRepo)
                builder.Append("--bare ");
            else
            { 
                if (!string.IsNullOrEmpty(branchName))
                    builder.Append("-b " + branchName + " ");
            }

            builder.Append(localName.Quoted());

            Args = builder.ToString();
        }
    }
}
