using System.Collections.Generic;
using System.Text.Json.Serialization;

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
                    Brush = new SolidColorBrush(value);
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

        [JsonIgnore]
        public IBrush Brush
        {
            get => _brush;
            private set => SetProperty(ref _brush, value);
        }

        public void AddRepository(string repo)
        {
            if (!Repositories.Contains(repo))
                Repositories.Add(repo);
        }

        private string _name = string.Empty;
        private uint _color = 0;
        private bool _isActive = false;
        private IBrush _brush = null;
    }
}
