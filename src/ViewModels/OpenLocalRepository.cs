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

        public List<RepositoryNode> Groups
        {
            get;
        }

        public RepositoryNode Group
        {
            get => _group;
            set => SetProperty(ref _group, value);
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

        public OpenLocalRepository(string pageId, RepositoryNode group)
        {
            _pageId = pageId;
            _group = group;

            Groups = new List<RepositoryNode>();
            CollectGroups(Groups, Preferences.Instance.RepositoryNodes);
            if (Groups.Count > 0 && _group == null)
                Group = Groups[0];

            Bookmarks = new List<int>();
            for (var i = 0; i < Models.Bookmarks.Brushes.Length; i++)
                Bookmarks.Add(i);
        }

        public static ValidationResult ValidateRepoPath(string folder, ValidationContext _)
        {
            if (!Directory.Exists(folder))
                return new ValidationResult("Given path can NOT be found");
            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
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
                            page.Popup = new Init(page.Node.Id, _repoPath, _group, test.StdErr);
                            break;
                        }
                    }

                    return false;
                }
            }

            var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(repoRoot, _group, true);
            node.Bookmark = _bookmark;
            await node.UpdateStatusAsync(false, null);
            Welcome.Instance.Refresh();
            node.Open();
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
        private string _repoPath = string.Empty;
        private RepositoryNode _group = null;
        private int _bookmark = 0;
    }
}
