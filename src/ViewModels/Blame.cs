using System;
using System.Collections.Generic;
using System.Text;
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

        public Models.Commit PrevRevision
        {
            get => _prevRevision;
            private set => SetProperty(ref _prevRevision, value);
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
            PrevRevision = null;

            _repo = repo;
            _navigationHistory.Add(sha);
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
            NavigateToCommit(_navigationHistory[_navigationActiveIndex], true);
        }

        public void Forward()
        {
            if (_navigationActiveIndex >= _navigationHistory.Count - 1)
                return;

            _navigationActiveIndex++;
            OnPropertyChanged(nameof(CanBack));
            OnPropertyChanged(nameof(CanForward));
            NavigateToCommit(_navigationHistory[_navigationActiveIndex], true);
        }

        public void GotoPrevRevision()
        {
            if (_prevRevision == null)
                return;

            NavigateToCommit(_prevRevision.SHA, false);
        }

        public void NavigateToCommit(string commitSHA, bool isBackOrForward)
        {
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

            if (Revision.SHA.StartsWith(commitSHA, StringComparison.Ordinal))
                return;

            if (!isBackOrForward)
            {
                var count = _navigationHistory.Count;
                if (_navigationActiveIndex < count - 1)
                    _navigationHistory.RemoveRange(_navigationActiveIndex + 1, count - _navigationActiveIndex - 1);

                _navigationHistory.Add(commitSHA);
                _navigationActiveIndex++;
                OnPropertyChanged(nameof(CanBack));
                OnPropertyChanged(nameof(CanForward));
            }

            SetBlameData(commitSHA);
        }

        private void SetBlameData(string commitSHA)
        {
            if (_cancellationSource is { IsCancellationRequested: false })
                _cancellationSource.Cancel();

            _cancellationSource = new CancellationTokenSource();
            var token = _cancellationSource.Token;

            Task.Run(async () =>
            {
                var argsBuilder = new StringBuilder();
                argsBuilder
                    .Append("--date-order -n 2 ")
                    .Append(commitSHA ?? string.Empty)
                    .Append(" -- ")
                    .Append(FilePath.Quoted());

                var commits = await new Commands.QueryCommits(_repo, argsBuilder.ToString(), false)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        Revision = commits.Count > 0 ? commits[0] : null;
                        PrevRevision = commits.Count == 2 ? commits[1] : null;
                    }
                });
            });

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
        private Models.Commit _prevRevision;
        private CancellationTokenSource _cancellationSource = null;
        private int _navigationActiveIndex = 0;
        private List<string> _navigationHistory = [];
        private Models.BlameData _data = null;
        private Dictionary<string, string> _commitMessages = new();
    }
}
