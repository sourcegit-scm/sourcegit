using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Workspace : ObservableObject
    {
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public uint Color
        {
            get => _color;
            set
            {
                if (SetProperty(ref _color, value))
                    OnPropertyChanged(nameof(Brush));
            }
        }

        public List<string> Repositories
        {
            get;
            set;
        } = new List<string>();

        public int ActiveIdx
        {
            get;
            set;
        } = 0;

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool RestoreOnStartup
        {
            get => _restoreOnStartup;
            set => SetProperty(ref _restoreOnStartup, value);
        }

        public IBrush Brush
        {
            get => new SolidColorBrush(_color);
        }

        public void AddRepository(string repo)
        {
            if (!Repositories.Contains(repo))
                Repositories.Add(repo);
        }

        private string _name = string.Empty;
        private uint _color = 4278221015;
        private bool _isActive = false;
        private bool _restoreOnStartup = true;
    }
}
