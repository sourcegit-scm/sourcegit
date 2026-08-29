using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Threading;
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
            set
            {
                if (ReferenceEquals(_data, value))
                    return;

                if (_data is Repository oldRepo)
                    oldRepo.PropertyChanged -= OnRepositoryPropertyChanged;

                if (SetProperty(ref _data, value) && value is Repository repo)
                {
                    repo.PropertyChanged += OnRepositoryPropertyChanged;
                    RefreshBaseBranch(repo);
                }
                else if (value is not Repository)
                {
                    Interlocked.Increment(ref _baseBranchRefreshVersion);
                    BaseBranch = string.Empty;
                }
            }
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
                    OnPropertyChanged(nameof(BaseBranchKind));
                }
            }
        }

        public bool HasBaseBranch => !string.IsNullOrEmpty(_baseBranch);

        public Models.WorktreeBaseBranchKind BaseBranchKind => Models.WorktreeBaseBranch.GetKind(_baseBranch);

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
            Data = repo;
        }

        public void RefreshBaseBranch(Repository repo)
        {
            var version = Interlocked.Increment(ref _baseBranchRefreshVersion);
            _ = RefreshBaseBranchAsync(repo, version);
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

        private async Task RefreshBaseBranchAsync(Repository repo, int version)
        {
            var query = new Commands.QueryWorktreeBaseBranch(repo.FullPath);
            var gitDir = await query.GetGitDirAsync().ConfigureAwait(false);
            var currentBranch = repo.CurrentBranch?.Name ?? string.Empty;
            var branch = Models.WorktreeBaseBranch.ReadPersisted(gitDir, currentBranch);
            if (string.IsNullOrEmpty(branch))
                branch = await query.GetResultAsync().ConfigureAwait(false);

            branch = Models.WorktreeBaseBranch.Normalize(branch);
            Dispatcher.UIThread.Post(() =>
            {
                if (version == _baseBranchRefreshVersion && ReferenceEquals(_data, repo))
                    BaseBranch = branch;
            });
        }

        private void OnRepositoryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Repository.CurrentBranch) && sender is Repository repo)
                RefreshBaseBranch(repo);
        }

        private RepositoryNode _node = null;
        private object _data = null;
        private Models.DirtyState _dirtyState = Models.DirtyState.None;
        private string _baseBranch = string.Empty;
        private int _baseBranchRefreshVersion = 0;
        private Popup _popup = null;
    }
}
