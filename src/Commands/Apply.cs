namespace SourceGit.Commands {
    /// <summary>
    ///     应用Patch
    /// </summary>
    public class Apply : Command {

        public Apply(string repo, string file, bool ignoreWhitespace, string whitespaceMode) {
            Cwd = repo;
            Args = "apply ";
            if (ignoreWhitespace) Args += "--ignore-whitespace ";
            else Args += $"--whitespace={whitespaceMode} ";
            Args += $"\"{file}\"";
        }
    }
}
