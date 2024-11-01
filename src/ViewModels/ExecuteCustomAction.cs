using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ExecuteCustomAction : Popup
    {
        public Models.CustomAction CustomAction
        {
            get;
            private set;
        }

        public ExecuteCustomAction(Repository repo, Models.CustomAction action, string sha)
        {
            _repo = repo;
            _args = action.Arguments.Replace("${REPO}", _repo.FullPath);
            if (!string.IsNullOrEmpty(sha))
                _args = _args.Replace("${SHA}", sha);

            CustomAction = action;
            View = new Views.ExecuteCustomAction() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Run custom action ...";

            return Task.Run(() =>
            {
                Commands.ExecuteCustomAction.Run(_repo.FullPath, CustomAction.Executable, _args, SetProgressDescription);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
        private string _args = string.Empty;
    }
}
