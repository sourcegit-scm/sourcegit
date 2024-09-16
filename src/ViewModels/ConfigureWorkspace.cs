using System;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class ConfigureWorkspace : ObservableObject
    {
        public AvaloniaList<Workspace> Workspaces
        {
            get;
            private set;
        }

        public Workspace Selected
        {
            get => _selected;
            set
            {
                if (SetProperty(ref _selected, value))
                    CanDeleteSelected = value != null && !value.IsActive;
            }
        }

        public bool CanDeleteSelected
        {
            get => _canDeleteSelected;
            private set => SetProperty(ref _canDeleteSelected, value);
        }

        public ConfigureWorkspace()
        {
            Workspaces = new AvaloniaList<Workspace>();
            Workspaces.AddRange(Preference.Instance.Workspaces);
        }

        public void Add()
        {
            var workspace = new Workspace();
            workspace.Name = $"Unnamed {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            workspace.Color = 4278221015;

            Preference.Instance.Workspaces.Add(workspace);
            Workspaces.Add(workspace);
            Selected = workspace;
        }

        public void Delete()
        {
            if (_selected == null || _selected.IsActive)
                return;

            Preference.Instance.Workspaces.Remove(_selected);
            Workspaces.Remove(_selected);
        }

        private Workspace _selected = null;
        private bool _canDeleteSelected = false;
    }
}
