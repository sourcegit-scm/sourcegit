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

        public bool IsMergeCommit
        {
            get;
            private set;
        }

        public List<Models.Commit> ParentsForMergeCommit
        {
            get;
            private set;
        }

        public int MainlineForMergeCommit
        {
            get;
            set;
        }

        public bool AppendSourceToMessage
        {
            get;
            set;
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
            IsMergeCommit = false;
            ParentsForMergeCommit = [];
            MainlineForMergeCommit = 0;
            AppendSourceToMessage = true;
            AutoCommit = true;
            View = new Views.CherryPick() { DataContext = this };
        }

        public CherryPick(Repository repo, Models.Commit merge, List<Models.Commit> parents)
        {
            _repo = repo;
            Targets = [merge];
            IsMergeCommit = true;
            ParentsForMergeCommit = parents;
            MainlineForMergeCommit = 0;
            AppendSourceToMessage = true;
            AutoCommit = true;
            View = new Views.CherryPick() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Cherry-Pick commit(s) ...";

            return Task.Run(() =>
            {
                var succ = false;
                if (IsMergeCommit)
                {
                    succ = new Commands.CherryPick(
                        _repo.FullPath,
                        Targets[0].SHA,
                        !AutoCommit,
                        AppendSourceToMessage,
                        $"-m {MainlineForMergeCommit+1}").Exec();
                }
                else
                {
                    var builder = new StringBuilder();
                    for (int i = Targets.Count - 1; i >= 0; i--)
                        builder.Append($"{Targets[i].SHA} ");

                    succ = new Commands.CherryPick(
                        _repo.FullPath,
                        builder.ToString(),
                        !AutoCommit,
                        AppendSourceToMessage,
                        string.Empty).Exec();
                }
                
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
    }
}
