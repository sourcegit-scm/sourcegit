using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;

namespace SourceGit.ViewModels
{
    public class InitGit : Popup
    {
        [Required(ErrorMessage = "Parent folder is required")]
        [CustomValidation(typeof(InitGit), nameof(ValidateParentFolder))]
        public string ParentFolder
        {
            get => _parentFolder;
            set => SetProperty(ref _parentFolder, value, true);
        }

        public string ProjectName
        {
            get => null;
            set => SetProperty(ref _projectName, value);
        }

        public List<RepositoryNode> Groups
        {
            get;
        }

        public RepositoryNode SelectedGroup
        {
            get => _selectedGroup;
            set => SetProperty(ref _selectedGroup, value);
        }

        public List<int> Bookmarks
        {
            get;
        }

        public int Bookmark
        {
            get => _bookmark;
            set => SetProperty(ref _bookmark, value);
        }

        public string InitialBranch
        {
            get => null;
            set => SetProperty(ref _initialBranch, value);
        }

        public bool BareRepo
        {
            get;
            set;
        } = false;

        public InitGit(string pageId)
        {
            _pageId = pageId;

            Groups = new List<RepositoryNode>();
            Groups.Add(new RepositoryNode { Name = "No Group (Uncategorized)", Id = string.Empty });
            SelectedGroup = Groups[0];
            CollectGroups(Groups, Preferences.Instance.RepositoryNodes);

            Bookmarks = new List<int>();
            for (var i = 0; i < Models.Bookmarks.Brushes.Length; i++)
                Bookmarks.Add(i);

            var activeWorkspace = Preferences.Instance.GetActiveWorkspace();
            _parentFolder = activeWorkspace?.DefaultCloneDir;
            if (string.IsNullOrEmpty(ParentFolder))
                _parentFolder = Preferences.Instance.GitDefaultCloneDir;
        }

        public static ValidationResult ValidateParentFolder(string folder, ValidationContext _)
        {
            if (!Directory.Exists(folder))
                return new ValidationResult("Given path can NOT be found");
            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            ProgressDescription = "Init ...";

            var log = new CommandLog("Init");
            Use(log);

            var succ = await new Commands.InitGit(_pageId, _parentFolder, _projectName, _initialBranch, BareRepo)
                .Use(log)
                .ExecAsync();
            if (!succ)
                return false;

            var path = _parentFolder;
            if (!string.IsNullOrEmpty(_projectName))
            {
                path = Path.GetFullPath(Path.Combine(path, _projectName));
            }
            else
            {
                path = Path.GetFullPath(Path.Combine(path, "new_git_project"));
            }

            if (!Directory.Exists(path))
            {
                Models.Notification.Send(_pageId, $"Folder '{path}' can NOT be found", true);
                return false;
            }

            log.Complete();

            var parent = _selectedGroup is { Id: not "" } ? _selectedGroup : null;
            var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(path, parent, true);
            node.Bookmark = _bookmark;
            await node.UpdateStatusAsync(false, null);

            var launcher = App.GetLauncher();
            LauncherPage page = null;
            foreach (var one in launcher.Pages)
            {
                if (one.Node.Id == _pageId)
                {
                    page = one;
                    break;
                }
            }

            Welcome.Instance.Refresh();
            launcher.OpenRepositoryInTab(node, page);
            return true;
        }

        private void CollectGroups(List<RepositoryNode> outs, List<RepositoryNode> collections)
        {
            foreach (var node in collections)
            {
                if (!node.IsRepository)
                {
                    outs.Add(node);
                    CollectGroups(outs, node.SubNodes);
                }
            }
        }

        private string _pageId = string.Empty;
        private string _parentFolder = string.Empty;
        private string _projectName = App.Text("InitGit.ProjectName.Placeholder");
        private string _initialBranch = App.Text("InitGit.InitialBranch.Placeholder");
        private RepositoryNode _selectedGroup = null;
        private int _bookmark = 0;
    }
}
