namespace SourceGit.Commands {
    /// <summary>
    ///     分支相关操作
    /// </summary>
    class Branch : Command {
        private string target = null;

        public Branch(string repo, string branch) {
            Cwd = repo;
            target = branch;
        }

        public void Create(string basedOn) {
            Args = $"branch {target} {basedOn}";
            Exec();
        }

        public void Rename(string to) {
            Args = $"branch -M {target} {to}";
            Exec();
        }

        public void SetUpstream(string upstream) {
            Args = $"branch {target} -u {upstream}";
            Exec();
        }

        public void Delete() {
            Args = $"branch -D {target}";
            Exec();
        }
    }
}
