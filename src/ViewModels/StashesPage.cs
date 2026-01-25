using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class StashesPage : ObservableObject, IDisposable
    {
        private const int MaxOFPASampleSize = 256 * 1024;

        public IReadOnlyDictionary<string, string> DecodedPaths
        {
            get
            {
                if (_repo == null || !_repo.Settings.EnableUnrealEngineSupport || !_repo.Settings.EnableOFPADecoding)
                    return null;
                return _decodedPaths;
            }
        }

        public List<Models.Stash> Stashes
        {
            get => _stashes;
            set
            {
                if (SetProperty(ref _stashes, value))
                    RefreshVisible();
            }
        }

        public List<Models.Stash> VisibleStashes
        {
            get => _visibleStashes;
            private set
            {
                if (SetProperty(ref _visibleStashes, value))
                    SelectedStash = null;
            }
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    RefreshVisible();
            }
        }

        public Models.Stash SelectedStash
        {
            get => _selectedStash;
            set
            {
                if (SetProperty(ref _selectedStash, value))
                {
                    if (value == null)
                    {
                        Changes = null;
                        _untracked.Clear();
                    }
                    else
                    {
                        Task.Run(async () =>
                        {
                            var changes = await new Commands.CompareRevisions(_repo.FullPath, $"{value.SHA}^", value.SHA)
                                .ReadAsync()
                                .ConfigureAwait(false);

                            var untracked = new List<Models.Change>();
                            if (value.Parents.Count == 3)
                            {
                                untracked = await new Commands.CompareRevisions(_repo.FullPath, Models.Commit.EmptyTreeSHA1, value.Parents[2])
                                    .ReadAsync()
                                    .ConfigureAwait(false);

                                var needSort = changes.Count > 0 && untracked.Count > 0;
                                changes.AddRange(untracked);
                                if (needSort)
                                    changes.Sort((l, r) => Models.NumericSort.Compare(l.Path, r.Path));
                            }

                            Dispatcher.UIThread.Post(() =>
                            {
                                if (value.SHA.Equals(_selectedStash?.SHA ?? string.Empty, StringComparison.Ordinal))
                                {
                                    _untracked = untracked;
                                    Changes = changes;

                                    if (_repo.Settings.EnableUnrealEngineSupport && _repo.Settings.EnableOFPADecoding)
                                        _currentDecodeTask = DecodeOFPAPathsAsync(value, changes, untracked);
                                }
                            });
                        });
                    }
                }
            }
        }

        public List<Models.Change> Changes
        {
            get => _changes;
            private set
            {
                if (SetProperty(ref _changes, value))
                    SelectedChanges = value is { Count: > 0 } ? [value[0]] : [];
            }
        }

        public List<Models.Change> SelectedChanges
        {
            get => _selectedChanges;
            set
            {
                if (SetProperty(ref _selectedChanges, value))
                {
                    if (value is not { Count: 1 })
                        DiffContext = null;
                    else if (_untracked.Contains(value[0]))
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption(Models.Commit.EmptyTreeSHA1, _selectedStash.Parents[2], value[0]), _diffContext);
                    else
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption(_selectedStash.Parents[0], _selectedStash.SHA, value[0]), _diffContext);
                }
            }
        }

        public DiffContext DiffContext
        {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        public StashesPage(Repository repo)
        {
            _repo = repo;
            if (_repo != null)
                _repo.PropertyChanged += OnRepositoryPropertyChanged;
        }

        public void Dispose()
        {
            if (_repo != null)
                _repo.PropertyChanged -= OnRepositoryPropertyChanged;

            _stashes?.Clear();
            _changes?.Clear();
            _selectedChanges?.Clear();
            _untracked.Clear();

            _repo = null;
            _selectedStash = null;
            _diffContext = null;
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        public string GetAbsPath(string path)
        {
            return Native.OS.GetAbsPath(_repo.FullPath, path);
        }

        public void Apply(Models.Stash stash)
        {
            if (_repo.CanCreatePopup())
                _repo.ShowPopup(new ApplyStash(_repo, stash));
        }

        public void Drop(Models.Stash stash)
        {
            if (_repo.CanCreatePopup())
                _repo.ShowPopup(new DropStash(_repo, stash));
        }

        public async Task SaveStashAsPatchAsync(Models.Stash stash, string saveTo)
        {
            var opts = new List<Models.DiffOption>();
            var changes = await new Commands.CompareRevisions(_repo.FullPath, $"{stash.SHA}^", stash.SHA)
                .ReadAsync()
                .ConfigureAwait(false);

            foreach (var c in changes)
                opts.Add(new Models.DiffOption(_selectedStash.Parents[0], _selectedStash.SHA, c));

            if (stash.Parents.Count == 3)
            {
                var untracked = await new Commands.CompareRevisions(_repo.FullPath, Models.Commit.EmptyTreeSHA1, stash.Parents[2])
                    .ReadAsync()
                    .ConfigureAwait(false);

                foreach (var c in untracked)
                    opts.Add(new Models.DiffOption(Models.Commit.EmptyTreeSHA1, _selectedStash.Parents[2], c));

                changes.AddRange(untracked);
            }

            var succ = await Commands.SaveChangesAsPatch.ProcessStashChangesAsync(_repo.FullPath, opts, saveTo);
            if (succ)
                App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
        }

        public void OpenChangeWithExternalDiffTool(Models.Change change)
        {
            Models.DiffOption opt;
            if (_untracked.Contains(change))
                opt = new Models.DiffOption(Models.Commit.EmptyTreeSHA1, _selectedStash.Parents[2], change);
            else
                opt = new Models.DiffOption(_selectedStash.Parents[0], _selectedStash.SHA, change);

            new Commands.DiffTool(_repo.FullPath, opt).Open();
        }

        public async Task CheckoutSingleFileAsync(Models.Change change)
        {
            var revision = _selectedStash.SHA;
            if (_untracked.Contains(change) && _selectedStash.Parents.Count == 3)
                revision = _selectedStash.Parents[2];
            else if (change.Index == Models.ChangeState.Added && _selectedStash.Parents.Count > 1)
                revision = _selectedStash.Parents[1];

            var log = _repo.CreateLog($"Reset File to '{_selectedStash.Name}'");
            await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.Path, revision);
            log.Complete();
        }

        public async Task CheckoutMultipleFileAsync(List<Models.Change> changes)
        {
            var untracked = new List<string>();
            var added = new List<string>();
            var modified = new List<string>();

            foreach (var c in changes)
            {
                if (_untracked.Contains(c) && _selectedStash.Parents.Count == 3)
                    untracked.Add(c.Path);
                else if (c.Index == Models.ChangeState.Added && _selectedStash.Parents.Count > 1)
                    added.Add(c.Path);
                else
                    modified.Add(c.Path);
            }

            var log = _repo.CreateLog($"Reset File to '{_selectedStash.Name}'");

            if (untracked.Count > 0)
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .MultipleFilesWithRevisionAsync(untracked, _selectedStash.Parents[2]);

            if (added.Count > 0)
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .MultipleFilesWithRevisionAsync(added, _selectedStash.Parents[1]);

            if (modified.Count > 0)
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .MultipleFilesWithRevisionAsync(modified, _selectedStash.SHA);

            log.Complete();
        }

        private void RefreshVisible()
        {
            if (string.IsNullOrEmpty(_searchFilter))
            {
                VisibleStashes = _stashes;
            }
            else
            {
                var visible = new List<Models.Stash>();
                foreach (var s in _stashes)
                {
                    if (s.Message.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(s);
                }

                VisibleStashes = visible;
            }
        }

        private void OnRepositoryPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Repository.EnableUnrealEngineSupport))
            {
                OnPropertyChanged(nameof(DecodedPaths));
                if (_repo.Settings.EnableUnrealEngineSupport && _repo.Settings.EnableOFPADecoding && _selectedStash != null)
                    _currentDecodeTask = DecodeOFPAPathsAsync(_selectedStash, _changes, _untracked);
            }
        }

        private async Task DecodeOFPAPathsAsync(Models.Stash stash, List<Models.Change> changes, List<Models.Change> untracked)
        {
            await _currentDecodeTask.ConfigureAwait(false);

            if (_repo == null || stash == null ||
                !_repo.Settings.EnableUnrealEngineSupport ||
                !_repo.Settings.EnableOFPADecoding ||
                changes == null || changes.Count == 0)
                return;

            var repositoryPath = _repo.FullPath;
            var untrackedSet = new HashSet<Models.Change>(untracked);
            var results = new Dictionary<string, string>(StringComparer.Ordinal);

            await Task.Run(async () =>
            {
                var filesToDecode = new List<(string RelativePath, string Spec)>();
                foreach (var change in changes)
                {
                    if (!Utilities.OFPAParser.IsOFPAFile(change.Path))
                        continue;

                    string spec;
                    if (untrackedSet.Contains(change) && stash.Parents.Count == 3)
                    {
                        // Untracked files are in the 3rd parent commit.
                        spec = $"{stash.Parents[2]}:{change.Path}";
                    }
                    else
                    {
                        // Standard stash changes (index + worktree).
                        // Deleted files need to be looked up in the parent.
                        if (change.WorkTree == Models.ChangeState.Deleted || change.Index == Models.ChangeState.Deleted)
                            spec = $"{stash.Parents[0]}:{change.Path}";
                        else
                            spec = $"{stash.SHA}:{change.Path}";
                    }

                    filesToDecode.Add((change.Path, spec));
                }

                if (filesToDecode.Count == 0)
                    return;

                var batchRequests = new List<string>();
                foreach (var entry in filesToDecode)
                    batchRequests.Add(entry.Spec);

                var batchResults = await Commands.QueryFileContent.RunBatchAsync(repositoryPath, batchRequests, MaxOFPASampleSize).ConfigureAwait(false);
                foreach (var entry in filesToDecode)
                {
                    if (batchResults.TryGetValue(entry.Spec, out var data))
                    {
                        var decoded = Utilities.OFPAParser.DecodeFromData(data);
                        results[entry.RelativePath] = decoded?.LabelValue;
                    }
                }

                var updated = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var kvp in results)
                    updated[kvp.Key] = kvp.Value;
                _decodedPaths = updated;
            });

            if (results.Count > 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_repo == null || !_repo.Settings.EnableUnrealEngineSupport || !_repo.Settings.EnableOFPADecoding)
                        return;

                    OnPropertyChanged(nameof(DecodedPaths));
                });
            }
        }

        private Repository _repo = null;
        private List<Models.Stash> _stashes = [];
        private List<Models.Stash> _visibleStashes = [];
        private string _searchFilter = string.Empty;
        private Models.Stash _selectedStash = null;
        private List<Models.Change> _changes = null;
        private List<Models.Change> _untracked = [];
        private List<Models.Change> _selectedChanges = [];
        private DiffContext _diffContext = null;
        private Dictionary<string, string> _decodedPaths = null;
        private Task _currentDecodeTask = Task.CompletedTask;
    }
}
