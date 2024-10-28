namespace SourceGit.ViewModels
{
    public class ConfirmCommitWithoutFiles
    {
        public ConfirmCommitWithoutFiles(WorkingCopy wc, bool autoPush)
        {
            _wc = wc;
            _autoPush = autoPush;
        }

        public void Continue()
        {
            _wc.CommitWithoutFiles(_autoPush);
        }

        private readonly WorkingCopy _wc;
        private bool _autoPush;
    }
}
