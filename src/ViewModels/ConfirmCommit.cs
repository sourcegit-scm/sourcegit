using System;

namespace SourceGit.ViewModels
{
    public class ConfirmCommit
    {
        public string Message
        {
            get;
            private set;
        }

        public ConfirmCommit(string message, Action onSure)
        {
            Message = message;
            _onSure = onSure;
        }

        public void Continue()
        {
            _onSure?.Invoke();
        }

        private Action _onSure;
    }
}
