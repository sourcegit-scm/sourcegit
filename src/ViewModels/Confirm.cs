using System;

namespace SourceGit.ViewModels
{
    public class Confirm
    {
        public string Message
        {
            get;
            private set;
        }

        public Confirm(string message, Action onSure, Action onCancel = null)
        {
            Message = message;
            _onSure = onSure;
            _onCancel = onCancel;
        }

        public void Done(bool isSure)
        {
            if (isSure)
                _onSure?.Invoke();
            else
                _onCancel?.Invoke();
        }

        private Action _onSure;
        private Action _onCancel;
    }
}
