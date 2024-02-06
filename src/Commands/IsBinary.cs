using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    public class IsBinary : Command {
        private static readonly Regex REG_TEST = new Regex(@"^\-\s+\-\s+.*$");

        public IsBinary(string repo, string commit, string path) {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"diff 4b825dc642cb6eb9a060e54bf8d69288fbee4904 {commit} --numstat -- \"{path}\"";
            RaiseError = false;
        }

        public bool Result() {
            return REG_TEST.IsMatch(ReadToEnd().StdOut);
        }
    }
}
