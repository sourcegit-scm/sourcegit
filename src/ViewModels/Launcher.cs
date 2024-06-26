using System;
using System.IO;

using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

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
                if (SetProperty(ref _activePage, value))
                {
                    PopupHost.Active = value;
                    UpdateTabSplitterVisible();
                }
            }
        }

        public Launcher()
        {
            Pages = new AvaloniaList<LauncherPage>();
            AddNewTab();

            var commandlines = Environment.GetCommandLineArgs();
            if (commandlines.Length == 2)
            {
                var path = commandlines[1];
                var root = new Commands.QueryRepositoryRootPath(path).Result();
                if (string.IsNullOrEmpty(root))
                {
                    Pages[0].Notifications.Add(new Notification
                    {
                        IsError = true,
                        Message = $"Given path: '{path}' is NOT a valid repository!"
                    });
                    return;
                }

                var gitDir = new Commands.QueryGitDir(root).Result();
                var repo = Preference.AddRepository(root, gitDir);
                var node = Preference.FindOrAddNodeByRepositoryPath(repo.FullPath, null, false);
                OpenRepositoryInTab(node, null);
            }
            else if (Preference.Instance.RestoreTabs)
            {
                foreach (var id in Preference.Instance.OpenedTabs)
                {
                    var node = Preference.FindNode(id);
                    if (node == null)
                        continue;

                    OpenRepositoryInTab(node, null);
                }

                var lastActiveIdx = Preference.Instance.LastActiveTabIdx;
                if (lastActiveIdx >= 0 && lastActiveIdx < Pages.Count)
                {
                    ActivePage = Pages[lastActiveIdx];
                }
            }
        }

        public void Quit()
        {
            Preference.Instance.OpenedTabs.Clear();

            if (Preference.Instance.RestoreTabs)
            {
                foreach (var page in Pages)
                {
                    if (page.Node.IsRepository)
                        Preference.Instance.OpenedTabs.Add(page.Node.Id);
                }
            }

            Preference.Instance.LastActiveTabIdx = Pages.IndexOf(ActivePage);
            Preference.Save();
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
                    Commands.AutoFetch.RemoveRepository(repo.FullPath);
                    repo.Close();

                    last.Node = new RepositoryNode() { Id = Guid.NewGuid().ToString() };
                    last.Data = Welcome.Instance;
                    last.Popup = null;

                    GC.Collect();
                }
                else
                {
                    App.Quit();
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
                UpdateTabSplitterVisible();
            }
            else if (removeIdx + 1 == activeIdx)
            {
                CloseRepositoryInTab(page);
                Pages.RemoveAt(removeIdx);
                UpdateTabSplitterVisible();
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

            var repo = Preference.FindRepository(node.Id);
            if (repo == null || !Path.Exists(repo.FullPath))
            {
                var ctx = page == null ? ActivePage.Node.Id : page.Node.Id;
                App.RaiseException(ctx, "Repository does NOT exists any more. Please remove it.");
                return;
            }

            repo.Open();
            Commands.AutoFetch.AddRepository(repo.FullPath);

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
            var notification = new Notification() { 
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

        public void DismissNotification(Notification notice)
        {
            if (notice != null)
                ActivePage?.Notifications.Remove(notice);
        }

        public ContextMenu CreateContextForPageTab(LauncherPage page)
        {
            if (page == null)
                return null;

            var menu = new ContextMenu();
            var close = new MenuItem();
            close.Header = App.Text("PageTabBar.Tab.Close");
            close.InputGesture = KeyGesture.Parse(OperatingSystem.IsMacOS() ? "⌘+W" : "Ctrl+W");
            close.Click += (o, e) =>
            {
                CloseTab(page);
                e.Handled = true;
            };
            menu.Items.Add(close);

            var closeOthers = new MenuItem();
            closeOthers.Header = App.Text("PageTabBar.Tab.CloseOther");
            closeOthers.Click += (o, e) =>
            {
                CloseOtherTabs();
                e.Handled = true;
            };
            menu.Items.Add(closeOthers);

            var closeRight = new MenuItem();
            closeRight.Header = App.Text("PageTabBar.Tab.CloseRight");
            closeRight.Click += (o, e) =>
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
                    icon.Fill = Models.Bookmarks.Brushes[i];
                    icon.Stroke = App.Current.FindResource("Brush.FG1") as Brush;
                    icon.StrokeThickness = i == 0 ? 1.0 : 0;

                    var dupIdx = i;
                    var setter = new MenuItem();
                    setter.Header = icon;
                    setter.Click += (o, e) =>
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
                copyPath.Click += (o, e) =>
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
                Commands.AutoFetch.RemoveRepository(repo.FullPath);
                repo.Close();
            }

            page.Data = null;
        }

        private void UpdateTabSplitterVisible()
        {
            var activePageIdx = ActivePage == null ? -1 : Pages.IndexOf(ActivePage);
            for (int i = 0; i < Pages.Count; i++)
                Pages[i].IsTabSplitterVisible = (activePageIdx != i && activePageIdx != i + 1);
        }

        private LauncherPage _activePage = null;
    }
}
