using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class SearchCommitContext : ObservableObject, IDisposable
    {
        public int Method
        {
            get => _method;
            set
            {
                if (SetProperty(ref _method, value))
                {
                    UpdateSuggestions();
                    StartSearch();
                }
            }
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                    UpdateSuggestions();
            }
        }

        public bool OnlySearchCurrentBranch
        {
            get => _onlySearchCurrentBranch;
            set
            {
                if (SetProperty(ref _onlySearchCurrentBranch, value))
                    StartSearch();
            }
        }

        public List<string> Suggestions
        {
            get => _suggestions;
            private set => SetProperty(ref _suggestions, value);
        }

        public bool IsQuerying
        {
            get => _isQuerying;
            private set => SetProperty(ref _isQuerying, value);
        }

        public List<Models.Commit> Results
        {
            get => _results;
            private set => SetProperty(ref _results, value);
        }

        public Models.Commit Selected
        {
            get => _selected;
            set
            {
                if (SetProperty(ref _selected, value) && value != null)
                    _repo.NavigateToCommit(value.SHA);
            }
        }

        public SearchCommitContext(Repository repo)
        {
            _repo = repo;
        }

        public void Dispose()
        {
            _repo = null;
            _suggestions?.Clear();
            _results?.Clear();
            _worktreeFiles?.Clear();
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
            Selected = null;
            Results = null;
        }

        public void ClearSuggestions()
        {
            Suggestions = null;
        }

        public void StartSearch()
        {
            Results = null;
            Selected = null;
            Suggestions = null;

            if (string.IsNullOrEmpty(_filter))
                return;

            IsQuerying = true;

            Task.Run(async () =>
            {
                var result = new List<Models.Commit>();
                var method = (Models.CommitSearchMethod)_method;
                var repoPath = _repo.FullPath;

                if (method == Models.CommitSearchMethod.BySHA)
                {
                    var isCommitSHA = await new Commands.IsCommitSHA(repoPath, _filter)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    if (isCommitSHA)
                    {
                        var commit = await new Commands.QuerySingleCommit(repoPath, _filter)
                            .GetResultAsync()
                            .ConfigureAwait(false);

                        commit.IsMerged = await new Commands.IsAncestor(repoPath, commit.SHA, "HEAD")
                            .GetResultAsync()
                            .ConfigureAwait(false);

                        result.Add(commit);
                    }
                }
                else if (_onlySearchCurrentBranch)
                {
                    result = await new Commands.QueryCommits(repoPath, _filter, method, true)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    foreach (var c in result)
                        c.IsMerged = true;
                }
                else
                {
                    result = await new Commands.QueryCommits(repoPath, _filter, method, false)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    if (result.Count > 0)
                    {
                        var set = await new Commands.QueryCurrentBranchCommitHashes(repoPath, result[^1].CommitterTime)
                            .GetResultAsync()
                            .ConfigureAwait(false);

                        foreach (var c in result)
                            c.IsMerged = set.Contains(c.SHA);
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    IsQuerying = false;

                    if (_repo.IsSearchingCommits)
                        Results = result;
                });
            });
        }

        public void EndSearch()
        {
            _worktreeFiles = null;
            Suggestions = null;
            Results = null;
            GC.Collect();
        }

        private void UpdateSuggestions()
        {
            if (_method != (int)Models.CommitSearchMethod.ByPath || _requestingWorktreeFiles)
            {
                Suggestions = null;
                return;
            }

            if (_worktreeFiles == null)
            {
                _requestingWorktreeFiles = true;

                Task.Run(async () =>
                {
                    var files = await new Commands.QueryRevisionFileNames(_repo.FullPath, "HEAD")
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    Dispatcher.UIThread.Post(() =>
                    {
                        _requestingWorktreeFiles = false;

                        if (_repo.IsSearchingCommits)
                        {
                            _worktreeFiles = files;
                            UpdateSuggestions();
                        }
                    });
                });

                return;
            }

            if (_worktreeFiles.Count == 0 || _filter.Length < 3)
            {
                Suggestions = null;
                return;
            }

            var matched = new List<string>();
            foreach (var file in _worktreeFiles)
            {
                if (file.Contains(_filter, StringComparison.OrdinalIgnoreCase) && file.Length != _filter.Length)
                {
                    matched.Add(file);
                    if (matched.Count > 100)
                        break;
                }
            }

            Suggestions = matched;
        }

        private Repository _repo = null;
        private int _method = (int)Models.CommitSearchMethod.ByMessage;
        private string _filter = string.Empty;
        private bool _onlySearchCurrentBranch = false;
        private List<string> _suggestions = null;
        private bool _isQuerying = false;
        private List<Models.Commit> _results = null;
        private Models.Commit _selected = null;
        private bool _requestingWorktreeFiles = false;
        private List<string> _worktreeFiles = null;
    }
}
