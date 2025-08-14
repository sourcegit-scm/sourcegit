using System;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Media;
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

        public IBrush DirtyBrush
        {
            get => _dirtyBrush;
            private set => SetProperty(ref _dirtyBrush, value);
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

        public void ClearNotifications()
        {
            Notifications.Clear();
        }

        public async Task CopyPathAsync()
        {
            if (_node.IsRepository)
                await App.CopyTextAsync(_node.Id);
        }

        public void ChangeDirtyState(Models.DirtyState flag, bool remove)
        {
            if (remove)
            {
                if (_dirtyState.HasFlag(flag))
                    _dirtyState -= flag;
            }
            else
            {
                _dirtyState |= flag;
            }

            if (_dirtyState.HasFlag(Models.DirtyState.HasLocalChanges))
                DirtyBrush = Brushes.Gray;
            else if (_dirtyState.HasFlag(Models.DirtyState.HasPendingPullOrPush))
                DirtyBrush = Brushes.RoyalBlue;
            else
                DirtyBrush = null;
        }

        public bool CanCreatePopup()
        {
            return _popup is not { InProgress: true };
        }

        public async Task ProcessPopupAsync()
        {
            if (_popup is { InProgress: false } dump)
            {
                if (!dump.Check())
                    return;

                dump.InProgress = true;

                try
                {
                    var finished = await dump.Sure();
                    if (finished)
                    {
                        dump.Cleanup();
                        Popup = null;
                    }
                }
                catch (Exception e)
                {
                    App.LogException(e);
                }

                dump.InProgress = false;
            }
        }

        public void CancelPopup()
        {
            if (_popup == null || _popup.InProgress)
                return;

            _popup?.Cleanup();
            Popup = null;
        }

        private RepositoryNode _node = null;
        private object _data = null;
        private IBrush _dirtyBrush = null;
        private Models.DirtyState _dirtyState = Models.DirtyState.None;
        private Popup _popup = null;
    }
}
