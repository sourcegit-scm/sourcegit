using System;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class LauncherPage : ObservableObject
    {
        public RepositoryNode Node
        {
            get => _node;
            set => SetProperty(ref _node, value);
        }

        public object Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        public Popup Popup
        {
            get => _popup;
            set => SetProperty(ref _popup, value);
        }

        public AvaloniaList<Models.Notification> Notifications
        {
            get;
            set;
        } = new AvaloniaList<Models.Notification>();

        public LauncherPage()
        {
            _node = new RepositoryNode() { Id = Guid.NewGuid().ToString() };
            _data = Welcome.Instance;

            // New welcome page will clear the search filter before.
            Welcome.Instance.ClearSearchFilter();
        }

        public LauncherPage(RepositoryNode node, Repository repo)
        {
            _node = node;
            _data = repo;
        }

        public void CopyPath()
        {
            if (_node.IsRepository)
                App.CopyText(_node.Id);
        }

        public bool CanCreatePopup()
        {
            return _popup == null || !_popup.InProgress;
        }

        public void StartPopup(Popup popup)
        {
            Popup = popup;
            ProcessPopup();
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

        private RepositoryNode _node = null;
        private object _data = null;
        private Popup _popup = null;
    }
}
