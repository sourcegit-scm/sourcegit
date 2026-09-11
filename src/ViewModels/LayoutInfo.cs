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

        public double RepositoryDashboardWidth
        {
            get;
            set;
        } = 250;

        public double RepositorySearchCommitWidth
        {
            get;
            set;
        } = 250;

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

        private GridLength _workingCopyLeftWidth = new GridLength(300, GridUnitType.Pixel);
        private GridLength _stashesLeftWidth = new GridLength(300, GridUnitType.Pixel);
        private GridLength _commitDetailChangesLeftWidth = new GridLength(256, GridUnitType.Pixel);
        private GridLength _commitDetailFilesLeftWidth = new GridLength(256, GridUnitType.Pixel);
    }
}
