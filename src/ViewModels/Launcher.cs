using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace SourceGit.ViewModels {
    public class Launcher : ObservableObject {
        public AvaloniaList<LauncherPage> Pages {
            get;
            private set;
        }

        public LauncherPage ActivePage {
            get => _activePage;
            set {
                if (SetProperty(ref _activePage, value)) {
                    PopupHost.Active = value;
                }
            }
        }

        public Launcher() {
            Pages = new AvaloniaList<LauncherPage>();
            AddNewTab();

            if (Preference.Instance.RestoreTabs) {
                foreach (var id in Preference.Instance.OpenedTabs) {
                    var node = Preference.FindNode(id);
                    if (node == null) continue;

                    OpenRepositoryInTab(node, null);
                }
            }
        }

        public void Quit() {
            Preference.Instance.OpenedTabs.Clear();

            if (Preference.Instance.RestoreTabs) {
                foreach (var page in Pages) {
                    if (page.Node.IsRepository) Preference.Instance.OpenedTabs.Add(page.Node.Id);
                }
            }

            Preference.Save();
        }

        public void AddNewTab() {
            var page = new LauncherPage();
            Pages.Add(page);
            ActivePage = page;
        }

        public void MoveTab(LauncherPage from, LauncherPage to) {
            var fromIdx = Pages.IndexOf(from);
            var toIdx = Pages.IndexOf(to);
            Pages.Move(fromIdx, toIdx);
            ActivePage = from;
        }

        public void GotoNextTab() {
            if (Pages.Count == 1) return;

            var activeIdx = Pages.IndexOf(_activePage);
            var nextIdx = (activeIdx + 1) % Pages.Count;
            ActivePage = Pages[nextIdx];
        }

        public void CloseTab(object param) {
            if (Pages.Count == 1) {
                App.Quit();
                return;
            }

            LauncherPage page = param as LauncherPage;
            if (page == null) page = _activePage;

            var removeIdx = Pages.IndexOf(page);
            var activeIdx = Pages.IndexOf(_activePage);
            if (removeIdx == activeIdx) {
                if (removeIdx == Pages.Count - 1) {
                    ActivePage = Pages[removeIdx - 1];
                } else {
                    ActivePage = Pages[removeIdx + 1];
                }

                CloseRepositoryInTab(page);
                Pages.RemoveAt(removeIdx);
                OnPropertyChanged(nameof(Pages));
            } else if (removeIdx + 1 == activeIdx) {
                CloseRepositoryInTab(page);
                Pages.RemoveAt(removeIdx);
                OnPropertyChanged(nameof(Pages));
            } else {
                CloseRepositoryInTab(page);
                Pages.RemoveAt(removeIdx);
            }

            GC.Collect();
        }

        public void CloseOtherTabs(object param) {
            if (Pages.Count == 1) return;

            var page = param as LauncherPage;
            if (page == null) page = _activePage;

            ActivePage = page;

            foreach (var one in Pages) {
                if (one.Node.Id != page.Node.Id) CloseRepositoryInTab(one);
            }

            Pages = new AvaloniaList<LauncherPage> { page };
            OnPropertyChanged(nameof(Pages));

            GC.Collect();
        }

        public void CloseRightTabs(object param) {
            LauncherPage page = param as LauncherPage;
            if (page == null) page = _activePage;

            var endIdx = Pages.IndexOf(page);
            var activeIdx = Pages.IndexOf(_activePage);
            if (endIdx < activeIdx) {
                ActivePage = page;
            }

            for (var i = Pages.Count - 1; i > endIdx; i--) {
                CloseRepositoryInTab(Pages[i]);
                Pages.Remove(Pages[i]);
            }

            GC.Collect();
        }

        public void OpenRepositoryInTab(RepositoryNode node, LauncherPage page) {
            foreach (var one in Pages) {
                if (one.Node.Id == node.Id) {
                    ActivePage = one;
                    return;
                }
            }

            var repo = Preference.FindRepository(node.Id);
            if (repo == null || !Path.Exists(repo.FullPath)) {
                var ctx = page == null ? ActivePage.Node.Id : page.Node.Id;
                App.RaiseException(ctx, "Repository does NOT exists any more. Please remove it.");
                return;
            }

            repo.Open();
            Commands.AutoFetch.AddRepository(repo.FullPath);

            if (page == null) {
                if (ActivePage == null || ActivePage.Node.IsRepository) {
                    page = new LauncherPage(node, repo);
                    Pages.Add(page);
                } else {
                    page = ActivePage;
                    page.Node = node;
                    page.Data = repo;
                }
            } else {
                page.Node = node;
                page.Data = repo;
            }

            ActivePage = page;
        }

        private void CloseRepositoryInTab(LauncherPage page) {
            if (page.Data is Repository repo) {
                Commands.AutoFetch.RemoveRepository(repo.FullPath);
                repo.Close();
            }

            page.Data = null;
        }

        private LauncherPage _activePage = null;
    }
}
