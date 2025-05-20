using System;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ExecuteCustomAction : Popup
    {
        public Models.CustomAction CustomAction
        {
            get;
        }

        public ExecuteCustomAction(Repository repo, Models.CustomAction action)
        {
            _repo = repo;
            _args = action.Arguments.Replace("${REPO}", GetWorkdir());
            CustomAction = action;
        }

        public ExecuteCustomAction(Repository repo, Models.CustomAction action, Models.Branch branch)
        {
            _repo = repo;
            _args = action.Arguments.Replace("${REPO}", GetWorkdir()).Replace("${BRANCH}", branch.FriendlyName);
            CustomAction = action;
        }

        public ExecuteCustomAction(Repository repo, Models.CustomAction action, Models.Commit commit)
        {
            _repo = repo;
            _args = action.Arguments.Replace("${REPO}", GetWorkdir()).Replace("${SHA}", commit.SHA);
            CustomAction = action;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Run custom action ...";

            var log = _repo.CreateLog(CustomAction.Name);
            Use(log);

            return Task.Run(() =>
            {
                if (CustomAction.WaitForExit)
                    Commands.ExecuteCustomAction.RunAndWait(_repo.FullPath, CustomAction.Executable, _args, log);
                else
                    Commands.ExecuteCustomAction.Run(_repo.FullPath, CustomAction.Executable, _args);

                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private string GetWorkdir()
        {
            return OperatingSystem.IsWindows() ? _repo.FullPath.Replace("/", "\\") : _repo.FullPath;
        }

        private readonly Repository _repo = null;
        private readonly string _args;
    }
}
