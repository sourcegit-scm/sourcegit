using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class DirHistories : ObservableObject
    {
        public string Title
        {
            get;
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public List<Models.Commit> Commits
        {
            get => _commits;
            private set => SetProperty(ref _commits, value);
        }

        public Models.Commit SelectedCommit
        {
            get => _selectedCommit;
            set
            {
                if (SetProperty(ref _selectedCommit, value))
                    Detail.Commit = value;
            }
        }

        public CommitDetail Detail
        {
            get => _detail;
        }

        public DirHistories(Repository repo, string dir, string revision = null)
        {
            if (!string.IsNullOrEmpty(revision))
                Title = $"{dir} @ {revision}";
            else
                Title = dir;

            _repo = repo;
            _detail = new CommitDetail(repo, false);
            _detail.SearchChangeFilter = dir;

            Task.Run(async () =>
            {
                var argsBuilder = new StringBuilder();
                argsBuilder
                    .Append("--date-order -n 10000 ")
                    .Append(revision ?? string.Empty)
                    .Append(" -- ")
                    .Append(dir.Quoted());

                var commits = await new Commands.QueryCommits(_repo.FullPath, argsBuilder.ToString(), false)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    Commits = commits;
                    IsLoading = false;

                    if (commits.Count > 0)
                        SelectedCommit = commits[0];
                });
            });
        }

        public void NavigateToCommit(Models.Commit commit)
        {
            _repo.NavigateToCommit(commit.SHA);
        }

        public string GetCommitFullMessage(Models.Commit commit)
        {
            var sha = commit.SHA;
            if (_cachedCommitFullMessage.TryGetValue(sha, out var msg))
                return msg;

            msg = new Commands.QueryCommitFullMessage(_repo.FullPath, sha).GetResult();
            _cachedCommitFullMessage[sha] = msg;
            return msg;
        }

        private Repository _repo = null;
        private bool _isLoading = true;
        private List<Models.Commit> _commits = [];
        private Models.Commit _selectedCommit = null;
        private CommitDetail _detail = null;
        private Dictionary<string, string> _cachedCommitFullMessage = new();
    }
}
