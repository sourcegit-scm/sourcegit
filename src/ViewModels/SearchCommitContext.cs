using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class SearchCommitContext : ObservableObject
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

        public List<object> Suggestions
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

            if (_cancellation is { IsCancellationRequested: false })
                _cancellation.Cancel();

            _cancellation = new();
            var token = _cancellation.Token;

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
                    if (token.IsCancellationRequested)
                        return;

                    IsQuerying = false;
                    if (_repo.IsSearchingCommits)
                    {
                        Results = result;
                        if (method == Models.CommitSearchMethod.BySHA && result.Count == 1)
                            Selected = result[0];
                    }
                });
            }, token);
        }

        public void EndSearch()
        {
            if (_cancellation is { IsCancellationRequested: false })
                _cancellation.Cancel();

            _worktreeFiles = null;
            _authors = null;

            IsQuerying = false;
            Suggestions = null;
            Results = null;
            GC.Collect();
        }

        private void UpdateSuggestions()
        {
            if (_method == (int)Models.CommitSearchMethod.ByAuthor)
            {
                if (_authors == null)
                {
                    if (_requestingAuthors)
                        return;

                    _requestingAuthors = true;

                    Task.Run(async () =>
                    {
                        var authors = await new Commands.QueryAuthors(_repo.FullPath)
                            .GetResultAsync()
                            .ConfigureAwait(false);

                        Dispatcher.UIThread.Post(() =>
                        {
                            _requestingAuthors = false;

                            if (_repo.IsSearchingCommits)
                            {
                                _authors = authors;
                                UpdateSuggestions();
                            }
                        });
                    });

                    return;
                }

                if (_authors.Count == 0 || _filter.Length < 2)
                {
                    Suggestions = null;
                    return;
                }

                var matched = new List<object>();
                foreach (var author in _authors)
                {
                    if (author.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                        author.Email.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                        matched.Add(author);
                }

                Suggestions = matched;
            }
            else if (_method == (int)Models.CommitSearchMethod.ByPath)
            {
                if (_worktreeFiles == null)
                {
                    if (_requestingWorktreeFiles)
                        return;

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

                var matched = new List<object>();
                foreach (var file in _worktreeFiles)
                {
                    if (file.Contains(_filter, StringComparison.OrdinalIgnoreCase) && file.Length != _filter.Length)
                        matched.Add(file);
                }

                Suggestions = matched;
            }
            else
            {
                Suggestions = null;
                return;
            }
        }

        private Repository _repo = null;
        private CancellationTokenSource _cancellation = null;
        private int _method = (int)Models.CommitSearchMethod.ByMessage;
        private string _filter = string.Empty;
        private bool _onlySearchCurrentBranch = false;
        private bool _isQuerying = false;
        private List<Models.Commit> _results = null;
        private Models.Commit _selected = null;
        private bool _requestingWorktreeFiles = false;
        private List<string> _worktreeFiles = null;
        private bool _requestingAuthors = false;
        private List<Models.User> _authors = null;
        private List<object> _suggestions = null;
    }
}
