using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class WorkspaceSwitcher : ObservableObject
    {
        public List<Workspace> VisibleWorkspaces
        {
            get => _visibleWorkspaces;
            private set => SetProperty(ref _visibleWorkspaces, value);
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    UpdateVisibleWorkspaces();
            }
        }

        public Workspace SelectedWorkspace
        {
            get => _selectedWorkspace;
            set => SetProperty(ref _selectedWorkspace, value);
        }

        public WorkspaceSwitcher(Launcher launcher)
        {
            _launcher = launcher;
            UpdateVisibleWorkspaces();
        }

        public void ClearFilter()
        {
            SearchFilter = string.Empty;
        }

        public void Switch()
        {
            if (_selectedWorkspace is { })
                _launcher.SwitchWorkspace(_selectedWorkspace);

            _launcher.CancelSwitcher();
        }

        private void UpdateVisibleWorkspaces()
        {
            var visible = new List<Workspace>();
            if (string.IsNullOrEmpty(_searchFilter))
            {
                visible.AddRange(Preferences.Instance.Workspaces);
            }
            else
            {
                foreach (var workspace in Preferences.Instance.Workspaces)
                {
                    if (workspace.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(workspace);
                }
            }

            VisibleWorkspaces = visible;
            SelectedWorkspace = visible.Count == 0 ? null : visible[0];
        }

        private Launcher _launcher = null;
        private List<Workspace> _visibleWorkspaces = null;
        private string _searchFilter = string.Empty;
        private Workspace _selectedWorkspace = null;
    }
}
