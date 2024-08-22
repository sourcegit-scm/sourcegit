using System;
using System.Collections.Generic;

using Avalonia.Collections;
using Avalonia.Controls;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Welcome : ObservableObject
    {
        public static Welcome Instance => _instance;

        public AvaloniaList<RepositoryNode> RepositoryNodes
        {
            get => Preference.Instance.RepositoryNodes;
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    Refresh();
            }
        }

        public void InitRepository(string path, RepositoryNode parent)
        {
            if (!Preference.Instance.IsGitConfigured())
            {
                App.RaiseException(PopupHost.Active.GetId(), App.Text("NotConfigured"));
                return;
            }

            if (PopupHost.CanCreatePopup())
            {
                PopupHost.ShowPopup(new Init(path, parent));
            }
        }

        public void Clone()
        {
            if (!Preference.Instance.IsGitConfigured())
            {
                App.RaiseException(string.Empty, App.Text("NotConfigured"));
                return;
            }

            if (PopupHost.CanCreatePopup())
                PopupHost.ShowPopup(new Clone());
        }

        public void OpenTerminal()
        {
            if (!Preference.Instance.IsGitConfigured())
                App.RaiseException(PopupHost.Active.GetId(), App.Text("NotConfigured"));
            else
                Native.OS.OpenTerminal(null);
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        public void AddRootNode()
        {
            if (PopupHost.CanCreatePopup())
                PopupHost.ShowPopup(new CreateGroup(null));
        }

        public void MoveNode(RepositoryNode from, RepositoryNode to)
        {
            Preference.Instance.MoveNode(from, to);
        }

        public RepositoryNode GetPrevVisible(RepositoryNode node)
        {
            var visibleRows = new List<RepositoryNode>();
            CollectVisibleRows(visibleRows, RepositoryNodes);

            var idx = visibleRows.IndexOf(node);
            if (idx <= 1)
                return null;

            return visibleRows[idx - 1];
        }

        public RepositoryNode GetNextVisible(RepositoryNode node)
        {
            var visibleRows = new List<RepositoryNode>();
            CollectVisibleRows(visibleRows, RepositoryNodes);

            var idx = visibleRows.IndexOf(node);
            if (idx < 0 || idx >= visibleRows.Count - 1)
                return null;

            return visibleRows[idx + 1];
        }

        public ContextMenu CreateContextMenu(RepositoryNode node)
        {
            var menu = new ContextMenu();

            if (!node.IsRepository && node.SubNodes.Count > 0)
            {
                var openAll = new MenuItem();
                openAll.Header = App.Text("Welcome.OpenAllInNode");
                openAll.Icon = App.CreateMenuIcon("Icons.Folder.Open");
                openAll.Click += (_, e) =>
                {
                    OpenAllInNode(App.GetLauncer(), node);
                    e.Handled = true;
                };

                menu.Items.Add(openAll);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var edit = new MenuItem();
            edit.Header = App.Text("Welcome.Edit");
            edit.Icon = App.CreateMenuIcon("Icons.Edit");
            edit.Click += (_, e) =>
            {
                node.Edit();
                e.Handled = true;
            };
            menu.Items.Add(edit);

            if (node.IsRepository)
            {
                var explore = new MenuItem();
                explore.Header = App.Text("Repository.Explore");
                explore.Icon = App.CreateMenuIcon("Icons.Explore");
                explore.Click += (_, e) =>
                {
                    node.OpenInFileManager();
                    e.Handled = true;
                };
                menu.Items.Add(explore);

                var terminal = new MenuItem();
                terminal.Header = App.Text("Repository.Terminal");
                terminal.Icon = App.CreateMenuIcon("Icons.Terminal");
                terminal.Click += (_, e) =>
                {
                    node.OpenTerminal();
                    e.Handled = true;
                };
                menu.Items.Add(terminal);
            }
            else
            {
                var addSubFolder = new MenuItem();
                addSubFolder.Header = App.Text("Welcome.AddSubFolder");
                addSubFolder.Icon = App.CreateMenuIcon("Icons.Folder.Add");
                addSubFolder.Click += (_, e) =>
                {
                    node.AddSubFolder();
                    e.Handled = true;
                };
                menu.Items.Add(addSubFolder);
            }

            var delete = new MenuItem();
            delete.Header = App.Text("Welcome.Delete");
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                node.Delete();
                e.Handled = true;
            };
            menu.Items.Add(delete);

            return menu;
        }

        private void Refresh()
        {
            if (string.IsNullOrWhiteSpace(_searchFilter))
            {
                foreach (var node in RepositoryNodes)
                    ResetVisibility(node);
            }
            else
            {
                foreach (var node in RepositoryNodes)
                    SetVisibilityBySearch(node);
            }
        }

        private void ResetVisibility(RepositoryNode node)
        {
            node.IsVisible = true;
            foreach (var subNode in node.SubNodes)
                ResetVisibility(subNode);
        }

        private void SetVisibilityBySearch(RepositoryNode node)
        {
            if (!node.IsRepository)
            {
                if (node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                {
                    node.IsVisible = true;
                    foreach (var subNode in node.SubNodes)
                        ResetVisibility(subNode);
                }
                else
                {
                    bool hasVisibleSubNode = false;
                    foreach (var subNode in node.SubNodes)
                    {
                        SetVisibilityBySearch(subNode);
                        hasVisibleSubNode |= subNode.IsVisible;
                    }
                    node.IsVisible = hasVisibleSubNode;
                }
            }
            else
            {
                node.IsVisible = node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void OpenAllInNode(Launcher launcher, RepositoryNode node)
        {
            foreach (var subNode in node.SubNodes)
            {
                if (subNode.IsRepository)
                    launcher.OpenRepositoryInTab(subNode, null);
                else if (subNode.SubNodes.Count > 0)
                    OpenAllInNode(launcher, subNode);
            }
        }

        private void CollectVisibleRows(List<RepositoryNode> visible, AvaloniaList<RepositoryNode> collection)
        {
            foreach (var node in collection)
            {
                if (node.IsVisible)
                {
                    visible.Add(node);

                    if (!node.IsRepository)
                        CollectVisibleRows(visible, node.SubNodes);
                }
            }
        }

        private static Welcome _instance = new Welcome();
        private string _searchFilter = string.Empty;
    }
}
