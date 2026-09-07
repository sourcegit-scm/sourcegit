using System.Text.Json.Serialization;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class LayoutInfo : ObservableObject
    {
        public double LauncherWidth
        {
            get;
            set;
        } = 1280;

        public double LauncherHeight
        {
            get;
            set;
        } = 720;

        public int LauncherPositionX
        {
            get;
            set;
        } = int.MinValue;

        public int LauncherPositionY
        {
            get;
            set;
        } = int.MinValue;

        public WindowState LauncherWindowState
        {
            get;
            set;
        } = WindowState.Normal;

        public bool RepositorySidebarCollapsed
        {
            get => _repositorySidebarCollapsed;
            set
            {
                if (SetProperty(ref _repositorySidebarCollapsed, value))
                {
                    OnPropertyChanged(nameof(RepositorySidebarMinWidth));
                    OnPropertyChanged(nameof(RepositorySidebarDisplayWidth));
                }
            }
        }

        public GridLength RepositorySidebarWidth
        {
            get;
            set;
        }

        [JsonIgnore]
        public double RepositorySidebarMinWidth
        {
            get => _repositorySidebarCollapsed ? 48 : 200;
        }

        [JsonIgnore]
        public GridLength RepositorySidebarDisplayWidth
        {
            get => _repositorySidebarCollapsed ? new GridLength(48, GridUnitType.Pixel) : RepositorySidebarWidth;
            set
            {
                if (!_repositorySidebarCollapsed)
                {
                    RepositorySidebarWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        public GridLength WorkingCopyLeftWidth
        {
            get => _workingCopyLeftWidth;
            set => SetProperty(ref _workingCopyLeftWidth, value);
        }

        public GridLength StashesLeftWidth
        {
            get => _stashesLeftWidth;
            set => SetProperty(ref _stashesLeftWidth, value);
        }

        public GridLength CommitDetailChangesLeftWidth
        {
            get => _commitDetailChangesLeftWidth;
            set => SetProperty(ref _commitDetailChangesLeftWidth, value);
        }

        public GridLength CommitDetailFilesLeftWidth
        {
            get => _commitDetailFilesLeftWidth;
            set => SetProperty(ref _commitDetailFilesLeftWidth, value);
        }

        private bool _repositorySidebarCollapsed = false;
        private GridLength _workingCopyLeftWidth = new GridLength(300, GridUnitType.Pixel);
        private GridLength _stashesLeftWidth = new GridLength(300, GridUnitType.Pixel);
        private GridLength _commitDetailChangesLeftWidth = new GridLength(256, GridUnitType.Pixel);
        private GridLength _commitDetailFilesLeftWidth = new GridLength(256, GridUnitType.Pixel);
    }
}
