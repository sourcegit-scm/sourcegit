using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SourceGit.Models;

namespace SourceGit.ViewModels
{
    public class LFSLocks : ObservableObject
    {
        public bool HasValidUserName
        {
            get => _hasValidUsername;
            private set => SetProperty(ref _hasValidUsername, value);
        }

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

            Task.Run(async () =>
            {
                _userName = await new Commands.Config(repo.FullPath).GetAsync("user.name").ConfigureAwait(false);
                _cachedLocks = await new Commands.LFS(_repo.FullPath).GetLocksAsync(_remote).ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    UpdateVisibleLocks();
                    IsLoading = false;
                    HasValidUserName = !string.IsNullOrEmpty(_userName);
                });
            });
        }

        public async Task UnlockAsync(Models.LFSLock lfsLock, bool force)
        {
            if (_isLoading)
                return;

            IsLoading = true;

            var succ = await _repo.UnlockLFSFileAsync(_remote, lfsLock.Path, force, false);
            if (succ)
            {
                _cachedLocks.Remove(lfsLock);
                UpdateVisibleLocks();
            }

            IsLoading = false;
        }

        public async Task UnlockAllMyLocksAsync(bool force = false)
        {
            if (_isLoading)
                return;
            
            IsLoading = true;

            List<string> myLocks = [];
            foreach (LFSLock lfsLock in _cachedLocks)
            {
                if (lfsLock.Owner.Name.Equals(_userName, StringComparison.Ordinal))
                {
                    myLocks.Add(lfsLock.Path);
                }
            }

            bool succ = await _repo.UnlockLFSFilesAsync(_remote, myLocks, force, false);
            if (succ)
            {
                _cachedLocks.RemoveAll(lfsLock => lfsLock.Owner.Name.Equals(_userName, StringComparison.Ordinal));
                UpdateVisibleLocks();
            }

            IsLoading = false;
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
                    if (lfsLock.Owner.Name.Equals(_userName, StringComparison.Ordinal))
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
        private bool _hasValidUsername;
    }
}
