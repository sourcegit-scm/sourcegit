using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Blame : ObservableObject
    {
        public string FilePath
        {
            get;
        }

        public Models.Commit Revision
        {
            get => _revision;
            private set => SetProperty(ref _revision, value);
        }

        public Models.BlameData Data
        {
            get => _data;
            private set
            {
                if (SetProperty(ref _data, value))
                    OnPropertyChanged(nameof(IsBinary));
            }
        }

        public bool IsBinary
        {
            get => _data?.IsBinary ?? false;
        }

        public bool CanBack
        {
            get => _navigationActiveIndex > 0;
        }

        public bool CanForward
        {
            get => _navigationActiveIndex < _navigationHistory.Count - 1;
        }

        public Blame(string repo, string file, Models.Commit commit)
        {
            var sha = commit.SHA.Substring(0, 10);

            FilePath = file;
            Revision = commit;

            _repo = repo;
            _navigationHistory.Add(sha);
            _commits.Add(sha, commit);
            SetBlameData(sha);
        }

        public string GetCommitMessage(string sha)
        {
            if (_commitMessages.TryGetValue(sha, out var msg))
                return msg;

            msg = new Commands.QueryCommitFullMessage(_repo, sha).GetResult();
            _commitMessages[sha] = msg;
            return msg;
        }

        public void Back()
        {
            if (_navigationActiveIndex <= 0)
                return;

            _navigationActiveIndex--;
            OnPropertyChanged(nameof(CanBack));
            OnPropertyChanged(nameof(CanForward));
            NavigateToCommit(_navigationHistory[_navigationActiveIndex]);
        }

        public void Forward()
        {
            if (_navigationActiveIndex >= _navigationHistory.Count - 1)
                return;

            _navigationActiveIndex++;
            OnPropertyChanged(nameof(CanBack));
            OnPropertyChanged(nameof(CanForward));
            NavigateToCommit(_navigationHistory[_navigationActiveIndex]);
        }

        public void NavigateToCommit(string commitSHA)
        {
            if (!_navigationHistory[_navigationActiveIndex].Equals(commitSHA, StringComparison.Ordinal))
            {
                _navigationHistory.Add(commitSHA);
                _navigationActiveIndex = _navigationHistory.Count - 1;
                OnPropertyChanged(nameof(CanBack));
                OnPropertyChanged(nameof(CanForward));
            }

            if (!Revision.SHA.StartsWith(commitSHA, StringComparison.Ordinal))
                SetBlameData(commitSHA);

            if (App.GetLauncher() is { Pages: { } pages })
            {
                foreach (var page in pages)
                {
                    if (page.Data is Repository repo && repo.FullPath.Equals(_repo))
                    {
                        repo.NavigateToCommit(commitSHA);
                        break;
                    }
                }
            }
        }

        private void SetBlameData(string commitSHA)
        {
            if (_cancellationSource is { IsCancellationRequested: false })
                _cancellationSource.Cancel();

            _cancellationSource = new CancellationTokenSource();
            var token = _cancellationSource.Token;

            if (_commits.TryGetValue(commitSHA, out var c))
            {
                Revision = c;
            }
            else
            {
                Task.Run(async () =>
                {
                    var result = await new Commands.QuerySingleCommit(_repo, commitSHA)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    Dispatcher.UIThread.Post(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            _commits.Add(commitSHA, result);
                            Revision = result ?? new Models.Commit() { SHA = commitSHA };
                        }
                    });
                }, token);
            }

            Task.Run(async () =>
            {
                var result = await new Commands.Blame(_repo, FilePath, commitSHA)
                    .ReadAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    if (!token.IsCancellationRequested)
                        Data = result;
                });
            }, token);
        }

        private string _repo;
        private Models.Commit _revision;
        private CancellationTokenSource _cancellationSource = null;
        private int _navigationActiveIndex = 0;
        private List<string> _navigationHistory = [];
        private Models.BlameData _data = null;
        private Dictionary<string, Models.Commit> _commits = new();
        private Dictionary<string, string> _commitMessages = new();
    }
}
