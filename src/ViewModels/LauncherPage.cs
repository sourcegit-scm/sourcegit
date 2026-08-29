using System;
using System.IO;
using System.Threading.Tasks;
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

        public Models.DirtyState DirtyState
        {
            get => _dirtyState;
            private set => SetProperty(ref _dirtyState, value);
        }

        public string BaseBranch
        {
            get => _baseBranch;
            private set
            {
                if (SetProperty(ref _baseBranch, value))
                {
                    OnPropertyChanged(nameof(HasBaseBranch));
                    OnPropertyChanged(nameof(BaseBranchBadgeColor));
                }
            }
        }

        public bool HasBaseBranch => !string.IsNullOrEmpty(_baseBranch);

        public string BaseBranchBadgeColor => Models.WorktreeBaseBranch.GetBadgeColor(Models.WorktreeBaseBranch.GetKind(_baseBranch));

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
            RefreshBaseBranch(repo);
        }

        public void RefreshBaseBranch(Repository repo)
        {
            if (repo == null || !File.Exists(Path.Combine(repo.FullPath, ".git")))
            {
                BaseBranch = string.Empty;
                return;
            }

            var branch = Models.WorktreeBaseBranch.ReadPersisted(repo.GitDir);
            if (string.IsNullOrEmpty(branch))
                branch = new Commands.QueryWorktreeBaseBranch(repo.FullPath).GetResult();

            BaseBranch = Models.WorktreeBaseBranch.Normalize(branch);
        }

        public void ClearNotifications()
        {
            Notifications.Clear();
        }

        public void ChangeDirtyState(Models.DirtyState flag, bool remove)
        {
            var state = _dirtyState;
            if (remove)
            {
                if (state.HasFlag(flag))
                    state -= flag;
            }
            else
            {
                state |= flag;
            }

            DirtyState = state;
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
                    Native.OS.LogException(e);
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
        private Models.DirtyState _dirtyState = Models.DirtyState.None;
        private string _baseBranch = string.Empty;
        private Popup _popup = null;
    }
}
