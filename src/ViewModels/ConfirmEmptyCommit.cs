using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ConfirmEmptyCommit
    {
        public bool HasLocalChanges
        {
            get;
            private set;
        }

        public string Message
        {
            get;
            private set;
        }

        public ConfirmEmptyCommit(WorkingCopy wc, bool autoPush, int unstagedCount)
        {
            _wc = wc;
            _autoPush = autoPush;
            HasLocalChanges = unstagedCount > 0;
            Message = App.Text(HasLocalChanges ? "ConfirmEmptyCommit.WithLocalChanges" : "ConfirmEmptyCommit.NoLocalChanges");
        }

        public async Task StageAllThenCommitAsync()
        {
            await _wc.CommitAsync(true, _autoPush, Models.CommitCheckPassed.FileCount);
        }

        public async Task ContinueAsync()
        {
            await _wc.CommitAsync(false, _autoPush, Models.CommitCheckPassed.FileCount);
        }

        private readonly WorkingCopy _wc;
        private readonly bool _autoPush;
    }
}
