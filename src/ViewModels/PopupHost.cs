using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class PopupHost : ObservableObject
    {
        public static PopupHost Active
        {
            get;
            set;
        } = null;

        public Popup Popup
        {
            get => _popup;
            set => SetProperty(ref _popup, value);
        }

        public static bool CanCreatePopup()
        {
            return Active?.IsInProgress() != true;
        }

        public static void ShowPopup(Popup popup)
        {
            popup.HostPageId = Active.GetId();
            Active.Popup = popup;
        }

        public static void ShowAndStartPopup(Popup popup)
        {
            var dumpPage = Active;
            popup.HostPageId = dumpPage.GetId();
            dumpPage.Popup = popup;
            dumpPage.ProcessPopup();
        }

        public virtual string GetId()
        {
            return string.Empty;
        }

        public virtual bool IsInProgress()
        {
            return _popup is { InProgress: true };
        }

        public async void ProcessPopup()
        {
            if (_popup != null)
            {
                if (!_popup.Check())
                    return;

                _popup.InProgress = true;
                var task = _popup.Sure();
                if (task != null)
                {
                    var finished = await task;
                    _popup.InProgress = false;
                    if (finished)
                        Popup = null;
                }
                else
                {
                    _popup.InProgress = false;
                    Popup = null;
                }
            }
        }

        public void CancelPopup()
        {
            if (_popup == null)
                return;
            if (_popup.InProgress)
                return;
            Popup = null;
        }

        private Popup _popup = null;
    }
}
