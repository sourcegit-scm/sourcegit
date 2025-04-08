using System;

namespace SourceGit.ViewModels
{
    public class ConfirmEmptyCommit
    {
        public bool HasLocalChanges
        {
            get;
            private set;
        }

        public ConfirmEmptyCommit(bool hasLocalChanges, Action<bool> onSure)
        {
            HasLocalChanges = hasLocalChanges;
            _onSure = onSure;
        }

        public void StageAllThenCommit()
        {
            _onSure?.Invoke(true);
        }

        public void Continue()
        {
            _onSure?.Invoke(false);
        }

        private Action<bool> _onSure;
    }
}
