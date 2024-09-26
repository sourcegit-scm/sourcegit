﻿using System.Linq;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class LFSLocks : ObservableObject
    {
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public bool IsEmpty
        {
            get => _isEmpty;
            private set => SetProperty(ref _isEmpty, value);
        }
        
        public bool ShowOnlyMyLocks
        {
            get => _showOnlyMyLocks;
            set
            {
                if (!SetProperty(ref _showOnlyMyLocks, value))
                    return;

                OnPropertyChanged(nameof(FilteredLocks));
                IsEmpty = !FilteredLocks.Any();
            }
        }

        private AvaloniaList<Models.LFSLock> Locks
        {
            get;
        }

        public AvaloniaList<Models.LFSLock> FilteredLocks
        {
            get
            {
                if (string.IsNullOrEmpty(_userName))
                {
                    App.RaiseException(_repo, "Username is empty");
                    return Locks;
                }

                return _showOnlyMyLocks ?
                    new AvaloniaList<Models.LFSLock>(Locks.Where(@lock => @lock.User == _userName)) :
                    Locks;
            }
        }

        public LFSLocks(string repo, string remote)
        {
            _repo = repo;
            _remote = remote;
            Locks = new AvaloniaList<Models.LFSLock>();
            new Commands.Config(repo).ListAll().TryGetValue("user.name", out _userName);

            Task.Run(() =>
            {
                var collect = new Commands.LFS(_repo).Locks(_remote);
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (collect.Count > 0)
                        Locks.AddRange(collect);

                    IsLoading = false;
                    IsEmpty = collect.Count == 0;
                });
            });
        }

        public void Unlock(Models.LFSLock lfsLock, bool force)
        {
            if (_isLoading)
                return;

            IsLoading = true;
            Task.Run(() =>
            {
                var succ = new Commands.LFS(_repo).Unlock(_remote, lfsLock.ID, force);
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (succ)
                        Locks.Remove(lfsLock);

                    IsLoading = false;
                    IsEmpty = Locks.Count == 0;
                });
            });
        }

        private string _repo;
        private string _remote;
        private bool _isLoading = true;
        private bool _isEmpty = false;
        private bool _showOnlyMyLocks = false;
        private string _userName;
    }
}
