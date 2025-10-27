using System;
using System.IO;

using Avalonia.Collections;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Launcher : ObservableObject
    {
        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        public AvaloniaList<LauncherPage> Pages
        {
            get;
            private set;
        }

        public Workspace ActiveWorkspace
        {
            get => _activeWorkspace;
            private set => SetProperty(ref _activeWorkspace, value);
        }

        public LauncherPage ActivePage
        {
            get => _activePage;
            set
            {
                if (SetProperty(ref _activePage, value))
                {
                    UpdateTitle();

                    if (!_ignoreIndexChange && value is { Data: Repository repo })
                        _activeWorkspace.ActiveIdx = _activeWorkspace.Repositories.IndexOf(repo.FullPath);
                }
            }
        }

        public QuickLauncher QuickLauncher
        {
            get => _quickLauncher;
            set => SetProperty(ref _quickLauncher, value);
        }

        public Launcher(string startupRepo)
        {
            _ignoreIndexChange = true;

            Pages = new AvaloniaList<LauncherPage>();
            AddNewTab();

            var pref = Preferences.Instance;
            if (string.IsNullOrEmpty(startupRepo))
            {
                ActiveWorkspace = pref.GetActiveWorkspace();

                var repos = ActiveWorkspace.Repositories.ToArray();
                foreach (var repo in repos)
                {
                    var node = pref.FindNode(repo) ??
                        new RepositoryNode
                        {
                            Id = repo,
                            Name = Path.GetFileName(repo),
                            Bookmark = 0,
                            IsRepository = true,
                        };

                    OpenRepositoryInTab(node, null);
                }

                var activeIdx = ActiveWorkspace.ActiveIdx;
                if (activeIdx >= 0 && activeIdx < Pages.Count)
                {
                    ActivePage = Pages[activeIdx];
                }
                else
                {
                    ActivePage = Pages[0];
                    ActiveWorkspace.ActiveIdx = 0;
                }
            }
            else
            {
                ActiveWorkspace = new Workspace() { Name = "Unnamed" };

                foreach (var w in pref.Workspaces)
                    w.IsActive = false;

                var test = new Commands.QueryRepositoryRootPath(startupRepo).GetResult();
                if (!test.IsSuccess || string.IsNullOrEmpty(test.StdOut))
                {
                    Pages[0].Notifications.Add(new Models.Notification
                    {
                        IsError = true,
                        Message = $"Given path: '{startupRepo}' is NOT a valid repository!"
                    });
                }
                else
                {
                    var node = pref.FindOrAddNodeByRepositoryPath(test.StdOut.Trim(), null, false);
                    Welcome.Instance.Refresh();
                    OpenRepositoryInTab(node, null);
                }
            }

            _ignoreIndexChange = false;

            if (string.IsNullOrEmpty(_title))
                UpdateTitle();
        }

        public void Quit()
        {
            _ignoreIndexChange = true;

            foreach (var one in Pages)
                CloseRepositoryInTab(one, false);

            _ignoreIndexChange = false;
        }

        public void SwitchWorkspace(Workspace to)
        {
            if (to == null || to.IsActive)
                return;

            foreach (var one in Pages)
            {
                if (!one.CanCreatePopup() || one.Data is Repository { IsAutoFetching: true })
                {
                    App.RaiseException(null, "You have unfinished task(s) in opened pages. Please wait!!!");
                    return;
                }
            }

            _ignoreIndexChange = true;

            var pref = Preferences.Instance;
            foreach (var w in pref.Workspaces)
                w.IsActive = false;

            ActiveWorkspace = to;
            to.IsActive = true;

            foreach (var one in Pages)
                CloseRepositoryInTab(one, false);

            Pages.Clear();
            AddNewTab();

            var repos = to.Repositories.ToArray();
            foreach (var repo in repos)
            {
                var node = pref.FindNode(repo) ??
                    new RepositoryNode
                    {
                        Id = repo,
                        Name = Path.GetFileName(repo),
                        Bookmark = 0,
                        IsRepository = true,
                    };

                OpenRepositoryInTab(node, null);
            }

            var activeIdx = to.ActiveIdx;
            if (activeIdx >= 0 && activeIdx < Pages.Count)
            {
                ActivePage = Pages[activeIdx];
            }
            else
            {
                ActivePage = Pages[0];
                to.ActiveIdx = 0;
            }

            _ignoreIndexChange = false;
            Preferences.Instance.Save();
            GC.Collect();
        }

        public void AddNewTab()
        {
            var page = new LauncherPage();
            Pages.Add(page);
            ActivePage = page;
        }

        public void MoveTab(LauncherPage from, LauncherPage to)
        {
            _ignoreIndexChange = true;

            var fromIdx = Pages.IndexOf(from);
            var toIdx = Pages.IndexOf(to);
            Pages.Move(fromIdx, toIdx);
            ActivePage = from;

            ActiveWorkspace.Repositories.Clear();
            foreach (var p in Pages)
            {
                if (p.Data is Repository r)
                    ActiveWorkspace.Repositories.Add(r.FullPath);
            }
            ActiveWorkspace.ActiveIdx = ActiveWorkspace.Repositories.IndexOf(from.Node.Id);

            _ignoreIndexChange = false;
        }

        public void GotoNextTab()
        {
            if (Pages.Count == 1)
                return;

            var activeIdx = Pages.IndexOf(_activePage);
            var nextIdx = (activeIdx + 1) % Pages.Count;
            ActivePage = Pages[nextIdx];
        }

        public void GotoPrevTab()
        {
            if (Pages.Count == 1)
                return;

            var activeIdx = Pages.IndexOf(_activePage);
            var prevIdx = activeIdx == 0 ? Pages.Count - 1 : activeIdx - 1;
            ActivePage = Pages[prevIdx];
        }

        public void CloseTab(LauncherPage page)
        {
            if (Pages.Count == 1)
            {
                var last = Pages[0];
                if (last.Data is Repository repo)
                {
                    ActiveWorkspace.Repositories.Clear();
                    ActiveWorkspace.ActiveIdx = 0;

                    repo.Close();

                    Welcome.Instance.ClearSearchFilter();
                    last.Node = new RepositoryNode() { Id = Guid.NewGuid().ToString() };
                    last.Data = Welcome.Instance;
                    last.Popup?.Cleanup();
                    last.Popup = null;
                    UpdateTitle();

                    GC.Collect();
                }
                else
                {
                    App.Quit(0);
                }

                return;
            }

            page ??= _activePage;

            var removeIdx = Pages.IndexOf(page);
            var activeIdx = Pages.IndexOf(_activePage);
            if (removeIdx == activeIdx)
                ActivePage = Pages[removeIdx > 0 ? removeIdx - 1 : removeIdx + 1];

            CloseRepositoryInTab(page);
            Pages.RemoveAt(removeIdx);
            GC.Collect();
        }

        public void CloseOtherTabs()
        {
            if (Pages.Count == 1)
                return;

            _ignoreIndexChange = true;

            var id = ActivePage.Node.Id;
            foreach (var one in Pages)
            {
                if (one.Node.Id != id)
                    CloseRepositoryInTab(one);
            }

            Pages = new AvaloniaList<LauncherPage> { ActivePage };
            ActiveWorkspace.ActiveIdx = 0;
            OnPropertyChanged(nameof(Pages));

            _ignoreIndexChange = false;
            GC.Collect();
        }

        public void CloseRightTabs()
        {
            _ignoreIndexChange = true;

            var endIdx = Pages.IndexOf(ActivePage);
            for (var i = Pages.Count - 1; i > endIdx; i--)
            {
                CloseRepositoryInTab(Pages[i]);
                Pages.Remove(Pages[i]);
            }

            _ignoreIndexChange = false;
            GC.Collect();
        }

        public void OpenRepositoryInTab(RepositoryNode node, LauncherPage page)
        {
            foreach (var one in Pages)
            {
                if (one.Node.Id == node.Id)
                {
                    ActivePage = one;
                    return;
                }
            }

            if (!Path.Exists(node.Id))
            {
                App.RaiseException(node.Id, "Repository does NOT exist any more. Please remove it.");
                return;
            }

            var isBare = new Commands.IsBareRepository(node.Id).GetResult();
            var gitDir = isBare ? node.Id : GetRepositoryGitDir(node.Id);
            if (string.IsNullOrEmpty(gitDir))
            {
                App.RaiseException(node.Id, "Given path is not a valid git repository!");
                return;
            }

            var repo = new Repository(isBare, node.Id, gitDir);
            repo.Open();

            if (page == null)
            {
                if (ActivePage == null || ActivePage.Node.IsRepository)
                {
                    page = new LauncherPage(node, repo);
                    Pages.Add(page);
                }
                else
                {
                    page = ActivePage;
                    page.Node = node;
                    page.Data = repo;
                }
            }
            else
            {
                page.Node = node;
                page.Data = repo;
            }

            if (page != _activePage)
                ActivePage = page;
            else
                UpdateTitle();

            ActiveWorkspace.Repositories.Clear();
            foreach (var p in Pages)
            {
                if (p.Data is Repository r)
                    ActiveWorkspace.Repositories.Add(r.FullPath);
            }

            if (!_ignoreIndexChange)
                ActiveWorkspace.ActiveIdx = ActiveWorkspace.Repositories.IndexOf(node.Id);
        }

        public void DispatchNotification(string pageId, string message, bool isError)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => DispatchNotification(pageId, message, isError));
                return;
            }

            var notification = new Models.Notification()
            {
                IsError = isError,
                Message = message,
            };

            foreach (var page in Pages)
            {
                var id = page.Node.Id.Replace('\\', '/').TrimEnd('/');
                if (id == pageId)
                {
                    page.Notifications.Add(notification);
                    return;
                }
            }

            _activePage?.Notifications.Add(notification);
        }

        private string GetRepositoryGitDir(string repo)
        {
            var fullpath = Path.Combine(repo, ".git");
            if (Directory.Exists(fullpath))
            {
                if (Directory.Exists(Path.Combine(fullpath, "refs")) &&
                    Directory.Exists(Path.Combine(fullpath, "objects")) &&
                    File.Exists(Path.Combine(fullpath, "HEAD")))
                    return fullpath;

                return null;
            }

            if (File.Exists(fullpath))
            {
                var redirect = File.ReadAllText(fullpath).Trim();
                if (redirect.StartsWith("gitdir: ", StringComparison.Ordinal))
                    redirect = redirect.Substring(8);

                if (!Path.IsPathRooted(redirect))
                    redirect = Path.GetFullPath(Path.Combine(repo, redirect));

                if (Directory.Exists(redirect))
                    return redirect;

                return null;
            }

            return new Commands.QueryGitDir(repo).GetResult();
        }

        private void CloseRepositoryInTab(LauncherPage page, bool removeFromWorkspace = true)
        {
            if (page.Data is Repository repo)
            {
                if (removeFromWorkspace)
                    ActiveWorkspace.Repositories.Remove(repo.FullPath);

                repo.Close();
            }

            page.Popup?.Cleanup();
            page.Popup = null;
            page.Data = null;
        }

        private void UpdateTitle()
        {
            if (_activeWorkspace == null)
                return;

            var workspace = _activeWorkspace.Name;
            if (_activePage is { Data: Repository })
            {
                var node = _activePage.Node;
                var name = node.Name;
                var path = node.Id;

                if (!OperatingSystem.IsWindows())
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var prefixLen = home.EndsWith('/') ? home.Length - 1 : home.Length;
                    if (path.StartsWith(home, StringComparison.Ordinal))
                        path = $"~{path.AsSpan(prefixLen)}";
                }

                Title = $"[{workspace}] {name} ({path})";
            }
            else
            {
                Title = $"[{workspace}] Repositories";
            }
        }

        private Workspace _activeWorkspace = null;
        private LauncherPage _activePage = null;
        private bool _ignoreIndexChange = false;
        private string _title = string.Empty;
        private QuickLauncher _quickLauncher = null;
    }
}
