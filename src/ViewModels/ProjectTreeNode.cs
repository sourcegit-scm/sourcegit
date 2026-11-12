using System.IO;

using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     Node of the left-side project tree. Three kinds of nodes exist:
    ///     project (container), directory (only kept when it leads to repositories) and
    ///     repository (double-click opens a regular repository tab).
    /// </summary>
    public class ProjectTreeNode : ObservableObject
    {
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string FullPath
        {
            get => _fullPath;
            set => SetProperty(ref _fullPath, value);
        }

        public bool IsProject
        {
            get;
            set;
        } = false;

        public bool IsRepository
        {
            get => _isRepository;
            set => SetProperty(ref _isRepository, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public ProjectTreeNode Parent
        {
            get;
            set;
        } = null;

        public Models.Project Project
        {
            get;
            set;
        } = null;

        public AvaloniaList<ProjectTreeNode> Children
        {
            get;
        } = new AvaloniaList<ProjectTreeNode>();

        public bool IsMissing
        {
            get => _isRepository && !Directory.Exists(_fullPath);
        }

        /// <summary>
        ///     Opens the repository in a regular tab. The upstream implementation already
        ///     deduplicates by path, so double-clicking an already opened repository just
        ///     activates its tab instead of creating a second one.
        /// </summary>
        public void Open()
        {
            if (_isRepository && Directory.Exists(_fullPath))
                App.GetLauncher().OpenRepositoryInTab(_fullPath, null);
        }

        private string _name = string.Empty;
        private string _fullPath = string.Empty;
        private bool _isRepository = false;
        private bool _isExpanded = false;
    }
}
