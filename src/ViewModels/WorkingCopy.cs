using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class WorkingCopy : ObservableObject, IDisposable
    {
        public Repository Repository
        {
            get => _repo;
        }

        public bool IncludeUntracked
        {
            get => _repo.IncludeUntracked;
            set
            {
                if (_repo.IncludeUntracked != value)
                {
                    _repo.IncludeUntracked = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasRemotes
        {
            get => _hasRemotes;
            set => SetProperty(ref _hasRemotes, value);
        }

        public bool HasUnsolvedConflicts
        {
            get => _hasUnsolvedConflicts;
            set => SetProperty(ref _hasUnsolvedConflicts, value);
        }

        public InProgressContext InProgressContext
        {
            get => _inProgressContext;
            private set => SetProperty(ref _inProgressContext, value);
        }

        public bool IsStaging
        {
            get => _isStaging;
            private set => SetProperty(ref _isStaging, value);
        }

        public bool IsUnstaging
        {
            get => _isUnstaging;
            private set => SetProperty(ref _isUnstaging, value);
        }

        public bool IsCommitting
        {
            get => _isCommitting;
            private set => SetProperty(ref _isCommitting, value);
        }

        public bool EnableSignOff
        {
            get => _repo.Settings.EnableSignOffForCommit;
            set => _repo.Settings.EnableSignOffForCommit = value;
        }

        public bool NoVerifyOnCommit
        {
            get => _repo.Settings.NoVerifyOnCommit;
            set => _repo.Settings.NoVerifyOnCommit = value;
        }

        public bool UseAmend
        {
            get => _useAmend;
            set
            {
                if (SetProperty(ref _useAmend, value))
                {
                    if (value)
                    {
                        var currentBranch = _repo.CurrentBranch;
                        if (currentBranch == null)
                        {
                            App.RaiseException(_repo.FullPath, "No commits to amend!!!");
                            _useAmend = false;
                            OnPropertyChanged();
                            return;
                        }

                        CommitMessage = new Commands.QueryCommitFullMessage(_repo.FullPath, currentBranch.Head).GetResult();
                    }
                    else
                    {
                        CommitMessage = string.Empty;
                        ResetAuthor = false;
                    }

                    Staged = GetStagedChanges();
                    VisibleStaged = GetVisibleChanges(_staged);
                    SelectedStaged = [];
                }
            }
        }

        public bool ResetAuthor
        {
            get => _resetAuthor;
            set => SetProperty(ref _resetAuthor, value);
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                {
                    if (_isLoadingData)
                        return;

                    VisibleUnstaged = GetVisibleChanges(_unstaged);
                    VisibleStaged = GetVisibleChanges(_staged);
                    SelectedUnstaged = [];
                }
            }
        }

        public List<Models.Change> Unstaged
        {
            get => _unstaged;
            private set => SetProperty(ref _unstaged, value);
        }

        public List<Models.Change> VisibleUnstaged
        {
            get => _visibleUnstaged;
            private set => SetProperty(ref _visibleUnstaged, value);
        }

        public List<Models.Change> Staged
        {
            get => _staged;
            private set => SetProperty(ref _staged, value);
        }

        public List<Models.Change> VisibleStaged
        {
            get => _visibleStaged;
            private set => SetProperty(ref _visibleStaged, value);
        }

        public List<Models.Change> SelectedUnstaged
        {
            get => _selectedUnstaged;
            set
            {
                if (SetProperty(ref _selectedUnstaged, value))
                {
                    if (value == null || value.Count == 0)
                    {
                        if (_selectedStaged == null || _selectedStaged.Count == 0)
                            SetDetail(null, true);
                    }
                    else
                    {
                        if (_selectedStaged is { Count: > 0 })
                            SelectedStaged = [];

                        if (value.Count == 1)
                            SetDetail(value[0], true);
                        else
                            SetDetail(null, true);
                    }
                }
            }
        }

        public List<Models.Change> SelectedStaged
        {
            get => _selectedStaged;
            set
            {
                if (SetProperty(ref _selectedStaged, value))
                {
                    if (value == null || value.Count == 0)
                    {
                        if (_selectedUnstaged == null || _selectedUnstaged.Count == 0)
                            SetDetail(null, false);
                    }
                    else
                    {
                        if (_selectedUnstaged is { Count: > 0 })
                            SelectedUnstaged = [];

                        if (value.Count == 1)
                            SetDetail(value[0], false);
                        else
                            SetDetail(null, false);
                    }
                }
            }
        }

        public object DetailContext
        {
            get => _detailContext;
            private set => SetProperty(ref _detailContext, value);
        }

        public string CommitMessage
        {
            get => _commitMessage;
            set => SetProperty(ref _commitMessage, value);
        }

        public WorkingCopy(Repository repo)
        {
            _repo = repo;
        }

        public void Dispose()
        {
            _repo = null;
            _inProgressContext = null;

            _selectedUnstaged.Clear();
            OnPropertyChanged(nameof(SelectedUnstaged));

            _selectedStaged.Clear();
            OnPropertyChanged(nameof(SelectedStaged));

            _visibleUnstaged.Clear();
            OnPropertyChanged(nameof(VisibleUnstaged));

            _visibleStaged.Clear();
            OnPropertyChanged(nameof(VisibleStaged));

            _unstaged.Clear();
            OnPropertyChanged(nameof(Unstaged));

            _staged.Clear();
            OnPropertyChanged(nameof(Staged));

            _detailContext = null;
            _commitMessage = string.Empty;
        }

        public void SetData(List<Models.Change> changes, CancellationToken cancellationToken)
        {
            if (!IsChanged(_cached, changes))
            {
                // Just force refresh selected changes.
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    HasUnsolvedConflicts = _cached.Find(x => x.IsConflicted) != null;
                    UpdateDetail();
                    UpdateInProgressState();
                });

                return;
            }

            _cached = changes;

            var lastSelectedUnstaged = new HashSet<string>();
            var lastSelectedStaged = new HashSet<string>();
            if (_selectedUnstaged is { Count: > 0 })
            {
                foreach (var c in _selectedUnstaged)
                    lastSelectedUnstaged.Add(c.Path);
            }
            else if (_selectedStaged is { Count: > 0 })
            {
                foreach (var c in _selectedStaged)
                    lastSelectedStaged.Add(c.Path);
            }

            var unstaged = new List<Models.Change>();
            var hasConflict = false;
            foreach (var c in changes)
            {
                if (c.WorkTree != Models.ChangeState.None)
                {
                    unstaged.Add(c);
                    hasConflict |= c.IsConflicted;
                }
            }

            var visibleUnstaged = GetVisibleChanges(unstaged);
            var selectedUnstaged = new List<Models.Change>();
            foreach (var c in visibleUnstaged)
            {
                if (lastSelectedUnstaged.Contains(c.Path))
                    selectedUnstaged.Add(c);
            }

            var staged = GetStagedChanges();

            var visibleStaged = GetVisibleChanges(staged);
            var selectedStaged = new List<Models.Change>();
            foreach (var c in visibleStaged)
            {
                if (lastSelectedStaged.Contains(c.Path))
                    selectedStaged.Add(c);
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                _isLoadingData = true;
                HasUnsolvedConflicts = hasConflict;
                VisibleUnstaged = visibleUnstaged;
                VisibleStaged = visibleStaged;
                Unstaged = unstaged;
                Staged = staged;
                SelectedUnstaged = selectedUnstaged;
                SelectedStaged = selectedStaged;
                _isLoadingData = false;

                UpdateDetail();
                UpdateInProgressState();
            });
        }

        public void OpenWithDefaultEditor(Models.Change c)
        {
            var absPath = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
            if (File.Exists(absPath))
                Native.OS.OpenWithDefaultEditor(absPath);
        }

        public async Task StageChangesAsync(List<Models.Change> changes, Models.Change next)
        {
            var canStaged = await GetCanStageChangesAsync(changes);
            var count = canStaged.Count;
            if (count == 0)
                return;

            IsStaging = true;
            _selectedUnstaged = next != null ? [next] : [];

            using var lockWatcher = _repo.LockWatcher();

            var log = _repo.CreateLog("Stage");
            if (count == _unstaged.Count)
            {
                await new Commands.Add(_repo.FullPath, _repo.IncludeUntracked).Use(log).ExecAsync();
            }
            else
            {
                var pathSpecFile = Path.GetTempFileName();
                await using (var writer = new StreamWriter(pathSpecFile))
                {
                    foreach (var c in canStaged)
                        await writer.WriteLineAsync(c.Path);
                }

                await new Commands.Add(_repo.FullPath, pathSpecFile).Use(log).ExecAsync();
                File.Delete(pathSpecFile);
            }
            log.Complete();

            _repo.MarkWorkingCopyDirtyManually();
            IsStaging = false;
        }

        public async Task UnstageChangesAsync(List<Models.Change> changes, Models.Change next)
        {
            var count = changes.Count;
            if (count == 0)
                return;

            IsUnstaging = true;
            _selectedStaged = next != null ? [next] : [];

            using var lockWatcher = _repo.LockWatcher();

            var log = _repo.CreateLog("Unstage");
            if (_useAmend)
            {
                log.AppendLine("$ git update-index --index-info ");
                await new Commands.UnstageChangesForAmend(_repo.FullPath, changes).ExecAsync();
            }
            else
            {
                var pathSpecFile = Path.GetTempFileName();
                await using (var writer = new StreamWriter(pathSpecFile))
                {
                    foreach (var c in changes)
                    {
                        await writer.WriteLineAsync(c.Path);
                        if (c.Index == Models.ChangeState.Renamed)
                            await writer.WriteLineAsync(c.OriginalPath);
                    }
                }

                await new Commands.Restore(_repo.FullPath, pathSpecFile, true).Use(log).ExecAsync();
                File.Delete(pathSpecFile);
            }
            log.Complete();

            _repo.MarkWorkingCopyDirtyManually();
            IsUnstaging = false;
        }

        public async Task SaveChangesToPatchAsync(List<Models.Change> changes, bool isUnstaged, string saveTo)
        {
            var succ = await Commands.SaveChangesAsPatch.ProcessLocalChangesAsync(_repo.FullPath, changes, isUnstaged, saveTo);
            if (succ)
                App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
        }

        public void Discard(List<Models.Change> changes)
        {
            if (_repo.CanCreatePopup())
                _repo.ShowPopup(new Discard(_repo, changes));
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public async Task UseTheirsAsync(List<Models.Change> changes)
        {
            using var lockWatcher = _repo.LockWatcher();

            var files = new List<string>();
            var needStage = new List<string>();
            var log = _repo.CreateLog("Use Theirs");

            foreach (var change in changes)
            {
                if (!change.IsConflicted)
                    continue;

                if (change.ConflictReason is Models.ConflictReason.BothDeleted or Models.ConflictReason.DeletedByThem or Models.ConflictReason.AddedByUs)
                {
                    var fullpath = Path.Combine(_repo.FullPath, change.Path);
                    if (File.Exists(fullpath))
                        File.Delete(fullpath);

                    needStage.Add(change.Path);
                }
                else
                {
                    files.Add(change.Path);
                }
            }

            if (files.Count > 0)
            {
                var succ = await new Commands.Checkout(_repo.FullPath).Use(log).UseTheirsAsync(files);
                if (succ)
                    needStage.AddRange(files);
            }

            if (needStage.Count > 0)
            {
                var pathSpecFile = Path.GetTempFileName();
                await File.WriteAllLinesAsync(pathSpecFile, needStage);
                await new Commands.Add(_repo.FullPath, pathSpecFile).Use(log).ExecAsync();
                File.Delete(pathSpecFile);
            }

            log.Complete();
            _repo.MarkWorkingCopyDirtyManually();
        }

        public async Task UseMineAsync(List<Models.Change> changes)
        {
            using var lockWatcher = _repo.LockWatcher();

            var files = new List<string>();
            var needStage = new List<string>();
            var log = _repo.CreateLog("Use Mine");

            foreach (var change in changes)
            {
                if (!change.IsConflicted)
                    continue;

                if (change.ConflictReason is Models.ConflictReason.BothDeleted or Models.ConflictReason.DeletedByUs or Models.ConflictReason.AddedByThem)
                {
                    var fullpath = Path.Combine(_repo.FullPath, change.Path);
                    if (File.Exists(fullpath))
                        File.Delete(fullpath);

                    needStage.Add(change.Path);
                }
                else
                {
                    files.Add(change.Path);
                }
            }

            if (files.Count > 0)
            {
                var succ = await new Commands.Checkout(_repo.FullPath).Use(log).UseMineAsync(files);
                if (succ)
                    needStage.AddRange(files);
            }

            if (needStage.Count > 0)
            {
                var pathSpecFile = Path.GetTempFileName();
                await File.WriteAllLinesAsync(pathSpecFile, needStage);
                await new Commands.Add(_repo.FullPath, pathSpecFile).Use(log).ExecAsync();
                File.Delete(pathSpecFile);
            }

            log.Complete();
            _repo.MarkWorkingCopyDirtyManually();
        }

        public async Task<bool> UseExternalMergeToolAsync(Models.Change change)
        {
            return await new Commands.MergeTool(_repo.FullPath, change?.Path).OpenAsync();
        }

        public void UseExternalDiffTool(Models.Change change, bool isUnstaged)
        {
            new Commands.DiffTool(_repo.FullPath, new Models.DiffOption(change, isUnstaged)).Open();
        }

        public async Task ContinueMergeAsync()
        {
            if (_inProgressContext != null)
            {
                using var lockWatcher = _repo.LockWatcher();
                IsCommitting = true;

                var mergeMsgFile = Path.Combine(_repo.GitDir, "MERGE_MSG");
                if (File.Exists(mergeMsgFile) && !string.IsNullOrWhiteSpace(_commitMessage))
                    await File.WriteAllTextAsync(mergeMsgFile, _commitMessage);

                var succ = await _inProgressContext.ContinueAsync();
                if (succ)
                    CommitMessage = string.Empty;

                IsCommitting = false;
            }
            else
            {
                _repo.MarkWorkingCopyDirtyManually();
            }
        }

        public async Task SkipMergeAsync()
        {
            if (_inProgressContext != null)
            {
                using var lockWatcher = _repo.LockWatcher();
                IsCommitting = true;

                var succ = await _inProgressContext.SkipAsync();
                if (succ)
                    CommitMessage = string.Empty;

                IsCommitting = false;
            }
            else
            {
                _repo.MarkWorkingCopyDirtyManually();
            }
        }

        public async Task AbortMergeAsync()
        {
            if (_inProgressContext != null)
            {
                using var lockWatcher = _repo.LockWatcher();
                IsCommitting = true;

                var succ = await _inProgressContext.AbortAsync();
                if (succ)
                    CommitMessage = string.Empty;

                IsCommitting = false;
            }
            else
            {
                _repo.MarkWorkingCopyDirtyManually();
            }
        }

        public void ApplyCommitMessageTemplate(Models.CommitTemplate tmpl)
        {
            CommitMessage = tmpl.Apply(_repo.CurrentBranch, _staged);
        }

        public async Task ClearCommitMessageHistoryAsync()
        {
            var sure = await App.AskConfirmAsync(App.Text("WorkingCopy.ClearCommitHistories.Confirm"));
            if (sure)
                _repo.Settings.CommitMessages.Clear();
        }

        public async Task CommitAsync(bool autoStage, bool autoPush)
        {
            if (string.IsNullOrWhiteSpace(_commitMessage))
                return;

            if (!_repo.CanCreatePopup())
            {
                App.RaiseException(_repo.FullPath, "Repository has an unfinished job! Please wait!");
                return;
            }

            if (autoStage && HasUnsolvedConflicts)
            {
                App.RaiseException(_repo.FullPath, "Repository has unsolved conflict(s). Auto-stage and commit is disabled!");
                return;
            }

            if (_repo.CurrentBranch is { IsDetachedHead: true })
            {
                var msg = App.Text("WorkingCopy.ConfirmCommitWithDetachedHead");
                var sure = await App.AskConfirmAsync(msg);
                if (!sure)
                    return;
            }

            if (!string.IsNullOrEmpty(_filter) && _staged.Count > _visibleStaged.Count)
            {
                var msg = App.Text("WorkingCopy.ConfirmCommitWithFilter", _staged.Count, _visibleStaged.Count, _staged.Count - _visibleStaged.Count);
                var sure = await App.AskConfirmAsync(msg);
                if (!sure)
                    return;
            }

            if (!_useAmend)
            {
                if ((!autoStage && _staged.Count == 0) || (autoStage && _cached.Count == 0))
                {
                    var rs = await App.AskConfirmEmptyCommitAsync(_cached.Count > 0);
                    if (rs == Models.ConfirmEmptyCommitResult.Cancel)
                        return;

                    if (rs == Models.ConfirmEmptyCommitResult.StageAllAndCommit)
                        autoStage = true;
                }
            }

            using var lockWatcher = _repo.LockWatcher();
            IsCommitting = true;
            _repo.Settings.PushCommitMessage(_commitMessage);

            var log = _repo.CreateLog("Commit");
            var succ = true;
            if (autoStage && _unstaged.Count > 0)
                succ = await new Commands.Add(_repo.FullPath, _repo.IncludeUntracked)
                    .Use(log)
                    .ExecAsync()
                    .ConfigureAwait(false);

            if (succ)
                succ = await new Commands.Commit(_repo.FullPath, _commitMessage, EnableSignOff, NoVerifyOnCommit, _useAmend, _resetAuthor)
                    .Use(log)
                    .RunAsync()
                    .ConfigureAwait(false);

            log.Complete();

            if (succ)
            {
                CommitMessage = string.Empty;
                UseAmend = false;
                if (autoPush && _repo.Remotes.Count > 0)
                {
                    Models.Branch pushBranch = null;
                    if (_repo.CurrentBranch == null)
                    {
                        var currentBranchName = await new Commands.QueryCurrentBranch(_repo.FullPath).GetResultAsync();
                        pushBranch = new Models.Branch() { Name = currentBranchName };
                    }

                    if (_repo.CanCreatePopup())
                        await _repo.ShowAndStartPopupAsync(new Push(_repo, pushBranch));
                }
            }

            _repo.MarkBranchesDirtyManually();
            IsCommitting = false;
        }

        private List<Models.Change> GetVisibleChanges(List<Models.Change> changes)
        {
            if (string.IsNullOrEmpty(_filter))
                return changes;

            var visible = new List<Models.Change>();

            foreach (var c in changes)
            {
                if (c.Path.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    visible.Add(c);
            }

            return visible;
        }

        private async Task<List<Models.Change>> GetCanStageChangesAsync(List<Models.Change> changes)
        {
            if (!HasUnsolvedConflicts)
                return changes;

            var outs = new List<Models.Change>();
            foreach (var c in changes)
            {
                if (c.IsConflicted)
                {
                    var isResolved = c.ConflictReason switch
                    {
                        Models.ConflictReason.BothAdded or Models.ConflictReason.BothModified =>
                            await new Commands.IsConflictResolved(_repo.FullPath, c).GetResultAsync(),
                        _ => false,
                    };

                    if (!isResolved)
                        continue;
                }

                outs.Add(c);
            }

            return outs;
        }

        private List<Models.Change> GetStagedChanges()
        {
            if (_useAmend)
            {
                var head = new Commands.QuerySingleCommit(_repo.FullPath, "HEAD").GetResult();
                return new Commands.QueryStagedChangesWithAmend(_repo.FullPath, head.Parents.Count == 0 ? Models.Commit.EmptyTreeSHA1 : $"{head.SHA}^").GetResult();
            }

            var rs = new List<Models.Change>();
            foreach (var c in _cached)
            {
                if (c.Index != Models.ChangeState.None)
                    rs.Add(c);
            }
            return rs;
        }

        private void UpdateDetail()
        {
            if (_selectedUnstaged.Count == 1)
                SetDetail(_selectedUnstaged[0], true);
            else if (_selectedStaged.Count == 1)
                SetDetail(_selectedStaged[0], false);
            else
                SetDetail(null, false);
        }

        private void UpdateInProgressState()
        {
            var oldType = _inProgressContext != null ? _inProgressContext.GetType() : null;

            if (File.Exists(Path.Combine(_repo.GitDir, "CHERRY_PICK_HEAD")))
                InProgressContext = new CherryPickInProgress(_repo);
            else if (Directory.Exists(Path.Combine(_repo.GitDir, "rebase-merge")) || Directory.Exists(Path.Combine(_repo.GitDir, "rebase-apply")))
                InProgressContext = new RebaseInProgress(_repo);
            else if (File.Exists(Path.Combine(_repo.GitDir, "REVERT_HEAD")))
                InProgressContext = new RevertInProgress(_repo);
            else if (File.Exists(Path.Combine(_repo.GitDir, "MERGE_HEAD")))
                InProgressContext = new MergeInProgress(_repo);
            else
                InProgressContext = null;

            if (_inProgressContext == null)
                return;

            if (_inProgressContext.GetType() == oldType && !string.IsNullOrEmpty(_commitMessage))
                return;

            do
            {
                if (LoadCommitMessageFromFile(Path.Combine(_repo.GitDir, "MERGE_MSG")))
                    break;

                if (LoadCommitMessageFromFile(Path.Combine(_repo.GitDir, "rebase-merge", "message")))
                    break;

                if (_inProgressContext is RebaseInProgress { StoppedAt: { } stopAt })
                    CommitMessage = new Commands.QueryCommitFullMessage(_repo.FullPath, stopAt.SHA).GetResult();
            } while (false);
        }

        private bool LoadCommitMessageFromFile(string file)
        {
            if (!File.Exists(file))
                return false;

            var msg = File.ReadAllText(file).Trim();
            if (string.IsNullOrEmpty(msg))
                return false;

            CommitMessage = msg;
            return true;
        }

        private void SetDetail(Models.Change change, bool isUnstaged)
        {
            if (_isLoadingData)
                return;

            if (change == null)
                DetailContext = null;
            else if (change.IsConflicted)
                DetailContext = new Conflict(_repo, this, change);
            else
                DetailContext = new DiffContext(_repo.FullPath, new Models.DiffOption(change, isUnstaged), _detailContext as DiffContext);
        }

        private bool IsChanged(List<Models.Change> old, List<Models.Change> cur)
        {
            if (old.Count != cur.Count)
                return true;

            for (int idx = 0; idx < old.Count; idx++)
            {
                var o = old[idx];
                var c = cur[idx];
                if (o.Path != c.Path || o.Index != c.Index || o.WorkTree != c.WorkTree)
                    return true;
            }

            return false;
        }

        private Repository _repo = null;
        private bool _isLoadingData = false;
        private bool _isStaging = false;
        private bool _isUnstaging = false;
        private bool _isCommitting = false;
        private bool _useAmend = false;
        private bool _resetAuthor = false;
        private bool _hasRemotes = false;
        private List<Models.Change> _cached = [];
        private List<Models.Change> _unstaged = [];
        private List<Models.Change> _visibleUnstaged = [];
        private List<Models.Change> _staged = [];
        private List<Models.Change> _visibleStaged = [];
        private List<Models.Change> _selectedUnstaged = [];
        private List<Models.Change> _selectedStaged = [];
        private object _detailContext = null;
        private string _filter = string.Empty;
        private string _commitMessage = string.Empty;

        private bool _hasUnsolvedConflicts = false;
        private InProgressContext _inProgressContext = null;
    }
}
