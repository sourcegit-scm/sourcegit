using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class Remove : Command
    {
        public Remove(string repo, List<string> files)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder.Append("rm -f --");
            foreach (var file in files)
                builder.Append(' ').Append(file.Quoted());

            Args = builder.ToString();
        }
    }
}
