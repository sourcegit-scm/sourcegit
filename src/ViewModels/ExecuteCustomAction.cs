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

        public ExecuteCustomAction(Repository repo, Models.CustomAction action)
        {
            _repo = repo;
            _args = action.Arguments.Replace("${REPO}", _repo.FullPath);
            CustomAction = action;
            View = new Views.ExecuteCustomAction() { DataContext = this };
        }

        public ExecuteCustomAction(Repository repo, Models.CustomAction action, Models.Branch branch)
        {
            _repo = repo;
            _args = action.Arguments.Replace("${REPO}", _repo.FullPath).Replace("${BRANCH}", branch.FriendlyName);
            CustomAction = action;
            View = new Views.ExecuteCustomAction() { DataContext = this };
        }

        public ExecuteCustomAction(Repository repo, Models.CustomAction action, Models.Commit commit)
        {
            _repo = repo;
            _args = action.Arguments.Replace("${REPO}", _repo.FullPath).Replace("${SHA}", commit.SHA);
            CustomAction = action;
            View = new Views.ExecuteCustomAction() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Run custom action ...";

            return Task.Run(() =>
            {
                if (CustomAction.WaitForExit)
                    Commands.ExecuteCustomAction.RunAndWait(_repo.FullPath, CustomAction.Executable, _args, SetProgressDescription);
                else
                    Commands.ExecuteCustomAction.Run(_repo.FullPath, CustomAction.Executable, _args);

                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
        private string _args = string.Empty;
    }
}
