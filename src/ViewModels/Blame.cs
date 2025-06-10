using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Blame : ObservableObject
    {
        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        public bool IsBinary
        {
            get => _data != null && _data.IsBinary;
        }

        public bool CanMoveBack
        {
            get => _shaHistoryIndex > 0 && _shaHistory.Count > 1;
        }
        public bool CanMoveForward
        {
            get => _shaHistoryIndex < _shaHistory.Count - 1;
        }

        public Models.BlameData Data
        {
            get => _data;
            private set => SetProperty(ref _data, value);
        }

        public Blame(string repo, string file, string revision)
        {
            _repo = repo;
            _file = file;

            SetBlameData($"{revision.AsSpan(0, 10)}", true);
        }

        private void SetBlameData(string commitSHA, bool resetHistoryForward)
        {
            Title = $"{_file} @ {commitSHA}";

            Task.Run(() =>
            {
                var result = new Commands.Blame(_repo, _file, commitSHA).Result();
                Dispatcher.UIThread.Invoke(() =>
                {
                    Data = result;
                    OnPropertyChanged(nameof(IsBinary));
                });
            });

            if (resetHistoryForward)
            {
                if (_shaHistoryIndex < _shaHistory.Count - 1)
                    _shaHistory.RemoveRange(_shaHistoryIndex + 1, _shaHistory.Count - _shaHistoryIndex - 1);

                if (_shaHistory.Count == 0 || _shaHistory[_shaHistoryIndex] != commitSHA)
                {
                    _shaHistory.Add(commitSHA);
                    _shaHistoryIndex = _shaHistory.Count - 1;
                }
            }

            OnPropertyChanged(nameof(CanMoveBack));
            OnPropertyChanged(nameof(CanMoveForward));
        }

        public void Back()
        {
            --_shaHistoryIndex;
            if (_shaHistoryIndex < 0)
                _shaHistoryIndex = 0;

            NavigateToCommit(_shaHistory[_shaHistoryIndex], false);
        }

        public void Forward()
        {
            ++_shaHistoryIndex;
            if (_shaHistoryIndex >= _shaHistory.Count)
                _shaHistoryIndex = _shaHistory.Count - 1;

            NavigateToCommit(_shaHistory[_shaHistoryIndex], false);
        }

        public void NavigateToCommit(string commitSHA, bool resetHistoryForward)
        {
            var launcher = App.GetLauncher();
            if (launcher == null)
                return;

            foreach (var page in launcher.Pages)
            {
                if (page.Data is Repository repo && repo.FullPath.Equals(_repo))
                {
                    repo.NavigateToCommit(commitSHA);
                    SetBlameData(commitSHA, resetHistoryForward);
                    break;
                }
            }
        }

        public string GetCommitMessage(string sha)
        {
            if (_commitMessages.TryGetValue(sha, out var msg))
                return msg;

            msg = new Commands.QueryCommitFullMessage(_repo, sha).Result();
            _commitMessages[sha] = msg;
            return msg;
        }

        private string _repo;
        private string _file;
        private string _title;
        private int _shaHistoryIndex = 0;
        private List<string> _shaHistory = [];
        private Models.BlameData _data = null;
        private Dictionary<string, string> _commitMessages = new Dictionary<string, string>();
    }
}
