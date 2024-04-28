using System;

using Avalonia.Collections;
using Avalonia.Controls;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Welcome : ObservableObject
    {
        public bool IsClearSearchVisible
        {
            get => !string.IsNullOrEmpty(_searchFilter);
        }

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
                {
                    Referesh();
                    OnPropertyChanged(nameof(IsClearSearchVisible));
                }
            }
        }

        public void InitRepository(string path)
        {
            if (!Preference.Instance.IsGitConfigured)
            {
                App.RaiseException(PopupHost.Active.GetId(), App.Text("NotConfigured"));
                return;
            }

            if (PopupHost.CanCreatePopup())
            {
                PopupHost.ShowPopup(new Init(path));
            }
        }

        public void Clone(object param)
        {
            var launcher = param as Launcher;
            var page = launcher.ActivePage;

            if (!Preference.Instance.IsGitConfigured)
            {
                App.RaiseException(page.GetId(), App.Text("NotConfigured"));
                return;
            }

            if (PopupHost.CanCreatePopup())
            {
                PopupHost.ShowPopup(new Clone(launcher, page));
            }
        }

        public void OpenTerminal()
        {
            if (!Preference.Instance.IsGitConfigured)
            {
                App.RaiseException(PopupHost.Active.GetId(), App.Text("NotConfigured"));
            }
            else
            {
                Native.OS.OpenTerminal(null);
            }
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
            Preference.MoveNode(from, to);
        }

        public ContextMenu CreateContextMenu(RepositoryNode node)
        {
            var menu = new ContextMenu();
            var hasRepo = Preference.FindRepository(node.Id) != null;

            var edit = new MenuItem();
            edit.Header = App.Text("Welcome.Edit");
            edit.Icon = App.CreateMenuIcon("Icons.Edit");
            edit.IsEnabled = !node.IsRepository || hasRepo;
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
                explore.Icon = App.CreateMenuIcon("Icons.Folder.Open");
                explore.IsEnabled = hasRepo;
                explore.Click += (_, e) =>
                {
                    node.OpenInFileManager();
                    e.Handled = true;
                };
                menu.Items.Add(explore);

                var terminal = new MenuItem();
                terminal.Header = App.Text("Repository.Terminal");
                terminal.Icon = App.CreateMenuIcon("Icons.Terminal");
                terminal.IsEnabled = hasRepo;
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

        private void Referesh()
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

        private string _searchFilter = string.Empty;
    }
}
