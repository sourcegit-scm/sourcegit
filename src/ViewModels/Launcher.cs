using System;
using System.IO;

using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;

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

        public Launcher(string startupRepo)
        {
            _ignoreIndexChange = true;

            Pages = new AvaloniaList<LauncherPage>();
            AddNewTab();

            var pref = Preference.Instance;
            if (string.IsNullOrEmpty(startupRepo))
            {
                ActiveWorkspace = pref.GetActiveWorkspace();

                var repos = ActiveWorkspace.Repositories.ToArray();
                foreach (var repo in repos)
                {
                    var node = pref.FindNode(repo);
                    if (node == null)
                    {
                        node = new RepositoryNode()
                        {
                            Id = repo,
                            Name = Path.GetFileName(repo),
                            Bookmark = 0,
                            IsRepository = true,
                        };
                    }

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

                var test = new Commands.QueryRepositoryRootPath(startupRepo).ReadToEnd();
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
                    var normalized = test.StdOut.Trim().Replace("\\", "/");
                    var node = pref.FindOrAddNodeByRepositoryPath(normalized, null, false);
                    Welcome.Instance.Refresh();
                    OpenRepositoryInTab(node, null);
                }
            }

            _ignoreIndexChange = false;

            if (string.IsNullOrEmpty(_title))
                UpdateTitle();
        }

        public void Quit(double width, double height)
        {
            var pref = Preference.Instance;
            pref.Layout.LauncherWidth = width;
            pref.Layout.LauncherHeight = height;
            pref.Save();

            _ignoreIndexChange = true;

            foreach (var one in Pages)
                CloseRepositoryInTab(one, false);

            _ignoreIndexChange = false;
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

            if (page == null)
                page = _activePage;

            var removeIdx = Pages.IndexOf(page);
            var activeIdx = Pages.IndexOf(_activePage);
            if (removeIdx == activeIdx)
            {
                ActivePage = Pages[removeIdx > 0 ? removeIdx - 1 : removeIdx + 1];
                CloseRepositoryInTab(page);
                Pages.RemoveAt(removeIdx);
            }
            else
            {
                CloseRepositoryInTab(page);
                Pages.RemoveAt(removeIdx);
            }

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
                var ctx = page == null ? ActivePage.Node.Id : page.Node.Id;
                App.RaiseException(ctx, "Repository does NOT exists any more. Please remove it.");
                return;
            }

            var gitDir = new Commands.QueryGitDir(node.Id).Result();
            if (string.IsNullOrEmpty(gitDir))
            {
                var ctx = page == null ? ActivePage.Node.Id : page.Node.Id;
                App.RaiseException(ctx, "Given path is not a valid git repository!");
                return;
            }

            var repo = new Repository()
            {
                FullPath = node.Id,
                GitDir = gitDir,
            };
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
            var notification = new Models.Notification()
            {
                IsError = isError,
                Message = message,
            };

            foreach (var page in Pages)
            {
                var id = page.Node.Id.Replace("\\", "/");
                if (id == pageId)
                {
                    page.Notifications.Add(notification);
                    return;
                }
            }

            if (_activePage != null)
                _activePage.Notifications.Add(notification);
        }

        public ContextMenu CreateContextForWorkspace()
        {
            var pref = Preference.Instance;
            var menu = new ContextMenu();

            for (var i = 0; i < pref.Workspaces.Count; i++)
            {
                var workspace = pref.Workspaces[i];

                var icon = App.CreateMenuIcon(workspace.IsActive ? "Icons.Check" : "Icons.Workspace");
                icon.Fill = workspace.Brush;

                var item = new MenuItem();
                item.Header = workspace.Name;
                item.Icon = icon;
                item.Click += (_, e) =>
                {
                    if (!workspace.IsActive)
                        SwitchWorkspace(workspace);

                    e.Handled = true;
                };

                menu.Items.Add(item);
            }

            menu.Items.Add(new MenuItem() { Header = "-" });

            var configure = new MenuItem();
            configure.Header = App.Text("Workspace.Configure");
            configure.Click += (_, e) =>
            {
                App.OpenDialog(new Views.ConfigureWorkspace() { DataContext = new ConfigureWorkspace() });
                e.Handled = true;
            };
            menu.Items.Add(configure);

            return menu;
        }

        public ContextMenu CreateContextForPageTab(LauncherPage page)
        {
            if (page == null)
                return null;

            var menu = new ContextMenu();
            var close = new MenuItem();
            close.Header = App.Text("PageTabBar.Tab.Close");
            close.InputGesture = KeyGesture.Parse(OperatingSystem.IsMacOS() ? "⌘+W" : "Ctrl+W");
            close.Click += (_, e) =>
            {
                CloseTab(page);
                e.Handled = true;
            };
            menu.Items.Add(close);

            var closeOthers = new MenuItem();
            closeOthers.Header = App.Text("PageTabBar.Tab.CloseOther");
            closeOthers.Click += (_, e) =>
            {
                CloseOtherTabs();
                e.Handled = true;
            };
            menu.Items.Add(closeOthers);

            var closeRight = new MenuItem();
            closeRight.Header = App.Text("PageTabBar.Tab.CloseRight");
            closeRight.Click += (_, e) =>
            {
                CloseRightTabs();
                e.Handled = true;
            };
            menu.Items.Add(closeRight);

            if (page.Node.IsRepository)
            {
                var bookmark = new MenuItem();
                bookmark.Header = App.Text("PageTabBar.Tab.Bookmark");
                bookmark.Icon = App.CreateMenuIcon("Icons.Bookmark");

                for (int i = 0; i < Models.Bookmarks.Supported.Count; i++)
                {
                    var icon = App.CreateMenuIcon("Icons.Bookmark");

                    if (i != 0)
                        icon.Fill = Models.Bookmarks.Brushes[i];

                    var dupIdx = i;
                    var setter = new MenuItem();
                    setter.Header = icon;
                    setter.Click += (_, e) =>
                    {
                        page.Node.Bookmark = dupIdx;
                        e.Handled = true;
                    };
                    bookmark.Items.Add(setter);
                }
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(bookmark);

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("PageTabBar.Tab.CopyPath");
                copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
                copyPath.Click += (_, e) =>
                {
                    page.CopyPath();
                    e.Handled = true;
                };
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(copyPath);
            }

            return menu;
        }

        private void SwitchWorkspace(Workspace to)
        {
            foreach (var one in Pages)
            {
                if (!one.CanCreatePopup() || one.Data is Repository { IsAutoFetching: true })
                {
                    App.RaiseException(null, "You have unfinished task(s) in opened pages. Please wait!!!");
                    return;
                }
            }

            _ignoreIndexChange = true;

            var pref = Preference.Instance;
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
                var node = pref.FindNode(repo);
                if (node == null)
                {
                    node = new RepositoryNode()
                    {
                        Id = repo,
                        Name = Path.GetFileName(repo),
                        Bookmark = 0,
                        IsRepository = true,
                    };
                }

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
            GC.Collect();
        }

        private void CloseRepositoryInTab(LauncherPage page, bool removeFromWorkspace = true)
        {
            if (page.Data is Repository repo)
            {
                if (removeFromWorkspace)
                    ActiveWorkspace.Repositories.Remove(repo.FullPath);

                repo.Close();
            }

            page.Data = null;
        }

        private void UpdateTitle()
        {
            if (_activeWorkspace == null)
                return;

            var workspace = _activeWorkspace.Name;
            if (_activePage is { Data: Repository repo })
            {
                var node = _activePage.Node;
                var name = node.Name;
                var path = node.Id;

                if (!OperatingSystem.IsWindows())
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var prefixLen = home.EndsWith('/') ? home.Length - 1 : home.Length;
                    if (path.StartsWith(home, StringComparison.Ordinal))
                        path = "~" + path.Substring(prefixLen);
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
    }
}
