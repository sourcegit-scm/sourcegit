using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CherryPick : Popup
    {
        public List<Models.Commit> Targets
        {
            get;
            private set;
        }

        public bool AutoCommit
        {
            get;
            set;
        }

        public CherryPick(Repository repo, List<Models.Commit> targets)
        {
            _repo = repo;
            Targets = targets;
            AutoCommit = true;
            View = new Views.CherryPick() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Cherry-Pick commit(s) ...";

            return Task.Run(() =>
            {
                // Get commit SHAs reverted
                var builder = new StringBuilder();
                for (int i = Targets.Count - 1; i >= 0; i--)
                    builder.Append($"{Targets[i].SHA} ");

                var succ = new Commands.CherryPick(_repo.FullPath, builder.ToString(), !AutoCommit).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
    }
}
