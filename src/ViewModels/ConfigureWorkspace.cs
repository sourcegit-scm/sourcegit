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
        }

        public Workspace Selected
        {
            get => _selected;
            set
            {
                if (SetProperty(ref _selected, value))
                    CanDeleteSelected = value is { IsActive: false };
            }
        }

        public bool CanDeleteSelected
        {
            get => _canDeleteSelected;
            private set => SetProperty(ref _canDeleteSelected, value);
        }

        public ConfigureWorkspace()
        {
            Workspaces = new(Preferences.Instance.Workspaces);
        }

        public void Add()
        {
            var workspace = new Workspace() { Name = $"Unnamed {DateTime.Now:yyyy-MM-dd HH:mm:ss}" };
            Preferences.Instance.Workspaces.Add(workspace);
            Workspaces.Add(workspace);
            Selected = workspace;
        }

        public void Delete()
        {
            if (_selected == null || _selected.IsActive)
                return;

            Preferences.Instance.Workspaces.Remove(_selected);
            Workspaces.Remove(_selected);
        }

        public void MoveSelectedUp()
        {
            if (_selected == null)
                return;

            var idx = Workspaces.IndexOf(_selected);
            if (idx == 0)
                return;

            Workspaces.Move(idx - 1, idx);

            Preferences.Instance.Workspaces.RemoveAt(idx);
            Preferences.Instance.Workspaces.Insert(idx - 1, _selected);
        }

        public void MoveSelectedDown()
        {
            if (_selected == null)
                return;

            var idx = Workspaces.IndexOf(_selected);
            if (idx == Workspaces.Count - 1)
                return;

            Workspaces.Move(idx + 1, idx);

            Preferences.Instance.Workspaces.RemoveAt(idx);
            Preferences.Instance.Workspaces.Insert(idx + 1, _selected);
        }

        private Workspace _selected = null;
        private bool _canDeleteSelected = false;
    }
}
