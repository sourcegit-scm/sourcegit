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
        public AvaloniaList<LauncherPage> Pages
        {
            get;
            private set;
        }

        public LauncherPage ActivePage
        {
            get => _activePage;
            set
            {
                var old = _activePage;
                if (SetProperty(ref _activePage, value)) {
                    PopupHost.Active = value;
                    UpdateSearchFilter(old, value);
                }
            }
        }

        public Launcher(string startupRepo)
        {
            Pages = new AvaloniaList<LauncherPage>();
            AddNewTab();

            var pref = Preference.Instance;
            if (!string.IsNullOrEmpty(startupRepo))
            {
                var root = new Commands.QueryRepositoryRootPath(startupRepo).Result();
                if (string.IsNullOrEmpty(root))
                {
                    Pages[0].Notifications.Add(new Models.Notification
                    {
                        IsError = true,
                        Message = $"Given path: '{startupRepo}' is NOT a valid repository!"
                    });
                    return;
                }

                var normalized = root.Replace("\\", "/");
                var node = pref.FindOrAddNodeByRepositoryPath(normalized, null, false);
                Welcome.Instance.Refresh();
                OpenRepositoryInTab(node, null);
            }
            else if (pref.RestoreTabs)
            {
                foreach (var id in pref.OpenedTabs)
                {
                    var node = pref.FindNode(id);
                    if (node == null)
                    {
                        node = new RepositoryNode()
                        {
                            Id = id,
                            Name = Path.GetFileName(id),
                            Bookmark = 0,
                            IsRepository = true,
                        };
                    }

                    OpenRepositoryInTab(node, null);
                }

                var lastActiveIdx = pref.LastActiveTabIdx;
                if (lastActiveIdx >= 0 && lastActiveIdx < Pages.Count)
                    ActivePage = Pages[lastActiveIdx];
            }
        }

        public void Quit()
        {
            var pref = Preference.Instance;
            pref.OpenedTabs.Clear();

            if (pref.RestoreTabs)
            {
                foreach (var page in Pages)
                {
                    if (page.Node.IsRepository)
                        pref.OpenedTabs.Add(page.Node.Id);
                }
            }

            pref.LastActiveTabIdx = Pages.IndexOf(ActivePage);
            pref.Save();

            foreach (var page in Pages)
            {
                if (page.Data is Repository repo)
                    repo.Close();
            }
        }

        public void AddNewTab()
        {
            var page = new LauncherPage();
            Pages.Add(page);
            ActivePage = page;
        }

        public void MoveTab(LauncherPage from, LauncherPage to)
        {
            var fromIdx = Pages.IndexOf(from);
            var toIdx = Pages.IndexOf(to);
            Pages.Move(fromIdx, toIdx);
            ActivePage = from;
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
                    Models.AutoFetchManager.Instance.RemoveRepository(repo.FullPath);
                    repo.Close();

                    last.Node = new RepositoryNode() { Id = Guid.NewGuid().ToString() };
                    last.Data = Welcome.Instance;
                    last.Popup = null;

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
                ActivePage = Pages[removeIdx == Pages.Count - 1 ? removeIdx - 1 : removeIdx + 1];
                CloseRepositoryInTab(page);
                Pages.RemoveAt(removeIdx);
            }
            else if (removeIdx + 1 == activeIdx)
            {
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

            var id = ActivePage.Node.Id;
            foreach (var one in Pages)
            {
                if (one.Node.Id != id)
                    CloseRepositoryInTab(one);
            }

            Pages = new AvaloniaList<LauncherPage> { ActivePage };
            OnPropertyChanged(nameof(Pages));
            GC.Collect();
        }

        public void CloseRightTabs()
        {
            var endIdx = Pages.IndexOf(ActivePage);
            for (var i = Pages.Count - 1; i > endIdx; i--)
            {
                CloseRepositoryInTab(Pages[i]);
                Pages.Remove(Pages[i]);
            }

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
            Models.AutoFetchManager.Instance.AddRepository(repo.FullPath);

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

            ActivePage = page;
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

        private void CloseRepositoryInTab(LauncherPage page)
        {
            if (page.Data is Repository repo)
            {
                Models.AutoFetchManager.Instance.RemoveRepository(repo.FullPath);
                repo.Close();
            }

            page.Data = null;
        }

        private static void UpdateSearchFilter(LauncherPage oldPage, LauncherPage newPage) {
            if (oldPage != null) {
                oldPage.SearchFilter = Welcome.Instance.SearchFilter;
            }

            Welcome.Instance.SearchFilter = newPage.SearchFilter;
        }

        private LauncherPage _activePage = null;
    }
}
