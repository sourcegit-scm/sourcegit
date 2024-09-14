using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class Apply : Command
    {
        public Apply(string repo, string file, bool ignoreWhitespace, string whitespaceMode, IEnumerable<string> extra)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = ["apply"];
            if (ignoreWhitespace)
                Args.Add("--ignore-whitespace");
            else
                Args.Add($"--whitespace={whitespaceMode}");
            Args.AddRange(extra);
            Args.Add(file);
        }
    }
}
