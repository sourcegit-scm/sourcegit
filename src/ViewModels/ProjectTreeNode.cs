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
            set
            {
                if (SetProperty(ref _isRepository, value))
                {
                    OnPropertyChanged(nameof(IsDirectory));
                    OnPropertyChanged(nameof(IsRepositoryDirty));
                    OnPropertyChanged(nameof(HasDirtyChildren));
                }
            }
        }

        /// <summary>
        ///     An intermediate directory: neither the project container nor a repository.
        ///     Used by the view to pick the folder icon instead of the repository icon.
        /// </summary>
        public bool IsDirectory
        {
            get => !IsProject && !_isRepository;
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
        ///     Working copy status of a repository node. Mirrors the state the repository tab
        ///     already computes, and is filled in by the background scan for repositories that
        ///     are not open. Directory and project nodes never carry a state of their own.
        /// </summary>
        public Models.DirtyState Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(IsRepositoryDirty));
                    OnPropertyChanged(nameof(HasPendingPullOrPush));
                    OnPropertyChanged(nameof(PendingText));
                }
            }
        }

        /// <summary>Repositories with local changes below this node (itself included).</summary>
        public int DirtyRepositories
        {
            get => _dirtyRepositories;
            set
            {
                if (SetProperty(ref _dirtyRepositories, value))
                {
                    OnPropertyChanged(nameof(HasDirtyChildren));
                    OnPropertyChanged(nameof(DirtyCountText));
                }
            }
        }

        /// <summary>
        ///     Number of changed files reported by the last status query. Only meaningful for repository
        ///     nodes; directory nodes show how many repositories below them are dirty instead.
        /// </summary>
        public int LocalChanges
        {
            get => _localChanges;
            set
            {
                if (SetProperty(ref _localChanges, value))
                    OnPropertyChanged(nameof(LocalChangesText));
            }
        }

        public string LocalChangesText
        {
            get => _localChanges > 0 ? $"{_localChanges} 处改动" : string.Empty;
        }

        public bool IsRepositoryDirty
        {
            get => _isRepository && _status != Models.DirtyState.None;
        }

        /// <summary>True when the repository has commits to push or pull.</summary>
        public bool HasPendingPullOrPush
        {
            get => _isRepository && _status.HasFlag(Models.DirtyState.HasPendingPullOrPush);
        }

        public string PendingText
        {
            get => HasPendingPullOrPush ? "待推拉" : string.Empty;
        }

        public bool HasDirtyChildren
        {
            get => !_isRepository && _dirtyRepositories > 0;
        }

        public string DirtyCountText
        {
            get => _dirtyRepositories > 0 ? $"{_dirtyRepositories} 个仓库有改动" : string.Empty;
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
        private Models.DirtyState _status = Models.DirtyState.None;
        private int _dirtyRepositories = 0;
        private int _localChanges = 0;
    }
}
