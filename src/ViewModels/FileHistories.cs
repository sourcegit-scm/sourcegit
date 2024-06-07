using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class FileHistories : ObservableObject
    {
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public List<Models.Commit> Commits
        {
            get => _commits;
            set => SetProperty(ref _commits, value);
        }

        public Models.Commit SelectedCommit
        {
            get => _selectedCommit;
            set
            {
                if (SetProperty(ref _selectedCommit, value))
                {
                    if (value == null)
                    {
                        DiffContext = null;
                        DetailContext.Commit = null;
                    }
                    else
                    {
                        DiffContext = new DiffContext(_repo, new Models.DiffOption(value, _file), _diffContext);
                        DetailContext.Commit = value;
                    }
                }
            }
        }

        public DiffContext DiffContext
        {
            get => _diffContext;
            set => SetProperty(ref _diffContext, value);
        }

        public CommitDetail DetailContext
        {
            get => _detailContext;
            set => SetProperty(ref _detailContext, value);
        }

        public FileHistories(string repo, string file)
        {
            _repo = repo;
            _file = file;
            _detailContext = new CommitDetail(repo);

            Task.Run(() =>
            {
                var commits = new Commands.QueryCommits(_repo, $"-n 10000 -- \"{file}\"", false).Result();
                Dispatcher.UIThread.Invoke(() =>
                {
                    IsLoading = false;
                    Commits = commits;
                    if (commits.Count > 0)
                        SelectedCommit = commits[0];
                });
            });
        }

        private readonly string _repo = string.Empty;
        private readonly string _file = string.Empty;
        private bool _isLoading = true;
        private List<Models.Commit> _commits = null;
        private Models.Commit _selectedCommit = null;
        private DiffContext _diffContext = null;
        private CommitDetail _detailContext = null;
    }
}
