using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class LFSLocks : ObservableObject
    {
        public bool HasValidUserName
        {
            get;
            private set;
        } = false;

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public bool ShowOnlyMyLocks
        {
            get => _showOnlyMyLocks;
            set
            {
                if (SetProperty(ref _showOnlyMyLocks, value))
                    UpdateVisibleLocks();
            }
        }

        public List<Models.LFSLock> VisibleLocks
        {
            get => _visibleLocks;
            private set => SetProperty(ref _visibleLocks, value);
        }

        public LFSLocks(Repository repo, string remote)
        {
            _repo = repo;
            _remote = remote;
            _userName = new Commands.Config(repo.FullPath).Get("user.name");

            HasValidUserName = !string.IsNullOrEmpty(_userName);

            Task.Run(async () =>
            {
                _cachedLocks = await new Commands.LFS(_repo.FullPath).LocksAsync(_remote);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateVisibleLocks();
                    IsLoading = false;
                });
            });
        }

        public void Unlock(Models.LFSLock lfsLock, bool force)
        {
            if (_isLoading)
                return;

            IsLoading = true;

            var log = _repo.CreateLog("Unlock LFS File");
            Task.Run(async () =>
            {
                var succ = await new Commands.LFS(_repo.FullPath).UnlockAsync(_remote, lfsLock.ID, force, log);
                log.Complete();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (succ)
                    {
                        _cachedLocks.Remove(lfsLock);
                        UpdateVisibleLocks();
                    }

                    IsLoading = false;
                });
            });
        }

        private void UpdateVisibleLocks()
        {
            var visible = new List<Models.LFSLock>();

            if (!_showOnlyMyLocks)
            {
                visible.AddRange(_cachedLocks);
            }
            else
            {
                foreach (var lfsLock in _cachedLocks)
                {
                    if (lfsLock.User.Equals(_userName, StringComparison.Ordinal))
                        visible.Add(lfsLock);
                }
            }

            VisibleLocks = visible;
        }

        private Repository _repo;
        private string _remote;
        private bool _isLoading = true;
        private List<Models.LFSLock> _cachedLocks = [];
        private List<Models.LFSLock> _visibleLocks = [];
        private bool _showOnlyMyLocks = false;
        private string _userName;
    }
}
