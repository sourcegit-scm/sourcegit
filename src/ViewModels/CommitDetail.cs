using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public partial class CommitDetail : ObservableObject, IDisposable
    {
        public int ActivePageIndex
        {
            get => _rememberActivePageIndex ? _repo.CommitDetailActivePageIndex : _activePageIndex;
            set
            {
                if (_rememberActivePageIndex)
                    _repo.CommitDetailActivePageIndex = value;
                else
                    _activePageIndex = value;

                OnPropertyChanged();
            }
        }

        public Models.Commit Commit
        {
            get => _commit;
            set
            {
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
                    if (value is not { Count: 1 })
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

        public CommitDetail(Repository repo, bool rememberActivePageIndex)
        {
            _repo = repo;
            _rememberActivePageIndex = rememberActivePageIndex;
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

        public async Task OpenRevisionFileWithDefaultEditorAsync(string file)
        {
            var fullPath = Native.OS.GetAbsPath(_repo.FullPath, file);
            var fileName = Path.GetFileNameWithoutExtension(fullPath) ?? "";
            var fileExt = Path.GetExtension(fullPath) ?? "";
            var tmpFile = Path.Combine(Path.GetTempPath(), $"{fileName}~{_commit.SHA.AsSpan(0, 10)}{fileExt}");

            await Commands.SaveRevisionFile
                .RunAsync(_repo.FullPath, _commit.SHA, file, tmpFile)
                .ConfigureAwait(false);

            Native.OS.OpenWithDefaultEditor(tmpFile);
        }

        public string GetAbsPath(string path)
        {
            return Native.OS.GetAbsPath(_repo.FullPath, path);
        }

        public void OpenChangeInMergeTool(Models.Change c)
        {
            var toolType = Preferences.Instance.ExternalMergeToolType;
            var toolPath = Preferences.Instance.ExternalMergeToolPath;
            var opt = new Models.DiffOption(_commit, c);
            new Commands.DiffTool(_repo.FullPath, toolType, toolPath, opt).Open();
        }

        public async Task SaveRevisionFile(Models.Object file)
        {
            var storageProvider = App.GetStorageProvider();
            if (storageProvider == null)
                return;

            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            try
            {
                var selected = await storageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                {
                    var folder = selected[0];
                    var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder.Path.ToString();
                    var saveTo = Path.Combine(folderPath, Path.GetFileName(file.Path)!);

                    await Commands.SaveRevisionFile
                        .RunAsync(_repo.FullPath, _commit.SHA, file.Path, saveTo)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                App.RaiseException(_repo.FullPath, $"Failed to save file: {e.Message}");
            }
        }

        public ContextMenu CreateChangeContextMenuByFolder(ChangeTreeNode node, List<Models.Change> changes)
        {
            var fullPath = Native.OS.GetAbsPath(_repo.FullPath, node.FullPath);
            var explore = new MenuItem();
            explore.Header = App.Text("RevealFile");
            explore.Icon = App.CreateMenuIcon("Icons.Explore");
            explore.IsEnabled = Directory.Exists(fullPath);
            explore.Click += (_, ev) =>
            {
                Native.OS.OpenInFileManager(fullPath, true);
                ev.Handled = true;
            };

            var history = new MenuItem();
            history.Header = App.Text("DirHistories");
            history.Icon = App.CreateMenuIcon("Icons.Histories");
            history.Click += (_, ev) =>
            {
                App.ShowWindow(new DirHistories(_repo, node.FullPath, _commit.SHA));
                ev.Handled = true;
            };

            var patch = new MenuItem();
            patch.Header = App.Text("FileCM.SaveAsPatch");
            patch.Icon = App.CreateMenuIcon("Icons.Diff");
            patch.Click += async (_, e) =>
            {
                var storageProvider = App.GetStorageProvider();
                if (storageProvider == null)
                    return;

                var options = new FilePickerSaveOptions();
                options.Title = App.Text("FileCM.SaveAsPatch");
                options.DefaultExtension = ".patch";
                options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                var baseRevision = _commit.Parents.Count == 0 ? Models.Commit.EmptyTreeSHA1 : _commit.Parents[0];
                var storageFile = await storageProvider.SaveFilePickerAsync(options);
                if (storageFile != null)
                {
                    var saveTo = storageFile.Path.LocalPath;
                    var succ = await Commands.SaveChangesAsPatch.ProcessRevisionCompareChangesAsync(_repo.FullPath, changes, baseRevision, _commit.SHA, saveTo);
                    if (succ)
                        App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                }

                e.Handled = true;
            };

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyPath.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(node.FullPath);
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
            copyFullPath.Click += async (_, e) =>
            {
                await App.CopyTextAsync(fullPath);
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(explore);
            menu.Items.Add(new MenuItem { Header = "-" });
            menu.Items.Add(history);
            menu.Items.Add(patch);
            menu.Items.Add(new MenuItem { Header = "-" });
            menu.Items.Add(copyPath);
            menu.Items.Add(copyFullPath);

            return menu;
        }

        public ContextMenu CreateChangeContextMenu(Models.Change change)
        {
            var openWithMerger = new MenuItem();
            openWithMerger.Header = App.Text("OpenInExternalMergeTool");
            openWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
            openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
            openWithMerger.Click += (_, ev) =>
            {
                OpenChangeInMergeTool(change);
                ev.Handled = true;
            };

            var fullPath = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
            var explore = new MenuItem();
            explore.Header = App.Text("RevealFile");
            explore.Icon = App.CreateMenuIcon("Icons.Explore");
            explore.IsEnabled = File.Exists(fullPath);
            explore.Click += (_, ev) =>
            {
                Native.OS.OpenInFileManager(fullPath, true);
                ev.Handled = true;
            };

            var history = new MenuItem();
            history.Header = App.Text("FileHistory");
            history.Icon = App.CreateMenuIcon("Icons.Histories");
            history.Click += (_, ev) =>
            {
                App.ShowWindow(new FileHistories(_repo, change.Path, _commit.SHA));
                ev.Handled = true;
            };

            var blame = new MenuItem();
            blame.Header = App.Text("Blame");
            blame.Icon = App.CreateMenuIcon("Icons.Blame");
            blame.IsEnabled = change.Index != Models.ChangeState.Deleted;
            blame.Click += (_, ev) =>
            {
                App.ShowWindow(new Blame(_repo.FullPath, change.Path, _commit));
                ev.Handled = true;
            };

            var patch = new MenuItem();
            patch.Header = App.Text("FileCM.SaveAsPatch");
            patch.Icon = App.CreateMenuIcon("Icons.Diff");
            patch.Click += async (_, e) =>
            {
                var storageProvider = App.GetStorageProvider();
                if (storageProvider == null)
                    return;

                var options = new FilePickerSaveOptions();
                options.Title = App.Text("FileCM.SaveAsPatch");
                options.DefaultExtension = ".patch";
                options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                var baseRevision = _commit.Parents.Count == 0 ? Models.Commit.EmptyTreeSHA1 : _commit.Parents[0];
                var storageFile = await storageProvider.SaveFilePickerAsync(options);
                if (storageFile != null)
                {
                    var saveTo = storageFile.Path.LocalPath;
                    var succ = await Commands.SaveChangesAsPatch.ProcessRevisionCompareChangesAsync(_repo.FullPath, [change], baseRevision, _commit.SHA, saveTo);
                    if (succ)
                        App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                }

                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(openWithMerger);
            menu.Items.Add(explore);
            menu.Items.Add(new MenuItem { Header = "-" });
            menu.Items.Add(history);
            menu.Items.Add(blame);
            menu.Items.Add(patch);
            menu.Items.Add(new MenuItem { Header = "-" });

            if (!_repo.IsBare)
            {
                var resetToThisRevision = new MenuItem();
                resetToThisRevision.Header = App.Text("ChangeCM.CheckoutThisRevision");
                resetToThisRevision.Icon = App.CreateMenuIcon("Icons.File.Checkout");
                resetToThisRevision.Click += async (_, ev) =>
                {
                    await ResetToThisRevisionAsync(change.Path);
                    ev.Handled = true;
                };

                var resetToFirstParent = new MenuItem();
                resetToFirstParent.Header = App.Text("ChangeCM.CheckoutFirstParentRevision");
                resetToFirstParent.Icon = App.CreateMenuIcon("Icons.File.Checkout");
                resetToFirstParent.IsEnabled = _commit.Parents.Count > 0;
                resetToFirstParent.Click += async (_, ev) =>
                {
                    await ResetToParentRevisionAsync(change);
                    ev.Handled = true;
                };

                menu.Items.Add(resetToThisRevision);
                menu.Items.Add(resetToFirstParent);
                menu.Items.Add(new MenuItem { Header = "-" });

                TryToAddContextMenuItemsForGitLFS(menu, fullPath, change.Path);
            }

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyPath.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(change.Path);
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
            copyFullPath.Click += async (_, e) =>
            {
                await App.CopyTextAsync(fullPath);
                e.Handled = true;
            };

            menu.Items.Add(copyPath);
            menu.Items.Add(copyFullPath);
            return menu;
        }

        public ContextMenu CreateRevisionFileContextMenuByFolder(string path)
        {
            var fullPath = Native.OS.GetAbsPath(_repo.FullPath, path);
            var explore = new MenuItem();
            explore.Header = App.Text("RevealFile");
            explore.Icon = App.CreateMenuIcon("Icons.Explore");
            explore.IsEnabled = Directory.Exists(fullPath);
            explore.Click += (_, ev) =>
            {
                Native.OS.OpenInFileManager(fullPath, true);
                ev.Handled = true;
            };

            var history = new MenuItem();
            history.Header = App.Text("DirHistories");
            history.Icon = App.CreateMenuIcon("Icons.Histories");
            history.Click += (_, ev) =>
            {
                App.ShowWindow(new DirHistories(_repo, path, _commit.SHA));
                ev.Handled = true;
            };

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyPath.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(path);
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
            copyFullPath.Click += async (_, e) =>
            {
                await App.CopyTextAsync(fullPath);
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(explore);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(history);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(copyPath);
            menu.Items.Add(copyFullPath);
            return menu;
        }

        public ContextMenu CreateRevisionFileContextMenu(Models.Object file)
        {
            if (file.Type == Models.ObjectType.Tree)
                return CreateRevisionFileContextMenuByFolder(file.Path);

            var menu = new ContextMenu();
            var fullPath = Native.OS.GetAbsPath(_repo.FullPath, file.Path);
            var openWith = new MenuItem();
            openWith.Header = App.Text("OpenWith");
            openWith.Icon = App.CreateMenuIcon("Icons.OpenWith");
            openWith.Tag = OperatingSystem.IsMacOS() ? "⌘+O" : "Ctrl+O";
            openWith.IsEnabled = file.Type == Models.ObjectType.Blob;
            openWith.Click += async (_, ev) =>
            {
                await OpenRevisionFileWithDefaultEditorAsync(file.Path);
                ev.Handled = true;
            };

            var saveAs = new MenuItem();
            saveAs.Header = App.Text("SaveAs");
            saveAs.Icon = App.CreateMenuIcon("Icons.Save");
            saveAs.IsEnabled = file.Type == Models.ObjectType.Blob;
            saveAs.Tag = OperatingSystem.IsMacOS() ? "⌘+S" : "Ctrl+S";
            saveAs.Click += async (_, ev) =>
            {
                await SaveRevisionFile(file);
                ev.Handled = true;
            };

            var explore = new MenuItem();
            explore.Header = App.Text("RevealFile");
            explore.Icon = App.CreateMenuIcon("Icons.Explore");
            explore.IsEnabled = File.Exists(fullPath);
            explore.Click += (_, ev) =>
            {
                Native.OS.OpenInFileManager(fullPath, file.Type == Models.ObjectType.Blob);
                ev.Handled = true;
            };

            menu.Items.Add(openWith);
            menu.Items.Add(saveAs);
            menu.Items.Add(explore);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var history = new MenuItem();
            history.Header = App.Text("FileHistory");
            history.Icon = App.CreateMenuIcon("Icons.Histories");
            history.Click += (_, ev) =>
            {
                App.ShowWindow(new FileHistories(_repo, file.Path, _commit.SHA));
                ev.Handled = true;
            };

            var blame = new MenuItem();
            blame.Header = App.Text("Blame");
            blame.Icon = App.CreateMenuIcon("Icons.Blame");
            blame.IsEnabled = file.Type == Models.ObjectType.Blob;
            blame.Click += (_, ev) =>
            {
                App.ShowWindow(new Blame(_repo.FullPath, file.Path, _commit));
                ev.Handled = true;
            };

            menu.Items.Add(history);
            menu.Items.Add(blame);
            menu.Items.Add(new MenuItem() { Header = "-" });

            if (!_repo.IsBare)
            {
                var resetToThisRevision = new MenuItem();
                resetToThisRevision.Header = App.Text("ChangeCM.CheckoutThisRevision");
                resetToThisRevision.Icon = App.CreateMenuIcon("Icons.File.Checkout");
                resetToThisRevision.Click += async (_, ev) =>
                {
                    await ResetToThisRevisionAsync(file.Path);
                    ev.Handled = true;
                };

                var change = _changes.Find(x => x.Path == file.Path) ?? new Models.Change() { Index = Models.ChangeState.None, Path = file.Path };
                var resetToFirstParent = new MenuItem();
                resetToFirstParent.Header = App.Text("ChangeCM.CheckoutFirstParentRevision");
                resetToFirstParent.Icon = App.CreateMenuIcon("Icons.File.Checkout");
                resetToFirstParent.IsEnabled = _commit.Parents.Count > 0;
                resetToFirstParent.Click += async (_, ev) =>
                {
                    await ResetToParentRevisionAsync(change);
                    ev.Handled = true;
                };

                menu.Items.Add(resetToThisRevision);
                menu.Items.Add(resetToFirstParent);
                menu.Items.Add(new MenuItem() { Header = "-" });

                TryToAddContextMenuItemsForGitLFS(menu, fullPath, file.Path);
            }

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyPath.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(file.Path);
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
            copyFullPath.Click += async (_, e) =>
            {
                await App.CopyTextAsync(fullPath);
                e.Handled = true;
            };

            menu.Items.Add(copyPath);
            menu.Items.Add(copyFullPath);
            return menu;
        }

        private void Refresh()
        {
            _changes = null;
            _requestingRevisionFiles = false;
            _revisionFiles = null;

            SignInfo = null;
            ViewRevisionFileContent = null;
            ViewRevisionFilePath = string.Empty;
            CanOpenRevisionFileWithDefaultEditor = false;
            Children = null;
            RevisionFileSearchFilter = string.Empty;
            RevisionFileSearchSuggestion = null;

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
                var inlines = await ParseInlinesInMessageAsync(message);

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
                var parent = _commit.Parents.Count == 0 ? Models.Commit.EmptyTreeSHA1 : _commit.Parents[0];
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
            if (_repo.Settings.IssueTrackerRules is { Count: > 0 } rules)
            {
                foreach (var rule in rules)
                    rule.Matches(inlines, message);
            }

            var urlMatches = REG_URL_FORMAT().Matches(message);
            for (int i = 0; i < urlMatches.Count; i++)
            {
                var match = urlMatches[i];
                if (!match.Success)
                    continue;

                var start = match.Index;
                var len = match.Length;
                if (inlines.Intersect(start, len) != null)
                    continue;

                var url = message.Substring(start, len);
                if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    inlines.Add(new Models.InlineElement(Models.InlineElementType.Link, start, len, url));
            }

            var shaMatches = REG_SHA_FORMAT().Matches(message);
            for (int i = 0; i < shaMatches.Count; i++)
            {
                var match = shaMatches[i];
                if (!match.Success)
                    continue;

                var start = match.Index;
                var len = match.Length;
                if (inlines.Intersect(start, len) != null)
                    continue;

                var sha = match.Groups[1].Value;
                var isCommitSHA = await new Commands.IsCommitSHA(_repo.FullPath, sha).GetResultAsync().ConfigureAwait(false);
                if (isCommitSHA)
                    inlines.Add(new Models.InlineElement(Models.InlineElementType.CommitSHA, start, len, sha));
            }

            inlines.Sort();
            return inlines;
        }

        private void RefreshVisibleChanges()
        {
            if (_changes == null)
                return;

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

        private void TryToAddContextMenuItemsForGitLFS(ContextMenu menu, string fullPath, string path)
        {
            if (_repo.Remotes.Count == 0 || !File.Exists(fullPath))
                return;

            var lfsEnabled = new Commands.LFS(_repo.FullPath).IsEnabled();
            if (!lfsEnabled)
                return;

            var lfs = new MenuItem();
            lfs.Header = App.Text("GitLFS");
            lfs.Icon = App.CreateMenuIcon("Icons.LFS");

            var lfsLock = new MenuItem();
            lfsLock.Header = App.Text("GitLFS.Locks.Lock");
            lfsLock.Icon = App.CreateMenuIcon("Icons.Lock");
            if (_repo.Remotes.Count == 1)
            {
                lfsLock.Click += async (_, e) =>
                {
                    var log = _repo.CreateLog("Lock LFS file");
                    var succ = await new Commands.LFS(_repo.FullPath).LockAsync(_repo.Remotes[0].Name, path, log);
                    if (succ)
                        App.SendNotification(_repo.FullPath, $"Lock file \"{path}\" successfully!");

                    log.Complete();
                    e.Handled = true;
                };
            }
            else
            {
                foreach (var remote in _repo.Remotes)
                {
                    var remoteName = remote.Name;
                    var lockRemote = new MenuItem();
                    lockRemote.Header = remoteName;
                    lockRemote.Click += async (_, e) =>
                    {
                        var log = _repo.CreateLog("Lock LFS file");
                        var succ = await new Commands.LFS(_repo.FullPath).LockAsync(remoteName, path, log);
                        if (succ)
                            App.SendNotification(_repo.FullPath, $"Lock file \"{path}\" successfully!");

                        log.Complete();
                        e.Handled = true;
                    };
                    lfsLock.Items.Add(lockRemote);
                }
            }
            lfs.Items.Add(lfsLock);

            var lfsUnlock = new MenuItem();
            lfsUnlock.Header = App.Text("GitLFS.Locks.Unlock");
            lfsUnlock.Icon = App.CreateMenuIcon("Icons.Unlock");
            if (_repo.Remotes.Count == 1)
            {
                lfsUnlock.Click += async (_, e) =>
                {
                    var log = _repo.CreateLog("Unlock LFS file");
                    var succ = await new Commands.LFS(_repo.FullPath).UnlockAsync(_repo.Remotes[0].Name, path, false, log);
                    if (succ)
                        App.SendNotification(_repo.FullPath, $"Unlock file \"{path}\" successfully!");

                    log.Complete();
                    e.Handled = true;
                };
            }
            else
            {
                foreach (var remote in _repo.Remotes)
                {
                    var remoteName = remote.Name;
                    var unlockRemote = new MenuItem();
                    unlockRemote.Header = remoteName;
                    unlockRemote.Click += async (_, e) =>
                    {
                        var log = _repo.CreateLog("Unlock LFS file");
                        var succ = await new Commands.LFS(_repo.FullPath).UnlockAsync(remoteName, path, false, log);
                        if (succ)
                            App.SendNotification(_repo.FullPath, $"Unlock file \"{path}\" successfully!");

                        log.Complete();
                        e.Handled = true;
                    };
                    lfsUnlock.Items.Add(unlockRemote);
                }
            }
            lfs.Items.Add(lfsUnlock);

            menu.Items.Add(lfs);
            menu.Items.Add(new MenuItem() { Header = "-" });
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

        private async Task ResetToThisRevisionAsync(string path)
        {
            var log = _repo.CreateLog($"Reset File to '{_commit.SHA}'");

            await new Commands.Checkout(_repo.FullPath).Use(log).FileWithRevisionAsync(path, $"{_commit.SHA}");
            log.Complete();
        }

        private async Task ResetToParentRevisionAsync(Models.Change change)
        {
            var log = _repo.CreateLog($"Reset File to '{_commit.SHA}~1'");

            if (change.Index == Models.ChangeState.Renamed)
                await new Commands.Checkout(_repo.FullPath).Use(log).FileWithRevisionAsync(change.OriginalPath, $"{_commit.SHA}~1");

            await new Commands.Checkout(_repo.FullPath).Use(log).FileWithRevisionAsync(change.Path, $"{_commit.SHA}~1");
            log.Complete();
        }

        [GeneratedRegex(@"\b(https?://|ftp://)[\w\d\._/\-~%@()+:?&=#!]*[\w\d/]")]
        private static partial Regex REG_URL_FORMAT();

        [GeneratedRegex(@"\b([0-9a-fA-F]{6,40})\b")]
        private static partial Regex REG_SHA_FORMAT();

        private Repository _repo = null;
        private bool _rememberActivePageIndex = true;
        private int _activePageIndex = 0;
        private Models.Commit _commit = null;
        private Models.CommitFullMessage _fullMessage = null;
        private Models.CommitSignInfo _signInfo = null;
        private List<string> _children = null;
        private List<Models.Change> _changes = null;
        private List<Models.Change> _visibleChanges = null;
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
    }
}
