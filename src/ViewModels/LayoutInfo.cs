using System;
using System.Text.Json;
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

        public WindowState LauncherWindowState
        {
            get;
            set;
        } = WindowState.Normal;

        public GridLength RepositorySidebarWidth
        {
            get => _repositorySidebarWidth;
            set => SetProperty(ref _repositorySidebarWidth, value);
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

        private GridLength _repositorySidebarWidth = new GridLength(250, GridUnitType.Pixel);
        private GridLength _workingCopyLeftWidth = new GridLength(300, GridUnitType.Pixel);
        private GridLength _stashesLeftWidth = new GridLength(300, GridUnitType.Pixel);
        private GridLength _commitDetailChangesLeftWidth = new GridLength(256, GridUnitType.Pixel);
        private GridLength _commitDetailFilesLeftWidth = new GridLength(256, GridUnitType.Pixel);
    }
}
