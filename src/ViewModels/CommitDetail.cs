using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class CommitDetailSharedData
    {
        public int ActiveTabIndex
        {
            get;
            set;
        }

        public CommitDetailSharedData()
        {
            ActiveTabIndex = Preferences.Instance.ShowChangesInCommitDetailByDefault ? 1 : 0;
        }
    }

    public partial class CommitDetail : ObservableObject, IDisposable
    {
        public Repository Repository
        {
            get => _repo;
        }

        public int ActiveTabIndex
        {
            get => _sharedData.ActiveTabIndex;
            set
            {
                if (value != _sharedData.ActiveTabIndex)
                {
                    _sharedData.ActiveTabIndex = value;

                    if (value == 1 && DiffContext == null && _selectedChanges is { Count: 1 })
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption(_commit, _selectedChanges[0]));
                }
            }
        }

        public Models.Commit Commit
        {
            get => _commit;
            set
            {
                if (_commit != null && value != null && _commit.SHA.Equals(value.SHA, StringComparison.Ordinal))
                    return;

                if (SetProperty(ref _commit, value))
                    Refresh();
            }
        }

        public Models.CommitFullMessage FullMessage
        {
            get => _fullMessage;
            private set => SetProperty(ref _fullMessage, value);
        }

        public Models.CommitSignInfo SignInfo
        {
            get => _signInfo;
            private set => SetProperty(ref _signInfo, value);
        }

        public List<Models.CommitLink> WebLinks
        {
            get;
            private set;
        }

        public List<string> Children
        {
            get => _children;
            private set => SetProperty(ref _children, value);
        }

        public List<Models.Change> Changes
        {
            get => _changes;
            set => SetProperty(ref _changes, value);
        }

        public List<Models.Change> VisibleChanges
        {
            get => _visibleChanges;
            set => SetProperty(ref _visibleChanges, value);
        }

        public List<Models.Change> SelectedChanges
        {
            get => _selectedChanges;
            set
            {
                if (SetProperty(ref _selectedChanges, value))
                {
                    if (ActiveTabIndex != 1 || value is not { Count: 1 })
                        DiffContext = null;
                    else
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption(_commit, value[0]), _diffContext);
                }
            }
        }

        public DiffContext DiffContext
        {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        public string SearchChangeFilter
        {
            get => _searchChangeFilter;
            set
            {
                if (SetProperty(ref _searchChangeFilter, value))
                    RefreshVisibleChanges();
            }
        }

        public string ViewRevisionFilePath
        {
            get => _viewRevisionFilePath;
            private set => SetProperty(ref _viewRevisionFilePath, value);
        }

        public object ViewRevisionFileContent
        {
            get => _viewRevisionFileContent;
            private set => SetProperty(ref _viewRevisionFileContent, value);
        }

        public string RevisionFileSearchFilter
        {
            get => _revisionFileSearchFilter;
            set
            {
                if (SetProperty(ref _revisionFileSearchFilter, value))
                    RefreshRevisionSearchSuggestion();
            }
        }

        public List<string> RevisionFileSearchSuggestion
        {
            get => _revisionFileSearchSuggestion;
            private set => SetProperty(ref _revisionFileSearchSuggestion, value);
        }

        public bool CanOpenRevisionFileWithDefaultEditor
        {
            get => _canOpenRevisionFileWithDefaultEditor;
            private set => SetProperty(ref _canOpenRevisionFileWithDefaultEditor, value);
        }

        public Vector ScrollOffset
        {
            get => _scrollOffset;
            set => SetProperty(ref _scrollOffset, value);
        }

        public CommitDetail(Repository repo, CommitDetailSharedData sharedData)
        {
            _repo = repo;
            _sharedData = sharedData ?? new CommitDetailSharedData();
            WebLinks = Models.CommitLink.Get(repo.Remotes);
        }

        public void Dispose()
        {
            _repo = null;
            _commit = null;
            _changes = null;
            _visibleChanges = null;
            _selectedChanges = null;
            _signInfo = null;
            _searchChangeFilter = null;
            _diffContext = null;
            _viewRevisionFileContent = null;
            _cancellationSource = null;
            _requestingRevisionFiles = false;
            _revisionFiles = null;
            _revisionFileSearchSuggestion = null;
        }

        public void NavigateTo(string commitSHA)
        {
            _repo?.NavigateToCommit(commitSHA);
        }

        public async Task<List<Models.Decorator>> GetRefsContainsThisCommitAsync()
        {
            return await new Commands.QueryRefsContainsCommit(_repo.FullPath, _commit.SHA)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        public void ClearSearchChangeFilter()
        {
            SearchChangeFilter = string.Empty;
        }

        public void ClearRevisionFileSearchFilter()
        {
            RevisionFileSearchFilter = string.Empty;
        }

        public void CancelRevisionFileSuggestions()
        {
            RevisionFileSearchSuggestion = null;
        }

        public async Task<Models.Commit> GetCommitAsync(string sha)
        {
            return await new Commands.QuerySingleCommit(_repo.FullPath, sha)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        public string GetAbsPath(string path)
        {
            return Native.OS.GetAbsPath(_repo.FullPath, path);
        }

        public void OpenChangeInMergeTool(Models.Change c)
        {
            new Commands.DiffTool(_repo.FullPath, new Models.DiffOption(_commit, c)).Open();
        }

        public async Task SaveChangesAsPatchAsync(List<Models.Change> changes, string saveTo)
        {
            if (_commit == null)
                return;

            var baseRevision = _commit.Parents.Count == 0 ? Models.Commit.EmptyTreeSHA1 : _commit.Parents[0];
            var succ = await Commands.SaveChangesAsPatch.ProcessRevisionCompareChangesAsync(_repo.FullPath, changes, baseRevision, _commit.SHA, saveTo);
            if (succ)
                App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
        }

        public async Task ResetToThisRevisionAsync(string path)
        {
            var c = _changes?.Find(x => x.Path.Equals(path, StringComparison.Ordinal));
            if (c != null)
            {
                await ResetToThisRevisionAsync(c);
                return;
            }

            var log = _repo.CreateLog($"Reset File to '{_commit.SHA}'");
            await new Commands.Checkout(_repo.FullPath).Use(log).FileWithRevisionAsync(path, _commit.SHA);
            log.Complete();
        }

        public async Task ResetToThisRevisionAsync(Models.Change change)
        {
            var log = _repo.CreateLog($"Reset File to '{_commit.SHA}'");

            if (change.Index == Models.ChangeState.Deleted)
            {
                var fullpath = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
                if (File.Exists(fullpath))
                    await new Commands.Remove(_repo.FullPath, [change.Path])
                        .Use(log)
                        .ExecAsync();
            }
            else if (change.Index == Models.ChangeState.Renamed)
            {
                var old = Native.OS.GetAbsPath(_repo.FullPath, change.OriginalPath);
                if (File.Exists(old))
                    await new Commands.Remove(_repo.FullPath, [change.OriginalPath])
                        .Use(log)
                        .ExecAsync();

                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.Path, _commit.SHA);
            }
            else
            {
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.Path, _commit.SHA);
            }

            log.Complete();
        }

        public async Task ResetToParentRevisionAsync(Models.Change change)
        {
            var log = _repo.CreateLog($"Reset File to '{_commit.SHA}~1'");

            if (change.Index == Models.ChangeState.Added)
            {
                var fullpath = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
                if (File.Exists(fullpath))
                    await new Commands.Remove(_repo.FullPath, [change.Path])
                        .Use(log)
                        .ExecAsync();
            }
            else if (change.Index == Models.ChangeState.Renamed)
            {
                var renamed = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
                if (File.Exists(renamed))
                    await new Commands.Remove(_repo.FullPath, [change.Path])
                        .Use(log)
                        .ExecAsync();

                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.OriginalPath, $"{_commit.SHA}~1");
            }
            else
            {
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.Path, $"{_commit.SHA}~1");
            }

            log.Complete();
        }

        public async Task ResetMultipleToThisRevisionAsync(List<Models.Change> changes)
        {
            var checkouts = new List<string>();
            var removes = new List<string>();

            foreach (var c in changes)
            {
                if (c.Index == Models.ChangeState.Deleted)
                {
                    var fullpath = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
                    if (File.Exists(fullpath))
                        removes.Add(c.Path);
                }
                else if (c.Index == Models.ChangeState.Renamed)
                {
                    var old = Native.OS.GetAbsPath(_repo.FullPath, c.OriginalPath);
                    if (File.Exists(old))
                        removes.Add(c.OriginalPath);

                    checkouts.Add(c.Path);
                }
                else
                {
                    checkouts.Add(c.Path);
                }
            }

            var log = _repo.CreateLog($"Reset Files to '{_commit.SHA}'");

            if (removes.Count > 0)
                await new Commands.Remove(_repo.FullPath, removes)
                    .Use(log)
                    .ExecAsync();

            if (checkouts.Count > 0)
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .MultipleFilesWithRevisionAsync(checkouts, _commit.SHA);

            log.Complete();
        }

        public async Task ResetMultipleToParentRevisionAsync(List<Models.Change> changes)
        {
            var checkouts = new List<string>();
            var removes = new List<string>();

            foreach (var c in changes)
            {
                if (c.Index == Models.ChangeState.Added)
                {
                    var fullpath = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
                    if (File.Exists(fullpath))
                        removes.Add(c.Path);
                }
                else if (c.Index == Models.ChangeState.Renamed)
                {
                    var renamed = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
                    if (File.Exists(renamed))
                        removes.Add(c.Path);

                    checkouts.Add(c.OriginalPath);
                }
                else
                {
                    checkouts.Add(c.Path);
                }
            }

            var log = _repo.CreateLog($"Reset Files to '{_commit.SHA}~1'");

            if (removes.Count > 0)
                await new Commands.Remove(_repo.FullPath, removes)
                    .Use(log)
                    .ExecAsync();

            if (checkouts.Count > 0)
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .MultipleFilesWithRevisionAsync(checkouts, $"{_commit.SHA}~1");

            log.Complete();
        }

        public async Task<List<Models.Object>> GetRevisionFilesUnderFolderAsync(string parentFolder)
        {
            return await new Commands.QueryRevisionObjects(_repo.FullPath, _commit.SHA, parentFolder)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        public async Task ViewRevisionFileAsync(Models.Object file)
        {
            var obj = file ?? new Models.Object() { Path = string.Empty, Type = Models.ObjectType.None };
            ViewRevisionFilePath = obj.Path;

            switch (obj.Type)
            {
                case Models.ObjectType.Blob:
                    CanOpenRevisionFileWithDefaultEditor = true;
                    await SetViewingBlobAsync(obj);
                    break;
                case Models.ObjectType.Commit:
                    CanOpenRevisionFileWithDefaultEditor = false;
                    await SetViewingCommitAsync(obj);
                    break;
                default:
                    CanOpenRevisionFileWithDefaultEditor = false;
                    ViewRevisionFileContent = null;
                    break;
            }
        }

        public async Task OpenRevisionFileAsync(string file, Models.ExternalTool tool)
        {
            var fullPath = Native.OS.GetAbsPath(_repo.FullPath, file);
            var fileName = Path.GetFileNameWithoutExtension(fullPath) ?? "";
            var fileExt = Path.GetExtension(fullPath) ?? "";
            var tmpFile = Path.Combine(Path.GetTempPath(), $"{fileName}~{_commit.SHA.AsSpan(0, 10)}{fileExt}");

            await Commands.SaveRevisionFile
                .RunAsync(_repo.FullPath, _commit.SHA, file, tmpFile)
                .ConfigureAwait(false);

            if (tool == null)
                Native.OS.OpenWithDefaultEditor(tmpFile);
            else
                tool.Launch(tmpFile.Quoted());
        }

        public async Task SaveRevisionFileAsync(Models.Object file, string saveTo)
        {
            await Commands.SaveRevisionFile
                .RunAsync(_repo.FullPath, _commit.SHA, file.Path, saveTo)
                .ConfigureAwait(false);
        }

        private void Refresh()
        {
            _changes = [];
            _requestingRevisionFiles = false;
            _revisionFiles = null;

            SignInfo = null;
            ViewRevisionFileContent = null;
            ViewRevisionFilePath = string.Empty;
            CanOpenRevisionFileWithDefaultEditor = false;
            Children = null;
            RevisionFileSearchFilter = string.Empty;
            RevisionFileSearchSuggestion = null;
            ScrollOffset = Vector.Zero;

            if (_commit == null)
                return;

            if (_cancellationSource is { IsCancellationRequested: false })
                _cancellationSource.Cancel();

            _cancellationSource = new CancellationTokenSource();
            var token = _cancellationSource.Token;

            Task.Run(async () =>
            {
                var message = await new Commands.QueryCommitFullMessage(_repo.FullPath, _commit.SHA)
                    .GetResultAsync()
                    .ConfigureAwait(false);
                var inlines = await ParseInlinesInMessageAsync(message).ConfigureAwait(false);

                if (!token.IsCancellationRequested)
                    Dispatcher.UIThread.Post(() =>
                    {
                        FullMessage = new Models.CommitFullMessage
                        {
                            Message = message,
                            Inlines = inlines
                        };
                    });
            }, token);

            Task.Run(async () =>
            {
                var signInfo = await new Commands.QueryCommitSignInfo(_repo.FullPath, _commit.SHA, !_repo.HasAllowedSignersFile)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                if (!token.IsCancellationRequested)
                    Dispatcher.UIThread.Post(() => SignInfo = signInfo);
            }, token);

            if (Preferences.Instance.ShowChildren)
            {
                Task.Run(async () =>
                {
                    var max = Preferences.Instance.MaxHistoryCommits;
                    var cmd = new Commands.QueryCommitChildren(_repo.FullPath, _commit.SHA, max) { CancellationToken = token };
                    var children = await cmd.GetResultAsync().ConfigureAwait(false);
                    if (!token.IsCancellationRequested)
                        Dispatcher.UIThread.Post(() => Children = children);
                }, token);
            }

            Task.Run(async () =>
            {
                var parent = _commit.Parents.Count == 0 ? Models.Commit.EmptyTreeSHA1 : $"{_commit.SHA}^";
                var cmd = new Commands.CompareRevisions(_repo.FullPath, parent, _commit.SHA) { CancellationToken = token };
                var changes = await cmd.ReadAsync().ConfigureAwait(false);
                var visible = changes;
                if (!string.IsNullOrWhiteSpace(_searchChangeFilter))
                {
                    visible = new List<Models.Change>();
                    foreach (var c in changes)
                    {
                        if (c.Path.Contains(_searchChangeFilter, StringComparison.OrdinalIgnoreCase))
                            visible.Add(c);
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Changes = changes;
                        VisibleChanges = visible;

                        if (visible.Count == 0)
                            SelectedChanges = null;
                        else
                            SelectedChanges = [VisibleChanges[0]];
                    });
                }
            }, token);
        }

        private async Task<Models.InlineElementCollector> ParseInlinesInMessageAsync(string message)
        {
            var inlines = new Models.InlineElementCollector();
            if (_repo.IssueTrackers is { Count: > 0 } rules)
            {
                foreach (var rule in rules)
                    rule.Matches(inlines, message);
            }

            var urlMatches = REG_URL_FORMAT().Matches(message);
            foreach (Match match in urlMatches)
            {
                var start = match.Index;
                var len = match.Length;
                if (inlines.Intersect(start, len) != null)
                    continue;

                var url = message.Substring(start, len);
                if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    inlines.Add(new Models.InlineElement(Models.InlineElementType.Link, start, len, url));
            }

            var shaMatches = REG_SHA_FORMAT().Matches(message);
            foreach (Match match in shaMatches)
            {
                var start = match.Index;
                var len = match.Length;
                if (inlines.Intersect(start, len) != null)
                    continue;

                var sha = match.Groups[1].Value;
                var isCommitSHA = await new Commands.IsCommitSHA(_repo.FullPath, sha).GetResultAsync().ConfigureAwait(false);
                if (isCommitSHA)
                    inlines.Add(new Models.InlineElement(Models.InlineElementType.CommitSHA, start, len, sha));
            }

            var inlineCodeMatches = REG_INLINECODE_FORMAT().Matches(message);
            foreach (Match match in inlineCodeMatches)
            {
                var start = match.Index;
                var len = match.Length;
                if (inlines.Intersect(start, len) != null)
                    continue;

                inlines.Add(new Models.InlineElement(Models.InlineElementType.Code, start + 1, len - 2, string.Empty));
            }

            inlines.Sort();
            return inlines;
        }

        private void RefreshVisibleChanges()
        {
            if (string.IsNullOrEmpty(_searchChangeFilter))
            {
                VisibleChanges = _changes;
            }
            else
            {
                var visible = new List<Models.Change>();
                foreach (var c in _changes)
                {
                    if (c.Path.Contains(_searchChangeFilter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(c);
                }

                VisibleChanges = visible;
            }
        }

        private void RefreshRevisionSearchSuggestion()
        {
            if (!string.IsNullOrEmpty(_revisionFileSearchFilter))
            {
                if (_revisionFiles == null)
                {
                    if (_requestingRevisionFiles)
                        return;

                    var sha = Commit.SHA;
                    _requestingRevisionFiles = true;

                    Task.Run(async () =>
                    {
                        var files = await new Commands.QueryRevisionFileNames(_repo.FullPath, sha)
                            .GetResultAsync()
                            .ConfigureAwait(false);

                        Dispatcher.UIThread.Post(() =>
                        {
                            if (sha == Commit.SHA && _requestingRevisionFiles)
                            {
                                _revisionFiles = files;
                                _requestingRevisionFiles = false;

                                if (!string.IsNullOrEmpty(_revisionFileSearchFilter))
                                    CalcRevisionFileSearchSuggestion();
                            }
                        });
                    });
                }
                else
                {
                    CalcRevisionFileSearchSuggestion();
                }
            }
            else
            {
                RevisionFileSearchSuggestion = null;
                GC.Collect();
            }
        }

        private void CalcRevisionFileSearchSuggestion()
        {
            var suggestion = new List<string>();
            foreach (var file in _revisionFiles)
            {
                if (file.Contains(_revisionFileSearchFilter, StringComparison.OrdinalIgnoreCase) &&
                    file.Length != _revisionFileSearchFilter.Length)
                    suggestion.Add(file);

                if (suggestion.Count >= 100)
                    break;
            }

            RevisionFileSearchSuggestion = suggestion;
        }

        private async Task SetViewingBlobAsync(Models.Object file)
        {
            var isBinary = await new Commands.IsBinary(_repo.FullPath, _commit.SHA, file.Path).GetResultAsync();
            if (isBinary)
            {
                var imgDecoder = ImageSource.GetDecoder(file.Path);
                if (imgDecoder != Models.ImageDecoder.None)
                {
                    var source = await ImageSource.FromRevisionAsync(_repo.FullPath, _commit.SHA, file.Path, imgDecoder);
                    ViewRevisionFileContent = new Models.RevisionImageFile(file.Path, source.Bitmap, source.Size);
                }
                else
                {
                    var size = await new Commands.QueryFileSize(_repo.FullPath, file.Path, _commit.SHA).GetResultAsync();
                    ViewRevisionFileContent = new Models.RevisionBinaryFile() { Size = size };
                }

                return;
            }

            var contentStream = await Commands.QueryFileContent.RunAsync(_repo.FullPath, _commit.SHA, file.Path);
            var content = await new StreamReader(contentStream).ReadToEndAsync();
            var lfs = Models.LFSObject.Parse(content);
            if (lfs != null)
            {
                var imgDecoder = ImageSource.GetDecoder(file.Path);
                if (imgDecoder != Models.ImageDecoder.None)
                    ViewRevisionFileContent = new RevisionLFSImage(_repo.FullPath, file.Path, lfs, imgDecoder);
                else
                    ViewRevisionFileContent = new Models.RevisionLFSObject() { Object = lfs };
            }
            else
            {
                ViewRevisionFileContent = new Models.RevisionTextFile() { FileName = file.Path, Content = content };
            }
        }

        private async Task SetViewingCommitAsync(Models.Object file)
        {
            var submoduleRoot = Path.Combine(_repo.FullPath, file.Path).Replace('\\', '/').Trim('/');
            var commit = await new Commands.QuerySingleCommit(submoduleRoot, file.SHA).GetResultAsync();
            if (commit == null)
            {
                ViewRevisionFileContent = new Models.RevisionSubmodule()
                {
                    Commit = new Models.Commit() { SHA = file.SHA },
                    FullMessage = new Models.CommitFullMessage()
                };
            }
            else
            {
                var message = await new Commands.QueryCommitFullMessage(submoduleRoot, file.SHA).GetResultAsync();
                ViewRevisionFileContent = new Models.RevisionSubmodule()
                {
                    Commit = commit,
                    FullMessage = new Models.CommitFullMessage { Message = message }
                };
            }
        }

        [GeneratedRegex(@"\b(https?://|ftp://)[\w\d\._/\-~%@()+:?&=#!]*[\w\d/]")]
        private static partial Regex REG_URL_FORMAT();

        [GeneratedRegex(@"\b([0-9a-fA-F]{6,40})\b")]
        private static partial Regex REG_SHA_FORMAT();

        [GeneratedRegex(@"`.*?`")]
        private static partial Regex REG_INLINECODE_FORMAT();

        private Repository _repo = null;
        private CommitDetailSharedData _sharedData = null;
        private Models.Commit _commit = null;
        private Models.CommitFullMessage _fullMessage = null;
        private Models.CommitSignInfo _signInfo = null;
        private List<string> _children = null;
        private List<Models.Change> _changes = [];
        private List<Models.Change> _visibleChanges = [];
        private List<Models.Change> _selectedChanges = null;
        private string _searchChangeFilter = string.Empty;
        private DiffContext _diffContext = null;
        private string _viewRevisionFilePath = string.Empty;
        private object _viewRevisionFileContent = null;
        private CancellationTokenSource _cancellationSource = null;
        private bool _requestingRevisionFiles = false;
        private List<string> _revisionFiles = null;
        private string _revisionFileSearchFilter = string.Empty;
        private List<string> _revisionFileSearchSuggestion = null;
        private bool _canOpenRevisionFileWithDefaultEditor = false;
        private Vector _scrollOffset = Vector.Zero;
    }
}
