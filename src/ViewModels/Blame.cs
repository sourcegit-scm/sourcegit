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
        public string File
        {
            get => _file;
            private set => SetProperty(ref _file, value);
        }

        public bool IgnoreWhitespace
        {
            get => _ignoreWhitespace;
            set
            {
                if (SetProperty(ref _ignoreWhitespace, value))
                    SetBlameData(_navigationHistory[0]);
            }
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
            _repo = repo;
            _navigationHistory.Add(new RevisionInfo(file, sha));
            SetBlameData(_navigationHistory[0]);
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

        public void GotoPrevRevision()
        {
            if (_prevRevision != null)
                NavigateToCommit(_file, _prevRevision.SHA.Substring(0, 10));
        }

        public void NavigateToCommit(string file, string sha)
        {
            if (App.GetLauncher() is { Pages: { } pages })
            {
                foreach (var page in pages)
                {
                    if (page.Data is Repository repo && repo.FullPath.Equals(_repo))
                    {
                        repo.NavigateToCommit(sha);
                        break;
                    }
                }
            }

            if (Revision.SHA.StartsWith(sha, StringComparison.Ordinal))
                return;

            var count = _navigationHistory.Count;
            if (_navigationActiveIndex < count - 1)
                _navigationHistory.RemoveRange(_navigationActiveIndex + 1, count - _navigationActiveIndex - 1);

            var rev = new RevisionInfo(file, sha);
            _navigationHistory.Add(rev);
            _navigationActiveIndex++;
            OnPropertyChanged(nameof(CanBack));
            OnPropertyChanged(nameof(CanForward));
            SetBlameData(rev);
        }

        private void NavigateToCommit(RevisionInfo rev)
        {
            if (App.GetLauncher() is { Pages: { } pages })
            {
                foreach (var page in pages)
                {
                    if (page.Data is Repository repo && repo.FullPath.Equals(_repo))
                    {
                        repo.NavigateToCommit(rev.SHA);
                        break;
                    }
                }
            }

            if (!Revision.SHA.StartsWith(rev.SHA, StringComparison.Ordinal))
                SetBlameData(rev);
        }

        private void SetBlameData(RevisionInfo rev)
        {
            if (_cancellationSource is { IsCancellationRequested: false })
                _cancellationSource.Cancel();

            _cancellationSource = new CancellationTokenSource();
            var token = _cancellationSource.Token;

            File = rev.File;

            Task.Run(async () =>
            {
                var argsBuilder = new StringBuilder();
                argsBuilder
                    .Append("--date-order -n 2 ")
                    .Append(rev.SHA)
                    .Append(" -- ")
                    .Append(rev.File.Quoted());

                var commits = await new Commands.QueryCommits(_repo, argsBuilder.ToString(), false)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        Revision = commits.Count > 0 ? commits[0] : null;
                        PrevRevision = commits.Count > 1 ? commits[1] : null;
                    }
                });
            });

            Task.Run(async () =>
            {
                var result = await new Commands.Blame(_repo, rev.File, rev.SHA, _ignoreWhitespace)
                    .ReadAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    if (!token.IsCancellationRequested)
                        Data = result;
                });
            }, token);
        }

        private class RevisionInfo
        {
            public string File { get; set; } = string.Empty;
            public string SHA { get; set; } = string.Empty;

            public RevisionInfo(string file, string sha)
            {
                File = file;
                SHA = sha;
            }
        }

        private string _repo;
        private string _file;
        private bool _ignoreWhitespace = false;
        private Models.Commit _revision;
        private Models.Commit _prevRevision;
        private CancellationTokenSource _cancellationSource = null;
        private int _navigationActiveIndex = 0;
        private List<RevisionInfo> _navigationHistory = [];
        private Models.BlameData _data = null;
        private Dictionary<string, string> _commitMessages = new();
    }
}
