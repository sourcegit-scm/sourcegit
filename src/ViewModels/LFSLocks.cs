﻿using System;
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

            var succ = await _repo.UnlockLFSFileAsync(_remote, lfsLock.File, force, false);
            if (succ)
            {
                _cachedLocks.Remove(lfsLock);
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
                    if (lfsLock.User.Equals(_userName, StringComparison.Ordinal))
                        visible.Add(lfsLock);
                }
            }

            VisibleLocks = visible;
        }

        private readonly Repository _repo;
        private readonly string _remote;
        private bool _isLoading = true;
        private List<Models.LFSLock> _cachedLocks = [];
        private List<Models.LFSLock> _visibleLocks = [];
        private bool _showOnlyMyLocks;
        private string _userName;
    }
}
