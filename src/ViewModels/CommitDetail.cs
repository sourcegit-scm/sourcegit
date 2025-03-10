using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public partial class CommitDetail : ObservableObject
    {
        public int ActivePageIndex
        {
            get => _repo.CommitDetailActivePageIndex;
            set
            {
                if (_repo.CommitDetailActivePageIndex != value)
                {
                    _repo.CommitDetailActivePageIndex = value;
                    OnPropertyChanged();
                }
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
        } = [];

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
                    if (value == null || value.Count != 1)
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
                {
                    RefreshVisibleChanges();
                }
            }
        }

        public object ViewRevisionFileContent
        {
            get => _viewRevisionFileContent;
            set => SetProperty(ref _viewRevisionFileContent, value);
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

        public CommitDetail(Repository repo)
        {
            _repo = repo;

            foreach (var remote in repo.Remotes)
            {
                if (remote.TryGetVisitURL(out var url))
                {
                    var trimmedUrl = url;
                    if (url.EndsWith(".git"))
                        trimmedUrl = url.Substring(0, url.Length - 4);

                    if (url.StartsWith("https://github.com/", StringComparison.Ordinal))
                        WebLinks.Add(new Models.CommitLink() { Name = $"Github ({trimmedUrl.Substring(19)})", URLPrefix = $"{url}/commit/" });
                    else if (url.StartsWith("https://gitlab.", StringComparison.Ordinal))
                        WebLinks.Add(new Models.CommitLink() { Name = $"GitLab ({trimmedUrl.Substring(trimmedUrl.Substring(15).IndexOf('/') + 16)})", URLPrefix = $"{url}/-/commit/" });
                    else if (url.StartsWith("https://gitee.com/", StringComparison.Ordinal))
                        WebLinks.Add(new Models.CommitLink() { Name = $"Gitee ({trimmedUrl.Substring(18)})", URLPrefix = $"{url}/commit/" });
                    else if (url.StartsWith("https://bitbucket.org/", StringComparison.Ordinal))
                        WebLinks.Add(new Models.CommitLink() { Name = $"BitBucket ({trimmedUrl.Substring(22)})", URLPrefix = $"{url}/commits/" });
                    else if (url.StartsWith("https://codeberg.org/", StringComparison.Ordinal))
                        WebLinks.Add(new Models.CommitLink() { Name = $"Codeberg ({trimmedUrl.Substring(21)})", URLPrefix = $"{url}/commit/" });
                    else if (url.StartsWith("https://gitea.org/", StringComparison.Ordinal))
                        WebLinks.Add(new Models.CommitLink() { Name = $"Gitea ({trimmedUrl.Substring(18)})", URLPrefix = $"{url}/commit/" });
                    else if (url.StartsWith("https://git.sr.ht/", StringComparison.Ordinal))
                        WebLinks.Add(new Models.CommitLink() { Name = $"sourcehut ({trimmedUrl.Substring(18)})", URLPrefix = $"{url}/commit/" });
                }
            }
        }

        public void Cleanup()
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
            _cancelToken = null;
            WebLinks.Clear();
            _revisionFiles = null;
            _revisionFileSearchSuggestion = null;
        }

        public void NavigateTo(string commitSHA)
        {
            _repo?.NavigateToCommit(commitSHA);
        }

        public List<Models.Decorator> GetRefsContainsThisCommit()
        {
            return new Commands.QueryRefsContainsCommit(_repo.FullPath, _commit.SHA).Result();
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

        public Models.Commit GetParent(string sha)
        {
            return new Commands.QuerySingleCommit(_repo.FullPath, sha).Result();
        }

        public List<Models.Object> GetRevisionFilesUnderFolder(string parentFolder)
        {
            return new Commands.QueryRevisionObjects(_repo.FullPath, _commit.SHA, parentFolder).Result();
        }

        public void ViewRevisionFile(Models.Object file)
        {
            if (file == null)
            {
                ViewRevisionFileContent = null;
                return;
            }

            switch (file.Type)
            {
                case Models.ObjectType.Blob:
                    Task.Run(() =>
                    {
                        var isBinary = new Commands.IsBinary(_repo.FullPath, _commit.SHA, file.Path).Result();
                        if (isBinary)
                        {
                            var ext = Path.GetExtension(file.Path);
                            if (IMG_EXTS.Contains(ext))
                            {
                                var stream = Commands.QueryFileContent.Run(_repo.FullPath, _commit.SHA, file.Path);
                                var fileSize = stream.Length;
                                var bitmap = fileSize > 0 ? new Bitmap(stream) : null;
                                var imageType = ext!.Substring(1).ToUpper(CultureInfo.CurrentCulture);
                                var image = new Models.RevisionImageFile() { Image = bitmap, FileSize = fileSize, ImageType = imageType };
                                Dispatcher.UIThread.Invoke(() => ViewRevisionFileContent = image);
                            }
                            else
                            {
                                var size = new Commands.QueryFileSize(_repo.FullPath, file.Path, _commit.SHA).Result();
                                var binary = new Models.RevisionBinaryFile() { Size = size };
                                Dispatcher.UIThread.Invoke(() => ViewRevisionFileContent = binary);
                            }

                            return;
                        }

                        var contentStream = Commands.QueryFileContent.Run(_repo.FullPath, _commit.SHA, file.Path);
                        var content = new StreamReader(contentStream).ReadToEnd();
                        var matchLFS = REG_LFS_FORMAT().Match(content);
                        if (matchLFS.Success)
                        {
                            var obj = new Models.RevisionLFSObject() { Object = new Models.LFSObject() };
                            obj.Object.Oid = matchLFS.Groups[1].Value;
                            obj.Object.Size = long.Parse(matchLFS.Groups[2].Value);
                            Dispatcher.UIThread.Invoke(() => ViewRevisionFileContent = obj);
                        }
                        else
                        {
                            var txt = new Models.RevisionTextFile() { FileName = file.Path, Content = content };
                            Dispatcher.UIThread.Invoke(() => ViewRevisionFileContent = txt);
                        }
                    });
                    break;
                case Models.ObjectType.Commit:
                    Task.Run(() =>
                    {
                        var submoduleRoot = Path.Combine(_repo.FullPath, file.Path);
                        var commit = new Commands.QuerySingleCommit(submoduleRoot, file.SHA).Result();
                        if (commit != null)
                        {
                            var body = new Commands.QueryCommitFullMessage(submoduleRoot, file.SHA).Result();
                            var submodule = new Models.RevisionSubmodule()
                            {
                                Commit = commit,
                                FullMessage = new Models.CommitFullMessage { Message = body }
                            };

                            Dispatcher.UIThread.Invoke(() => ViewRevisionFileContent = submodule);
                        }
                        else
                        {
                            Dispatcher.UIThread.Invoke(() =>
                            {
                                ViewRevisionFileContent = new Models.RevisionSubmodule()
                                {
                                    Commit = new Models.Commit() { SHA = file.SHA },
                                    FullMessage = null,
                                };
                            });
                        }
                    });
                    break;
                default:
                    ViewRevisionFileContent = null;
                    break;
            }
        }

        public ContextMenu CreateChangeContextMenu(Models.Change change)
        {
            var diffWithMerger = new MenuItem();
            diffWithMerger.Header = App.Text("DiffWithMerger");
            diffWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
            diffWithMerger.Click += (_, ev) =>
            {
                var toolType = Preferences.Instance.ExternalMergeToolType;
                var toolPath = Preferences.Instance.ExternalMergeToolPath;
                var opt = new Models.DiffOption(_commit, change);

                Task.Run(() => Commands.MergeTool.OpenForDiff(_repo.FullPath, toolType, toolPath, opt));
                ev.Handled = true;
            };

            var fullPath = Path.Combine(_repo.FullPath, change.Path);
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
                var window = new Views.FileHistories() { DataContext = new FileHistories(_repo, change.Path, _commit.SHA) };
                window.Show();
                ev.Handled = true;
            };

            var blame = new MenuItem();
            blame.Header = App.Text("Blame");
            blame.Icon = App.CreateMenuIcon("Icons.Blame");
            blame.IsEnabled = change.Index != Models.ChangeState.Deleted;
            blame.Click += (_, ev) =>
            {
                var window = new Views.Blame() { DataContext = new Blame(_repo.FullPath, change.Path, _commit.SHA) };
                window.Show();
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

                var baseRevision = _commit.Parents.Count == 0 ? "4b825dc642cb6eb9a060e54bf8d69288fbee4904" : _commit.Parents[0];
                var storageFile = await storageProvider.SaveFilePickerAsync(options);
                if (storageFile != null)
                {
                    var saveTo = storageFile.Path.LocalPath;
                    var succ = await Task.Run(() => Commands.SaveChangesAsPatch.ProcessRevisionCompareChanges(_repo.FullPath, [change], baseRevision, _commit.SHA, saveTo));
                    if (succ)
                        App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                }

                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(diffWithMerger);
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
                resetToThisRevision.Click += (_, ev) =>
                {
                    new Commands.Checkout(_repo.FullPath).FileWithRevision(change.Path, $"{_commit.SHA}");
                    ev.Handled = true;
                };

                var resetToFirstParent = new MenuItem();
                resetToFirstParent.Header = App.Text("ChangeCM.CheckoutFirstParentRevision");
                resetToFirstParent.Icon = App.CreateMenuIcon("Icons.File.Checkout");
                resetToFirstParent.IsEnabled = _commit.Parents.Count > 0;
                resetToFirstParent.Click += (_, ev) =>
                {
                    if (change.Index == Models.ChangeState.Renamed)
                        new Commands.Checkout(_repo.FullPath).FileWithRevision(change.OriginalPath, $"{_commit.SHA}~1");

                    new Commands.Checkout(_repo.FullPath).FileWithRevision(change.Path, $"{_commit.SHA}~1");
                    ev.Handled = true;
                };

                menu.Items.Add(resetToThisRevision);
                menu.Items.Add(resetToFirstParent);
                menu.Items.Add(new MenuItem { Header = "-" });

                if (File.Exists(Path.Combine(fullPath)))
                    TryToAddContextMenuItemsForGitLFS(menu, change.Path);
            }

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyPath.Click += (_, ev) =>
            {
                App.CopyText(change.Path);
                ev.Handled = true;
            };

            var copyFileName = new MenuItem();
            copyFileName.Header = App.Text("CopyFileName");
            copyFileName.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFileName.Click += (_, e) =>
            {
                App.CopyText(Path.GetFileName(change.Path));
                e.Handled = true;
            };

            menu.Items.Add(copyPath);
            menu.Items.Add(copyFileName);
            return menu;
        }

        public ContextMenu CreateRevisionFileContextMenu(Models.Object file)
        {
            var menu = new ContextMenu();
            var fullPath = Path.Combine(_repo.FullPath, file.Path);
            var explore = new MenuItem();
            explore.Header = App.Text("RevealFile");
            explore.Icon = App.CreateMenuIcon("Icons.Explore");
            explore.IsEnabled = File.Exists(fullPath);
            explore.Click += (_, ev) =>
            {
                Native.OS.OpenInFileManager(fullPath, file.Type == Models.ObjectType.Blob);
                ev.Handled = true;
            };

            var openWith = new MenuItem();
            openWith.Header = App.Text("OpenWith");
            openWith.Icon = App.CreateMenuIcon("Icons.OpenWith");
            openWith.Click += (_, ev) =>
            {
                var fileName = Path.GetFileNameWithoutExtension(fullPath) ?? "";
                var fileExt = Path.GetExtension(fullPath) ?? "";
                var tmpFile = Path.Combine(Path.GetTempPath(), $"{fileName}~{_commit.SHA.Substring(0, 10)}{fileExt}");
                Commands.SaveRevisionFile.Run(_repo.FullPath, _commit.SHA, file.Path, tmpFile);
                Native.OS.OpenWithDefaultEditor(tmpFile);
                ev.Handled = true;
            };

            var saveAs = new MenuItem();
            saveAs.Header = App.Text("SaveAs");
            saveAs.Icon = App.CreateMenuIcon("Icons.Save");
            saveAs.IsEnabled = file.Type == Models.ObjectType.Blob;
            saveAs.Click += async (_, ev) =>
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
                        var saveTo = Path.Combine(selected[0].Path.LocalPath, Path.GetFileName(file.Path));
                        Commands.SaveRevisionFile.Run(_repo.FullPath, _commit.SHA, file.Path, saveTo);
                    }
                }
                catch (Exception e)
                {
                    App.RaiseException(_repo.FullPath, $"Failed to save file: {e.Message}");
                }

                ev.Handled = true;
            };

            menu.Items.Add(explore);
            menu.Items.Add(openWith);
            menu.Items.Add(saveAs);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var history = new MenuItem();
            history.Header = App.Text("FileHistory");
            history.Icon = App.CreateMenuIcon("Icons.Histories");
            history.Click += (_, ev) =>
            {
                var window = new Views.FileHistories() { DataContext = new FileHistories(_repo, file.Path, _commit.SHA) };
                window.Show();
                ev.Handled = true;
            };

            var blame = new MenuItem();
            blame.Header = App.Text("Blame");
            blame.Icon = App.CreateMenuIcon("Icons.Blame");
            blame.IsEnabled = file.Type == Models.ObjectType.Blob;
            blame.Click += (_, ev) =>
            {
                var window = new Views.Blame() { DataContext = new Blame(_repo.FullPath, file.Path, _commit.SHA) };
                window.Show();
                ev.Handled = true;
            };

            menu.Items.Add(history);
            menu.Items.Add(blame);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var resetToThisRevision = new MenuItem();
            resetToThisRevision.Header = App.Text("ChangeCM.CheckoutThisRevision");
            resetToThisRevision.Icon = App.CreateMenuIcon("Icons.File.Checkout");
            resetToThisRevision.IsEnabled = File.Exists(fullPath);
            resetToThisRevision.Click += (_, ev) =>
            {
                new Commands.Checkout(_repo.FullPath).FileWithRevision(file.Path, $"{_commit.SHA}");
                ev.Handled = true;
            };

            var resetToFirstParent = new MenuItem();
            resetToFirstParent.Header = App.Text("ChangeCM.CheckoutFirstParentRevision");
            resetToFirstParent.Icon = App.CreateMenuIcon("Icons.File.Checkout");
            var fileInChanges = _changes.Find(x => x.Path == file.Path);
            var fileIndex = fileInChanges?.Index;
            resetToFirstParent.IsEnabled = _commit.Parents.Count > 0 && fileIndex != Models.ChangeState.Renamed;
            resetToFirstParent.Click += (_, ev) =>
            {
                new Commands.Checkout(_repo.FullPath).FileWithRevision(file.Path, $"{_commit.SHA}~1");
                ev.Handled = true;
            };

            menu.Items.Add(resetToThisRevision);
            menu.Items.Add(resetToFirstParent);
            menu.Items.Add(new MenuItem() { Header = "-" });

            if (File.Exists(Path.Combine(fullPath)))
                TryToAddContextMenuItemsForGitLFS(menu, file.Path);

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyPath.Click += (_, ev) =>
            {
                App.CopyText(file.Path);
                ev.Handled = true;
            };

            var copyFileName = new MenuItem();
            copyFileName.Header = App.Text("CopyFileName");
            copyFileName.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFileName.Click += (_, e) =>
            {
                App.CopyText(Path.GetFileName(file.Path));
                e.Handled = true;
            };

            menu.Items.Add(copyPath);
            menu.Items.Add(copyFileName);
            return menu;
        }

        private void Refresh()
        {
            _changes = null;
            _revisionFiles = null;

            SignInfo = null;
            ViewRevisionFileContent = null;
            Children = null;
            RevisionFileSearchFilter = string.Empty;
            RevisionFileSearchSuggestion = null;

            if (_commit == null)
                return;

            Task.Run(() =>
            {
                var message = new Commands.QueryCommitFullMessage(_repo.FullPath, _commit.SHA).Result();
                var links = ParseLinksInMessage(message);
                Dispatcher.UIThread.Invoke(() => FullMessage = new Models.CommitFullMessage { Message = message, Links = links });
            });

            Task.Run(() =>
            {
                var signInfo = new Commands.QueryCommitSignInfo(_repo.FullPath, _commit.SHA, !_repo.HasAllowedSignersFile).Result();
                Dispatcher.UIThread.Invoke(() => SignInfo = signInfo);
            });

            if (_cancelToken != null)
                _cancelToken.Requested = true;

            _cancelToken = new Commands.Command.CancelToken();

            if (Preferences.Instance.ShowChildren)
            {
                Task.Run(() =>
                {
                    var max = Preferences.Instance.MaxHistoryCommits;
                    var cmdChildren = new Commands.QueryCommitChildren(_repo.FullPath, _commit.SHA, max) { Cancel = _cancelToken };
                    var children = cmdChildren.Result();
                    if (!cmdChildren.Cancel.Requested)
                        Dispatcher.UIThread.Post(() => Children = children);
                });
            }

            Task.Run(() =>
            {
                var parent = _commit.Parents.Count == 0 ? "4b825dc642cb6eb9a060e54bf8d69288fbee4904" : _commit.Parents[0];
                var cmdChanges = new Commands.CompareRevisions(_repo.FullPath, parent, _commit.SHA) { Cancel = _cancelToken };
                var changes = cmdChanges.Result();
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

                if (!cmdChanges.Cancel.Requested)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Changes = changes;
                        VisibleChanges = visible;

                        if (visible.Count == 0)
                            SelectedChanges = null;
                    });
                }
            });
        }

        private List<Models.Hyperlink> ParseLinksInMessage(string message)
        {
            var links = new List<Models.Hyperlink>();
            if (_repo.Settings.IssueTrackerRules is { Count: > 0 } rules)
            {
                foreach (var rule in rules)
                    rule.Matches(links, message);
            }

            var matches = REG_SHA_FORMAT().Matches(message);
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (!match.Success)
                    continue;

                var start = match.Index;
                var len = match.Length;
                var intersect = false;
                foreach (var link in links)
                {
                    if (link.Intersect(start, len))
                    {
                        intersect = true;
                        break;
                    }
                }

                if (intersect)
                    continue;

                var sha = match.Groups[1].Value;
                var isCommitSHA = new Commands.IsCommitSHA(_repo.FullPath, sha).Result();
                if (isCommitSHA)
                    links.Add(new Models.Hyperlink(start, len, sha, true));
            }

            if (links.Count > 0)
                links.Sort((l, r) => l.Start - r.Start);

            return links;
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

        private void TryToAddContextMenuItemsForGitLFS(ContextMenu menu, string path)
        {
            var lfsEnabled = new Commands.LFS(_repo.FullPath).IsEnabled();
            if (!lfsEnabled)
                return;

            var lfs = new MenuItem();
            lfs.Header = App.Text("GitLFS");
            lfs.Icon = App.CreateMenuIcon("Icons.LFS");

            var lfsLock = new MenuItem();
            lfsLock.Header = App.Text("GitLFS.Locks.Lock");
            lfsLock.Icon = App.CreateMenuIcon("Icons.Lock");
            lfsLock.IsEnabled = _repo.Remotes.Count > 0;
            if (_repo.Remotes.Count == 1)
            {
                lfsLock.Click += async (_, e) =>
                {
                    var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Lock(_repo.Remotes[0].Name, path));
                    if (succ)
                        App.SendNotification(_repo.FullPath, $"Lock file \"{path}\" successfully!");

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
                        var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Lock(remoteName, path));
                        if (succ)
                            App.SendNotification(_repo.FullPath, $"Lock file \"{path}\" successfully!");

                        e.Handled = true;
                    };
                    lfsLock.Items.Add(lockRemote);
                }
            }
            lfs.Items.Add(lfsLock);

            var lfsUnlock = new MenuItem();
            lfsUnlock.Header = App.Text("GitLFS.Locks.Unlock");
            lfsUnlock.Icon = App.CreateMenuIcon("Icons.Unlock");
            lfsUnlock.IsEnabled = _repo.Remotes.Count > 0;
            if (_repo.Remotes.Count == 1)
            {
                lfsUnlock.Click += async (_, e) =>
                {
                    var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Unlock(_repo.Remotes[0].Name, path, false));
                    if (succ)
                        App.SendNotification(_repo.FullPath, $"Unlock file \"{path}\" successfully!");

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
                        var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Unlock(remoteName, path, false));
                        if (succ)
                            App.SendNotification(_repo.FullPath, $"Unlock file \"{path}\" successfully!");

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
                    var sha = Commit.SHA;

                    Task.Run(() =>
                    {
                        var files = new Commands.QueryRevisionFileNames(_repo.FullPath, sha).Result();
                        var filesList = new List<string>();
                        filesList.AddRange(files);

                        Dispatcher.UIThread.Invoke(() =>
                        {
                            if (sha == Commit.SHA)
                            {
                                _revisionFiles = filesList;
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

        [GeneratedRegex(@"\b([0-9a-fA-F]{6,40})\b")]
        private static partial Regex REG_SHA_FORMAT();

        [GeneratedRegex(@"^version https://git-lfs.github.com/spec/v\d+\r?\noid sha256:([0-9a-f]+)\r?\nsize (\d+)[\r\n]*$")]
        private static partial Regex REG_LFS_FORMAT();

        private static readonly HashSet<string> IMG_EXTS = new HashSet<string>()
        {
            ".ico", ".bmp", ".jpg", ".png", ".jpeg", ".webp"
        };

        private Repository _repo = null;
        private Models.Commit _commit = null;
        private Models.CommitFullMessage _fullMessage = null;
        private Models.CommitSignInfo _signInfo = null;
        private List<string> _children = null;
        private List<Models.Change> _changes = null;
        private List<Models.Change> _visibleChanges = null;
        private List<Models.Change> _selectedChanges = null;
        private string _searchChangeFilter = string.Empty;
        private DiffContext _diffContext = null;
        private object _viewRevisionFileContent = null;
        private Commands.Command.CancelToken _cancelToken = null;
        private List<string> _revisionFiles = null;
        private string _revisionFileSearchFilter = string.Empty;
        private List<string> _revisionFileSearchSuggestion = null;
    }
}
