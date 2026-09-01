using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class OpenLocalRepository : Popup
    {
        [Required(ErrorMessage = "Repository folder is required")]
        [CustomValidation(typeof(OpenLocalRepository), nameof(ValidateRepoPath))]
        public string RepoPath
        {
            get => _repoPath;
            set => SetProperty(ref _repoPath, value, true);
        }

        public List<TargetGroup> Groups
        {
            get;
        }

        public int SelectedGroupIndex
        {
            get => _selectedGroupIndex;
            set => SetProperty(ref _selectedGroupIndex, value);
        }

        public int Bookmark
        {
            get => _bookmark;
            set => SetProperty(ref _bookmark, value);
        }

        public OpenLocalRepository(string pageId, RepositoryNode group)
        {
            _pageId = pageId;

            Groups = new List<TargetGroup>();
            Groups.Add(new TargetGroup(string.Empty, "Auto", "Based-on Default Clone Dir"));
            Groups.Add(new TargetGroup(string.Empty, "No Group", "Uncategorized"));
            CollectGroups(Groups, Preferences.Instance.RepositoryNodes);
        }

        public static ValidationResult ValidateRepoPath(string folder, ValidationContext _)
        {
            if (!Directory.Exists(folder))
                return new ValidationResult("Given path can NOT be found");
            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            RepositoryNode parent = null;
            if (_selectedGroupIndex == 0) // Auto (Based-on Default Clone Dir)
            {
                var activeWorkspace = Preferences.Instance.GetActiveWorkspace();
                var defaultCloneDir = activeWorkspace?.DefaultCloneDir;
                if (string.IsNullOrEmpty(defaultCloneDir))
                    defaultCloneDir = Preferences.Instance.GitDefaultCloneDir;

                if (!string.IsNullOrEmpty(defaultCloneDir))
                {
                    var normalizedParentFolder = new DirectoryInfo(RepoPath).Parent!.FullName.Replace('\\', '/').TrimEnd('/') + "/";
                    var normalizedDefaultCloneDir = defaultCloneDir.Replace('\\', '/').TrimEnd('/') + "/";
                    if (normalizedParentFolder.Length > normalizedDefaultCloneDir.Length &&
                        normalizedParentFolder.StartsWith(normalizedDefaultCloneDir, StringComparison.Ordinal))
                    {
                        var relativePath = normalizedParentFolder.Substring(normalizedDefaultCloneDir.Length);
                        parent = Preferences.Instance.FindOrCreateGroupRecursive(relativePath.TrimEnd('/'));
                    }
                }
            }
            else if (_selectedGroupIndex > 1 && _selectedGroupIndex < Groups.Count) // Existing group
            {
                parent = Preferences.Instance.FindNode(Groups[_selectedGroupIndex].Id);
            }

            var isBare = await new Commands.IsBareRepository(_repoPath).GetResultAsync();
            var repoRoot = _repoPath;
            if (!isBare)
            {
                var test = await new Commands.QueryRepositoryRootPath(_repoPath).GetResultAsync();
                if (test.IsSuccess && !string.IsNullOrWhiteSpace(test.StdOut))
                {
                    repoRoot = test.StdOut.Trim();
                }
                else
                {
                    var launcher = App.GetLauncher();
                    foreach (var page in launcher.Pages)
                    {
                        if (page.Node.Id.Equals(_pageId, StringComparison.Ordinal))
                        {
                            page.Popup = new Init(page.Node.Id, _repoPath, parent, _bookmark, test.StdErr);
                            break;
                        }
                    }

                    return false;
                }
            }

            var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(repoRoot, parent, true);
            node.Bookmark = _bookmark;
            await node.UpdateStatusAsync(false, null);
            Welcome.Instance.Refresh();
            node.Open();
            return true;
        }

        private void CollectGroups(List<TargetGroup> outs, List<RepositoryNode> collections, string prefix = null)
        {
            foreach (var node in collections)
            {
                if (!node.IsRepository)
                {
                    var displayName = prefix != null ? $"{prefix}/{node.Name}" : node.Name;
                    outs.Add(new(node.Id, displayName, string.Empty));
                    CollectGroups(outs, node.SubNodes, displayName);
                }
            }
        }

        private string _pageId = string.Empty;
        private string _repoPath = string.Empty;
        private int _selectedGroupIndex = 0;
        private int _bookmark = 0;
    }
}
