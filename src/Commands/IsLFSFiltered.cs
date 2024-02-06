namespace SourceGit.Commands {
    public class IsLFSFiltered : Command {
        public IsLFSFiltered(string repo, string path) {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"check-attr -a -z \"{path}\"";
            RaiseError = false;
        }

        public bool Result() {
            var rs = ReadToEnd();
            return rs.IsSuccess && rs.StdOut.Contains("filter\0lfs");
        }
    }
}
