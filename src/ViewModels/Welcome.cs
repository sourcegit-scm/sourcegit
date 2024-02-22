using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace SourceGit.ViewModels {
    public class Welcome : ObservableObject {
        public bool IsClearSearchVisible {
            get => !string.IsNullOrEmpty(_searchFilter);
        }

        public AvaloniaList<RepositoryNode> RepositoryNodes {
            get => Preference.Instance.RepositoryNodes;
        }

        public string SearchFilter {
            get => _searchFilter;
            set {
                if (SetProperty(ref _searchFilter, value)) {
                    Referesh();
                    OnPropertyChanged(nameof(IsClearSearchVisible));
                }
            }
        }

        public void InitRepository(string path) {
            if (!Preference.Instance.IsGitConfigured) {
                App.RaiseException(PopupHost.Active.GetId(), App.Text("NotConfigured"));
                return;
            }

            if (PopupHost.CanCreatePopup()) {
                PopupHost.ShowPopup(new Init(path));
            }
        }

        public void Clone(object param) {
            var launcher = param as Launcher;
            var page = launcher.ActivePage;

            if (!Preference.Instance.IsGitConfigured) {
                App.RaiseException(page.GetId(), App.Text("NotConfigured"));
                return;
            }

            if (PopupHost.CanCreatePopup()) {
                PopupHost.ShowPopup(new Clone(launcher, page));
            }
        }

        public void OpenTerminal() {
            if (!Preference.Instance.IsGitConfigured) {
                App.RaiseException(PopupHost.Active.GetId(), App.Text("NotConfigured"));
            } else {
                Native.OS.OpenTerminal(null);
            }            
        }

        public void ClearSearchFilter() {
            SearchFilter = string.Empty;
        }

        public void AddFolder() {
            if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CreateGroup(null));
        }

        public void MoveNode(RepositoryNode from, RepositoryNode to) {
            Preference.MoveNode(from, to);
        }

        private void Referesh() {
            if (string.IsNullOrWhiteSpace(_searchFilter)) {
                foreach (var node in RepositoryNodes) ResetVisibility(node);
            } else {
                foreach (var node in RepositoryNodes) SetVisibilityBySearch(node);
            }
        }

        private void ResetVisibility(RepositoryNode node) {
            node.IsVisible = true;
            foreach (var subNode in node.SubNodes) ResetVisibility(subNode);
        }

        private void SetVisibilityBySearch(RepositoryNode node) {
            if (!node.IsRepository) {
                if (node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)) {
                    node.IsVisible = true;
                    foreach (var subNode in node.SubNodes) ResetVisibility(subNode);
                } else {
                    bool hasVisibleSubNode = false;
                    foreach (var subNode in node.SubNodes) {
                        SetVisibilityBySearch(subNode);
                        hasVisibleSubNode |= subNode.IsVisible;
                    }
                    node.IsVisible = hasVisibleSubNode;
                }
            } else {
                node.IsVisible = node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase);
            }
        }

        private string _searchFilter = string.Empty;
    }
}
