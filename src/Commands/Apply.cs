namespace SourceGit.Commands {
    public class Apply : Command {
        public Apply(string repo, string file, bool ignoreWhitespace, string whitespaceMode) {
            WorkingDirectory = repo;
            Context = repo;
            Args = "apply ";
            if (ignoreWhitespace) Args += "--ignore-whitespace ";
            else Args += $"--whitespace={whitespaceMode} ";
            Args += $"\"{file}\"";
        }
    }
}
