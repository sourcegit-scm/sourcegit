using System.Collections.Generic;
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
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            _repo.ClearCommitMessage();
            ProgressDescription = "Cherry-Pick commit(s) ...";

            var log = _repo.CreateLog("Cherry-Pick");
            Use(log);

            if (IsMergeCommit)
            {
                await new Commands.CherryPick(
                    _repo.FullPath,
                    Targets[0].SHA,
                    !AutoCommit,
                    AppendSourceToMessage,
                    $"-m {MainlineForMergeCommit + 1}")
                    .Use(log)
                    .ExecAsync();
            }
            else
            {
                await new Commands.CherryPick(
                    _repo.FullPath,
                    string.Join(' ', Targets.ConvertAll(c => c.SHA)),
                    !AutoCommit,
                    AppendSourceToMessage,
                    string.Empty)
                    .Use(log)
                    .ExecAsync();
            }

            log.Complete();
            return true;
        }

        private readonly Repository _repo = null;
    }
}
