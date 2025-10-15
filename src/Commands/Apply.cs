namespace SourceGit.Commands
{
    public class Apply : Command
    {
        public Apply(string repo, string file, bool ignoreWhitespace, string whitespaceMode, string extra)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "apply ";
            if (ignoreWhitespace)
                Args += "--ignore-whitespace ";
            else
                Args += $"--whitespace={whitespaceMode} ";
            if (!string.IsNullOrEmpty(extra))
                Args += $"{extra} ";
            Args += $"{file.Quoted()}";
        }
    }
}
