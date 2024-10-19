namespace SourceGit.Commands
{
    public partial class QueryHasUntrackedFiles : Command
    {
        private bool _hasUntracked = false;

        public QueryHasUntrackedFiles(string repo) {
            WorkingDirectory = repo;
            Context = repo;
            Args = "ls-files --others --exclude-standard";
        }

        public bool Result()
        {
            Exec();
            return _hasUntracked;
        }

        protected override void OnReadline(string line)
        {
            _hasUntracked = true;
        }
    }
}
