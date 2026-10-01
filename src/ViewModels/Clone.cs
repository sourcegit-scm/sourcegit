using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public record TargetGroup(string Id, string Name, string Description);

    public class Clone : Popup
    {
        [Required(ErrorMessage = "Remote URL is required")]
        [CustomValidation(typeof(Clone), nameof(ValidateRemote))]
        public string Remote
        {
            get => _remote;
            set
            {
                if (SetProperty(ref _remote, value, true))
                    UseSSH = Models.Remote.IsSSH(value);
            }
        }

        public bool UseSSH
        {
            get => _useSSH;
            set => SetProperty(ref _useSSH, value);
        }

        public string SSHKey
        {
            get => _sshKey;
            set => SetProperty(ref _sshKey, value);
        }

        [Required(ErrorMessage = "Parent folder is required")]
        [CustomValidation(typeof(Clone), nameof(ValidateParentFolder))]
        public string ParentFolder
        {
            get => _parentFolder;
            set => SetProperty(ref _parentFolder, value, true);
        }

        public string Local
        {
            get => _local;
            set => SetProperty(ref _local, value);
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

        public bool CanAutoSelectGroup
        {
            get => _canAutoSelectGroup;
            private set => SetProperty(ref _canAutoSelectGroup, value);
        }

        public int Bookmark
        {
            get => _bookmark;
            set => SetProperty(ref _bookmark, value);
        }

        public string ExtraArgs
        {
            get => _extraArgs;
            set => SetProperty(ref _extraArgs, value);
        }

        public bool InitAndUpdateSubmodules
        {
            get;
            set;
        } = true;

        public Clone(string pageId)
        {
            _pageId = pageId;
            CanTerminate = true;

            Groups = new List<TargetGroup>();
            Groups.Add(new TargetGroup(string.Empty, "Auto", "Based-on Default Clone Dir"));
            Groups.Add(new TargetGroup(string.Empty, "No Group", "Uncategorized"));
            CollectGroups(Groups, Preferences.Instance.RepositoryNodes);

            var activeWorkspace = Preferences.Instance.GetActiveWorkspace();
            _defaultCloneDir = activeWorkspace?.DefaultCloneDir;
            if (string.IsNullOrEmpty(_defaultCloneDir))
                _defaultCloneDir = Preferences.Instance.GitDefaultCloneDir;

            ParentFolder = _defaultCloneDir;
        }

        public static ValidationResult ValidateRemote(string remote, ValidationContext _)
        {
            if (!Models.Remote.IsValidURL(remote))
                return new ValidationResult("Invalid remote repository URL format");
            return ValidationResult.Success;
        }

        public static ValidationResult ValidateParentFolder(string folder, ValidationContext _)
        {
            if (!Directory.Exists(folder))
                return new ValidationResult("Given path can NOT be found");
            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            ProgressDescription = "Clone ...";

            var log = new CommandLog("Clone");
            Use(log);

            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;

            var succ = await new Commands.Clone(_pageId, _parentFolder, _remote, _local, _useSSH ? _sshKey : "", _extraArgs)
                .WithCancellation(token)
                .Use(log)
                .ExecAsync();
            if (!succ || token.IsCancellationRequested)
                return false;

            var path = _parentFolder;
            if (!string.IsNullOrEmpty(_local))
            {
                path = Path.GetFullPath(Path.Combine(path, _local));
            }
            else
            {
                var name = Path.GetFileName(_remote)!;
                if (name.EndsWith(".git", StringComparison.Ordinal))
                    name = name.Substring(0, name.Length - 4);
                else if (name.EndsWith(".bundle", StringComparison.Ordinal))
                    name = name.Substring(0, name.Length - 7);

                path = Path.GetFullPath(Path.Combine(path, name));
            }

            if (!Directory.Exists(path))
            {
                Models.Notification.Send(_pageId, $"Folder '{path}' can NOT be found", true);
                return false;
            }

            if (_useSSH && !string.IsNullOrEmpty(_sshKey))
            {
                await new Commands.Config(path)
                    .Use(log)
                    .SetAsync("remote.origin.sshkey", _sshKey);
            }

            if (InitAndUpdateSubmodules && !token.IsCancellationRequested)
            {
                var submodules = await new Commands.QueryUpdatableSubmodules(path, true).GetResultAsync();
                if (submodules.Count > 0)
                    await new Commands.Submodule(path)
                        .WithCancellation(token)
                        .Use(log)
                        .UpdateAsync(submodules, true, true, false);
            }

            log.Complete();

            RepositoryNode parent = null;
            if (_selectedGroupIndex == 0) // Auto (Based-on Default Clone Dir)
            {
                if (!string.IsNullOrEmpty(_defaultCloneDir))
                {
                    var normalizedDefaultCloneDir = _defaultCloneDir.Replace('\\', '/').TrimEnd('/') + "/";
                    var normalizedParentFolder = _parentFolder.Replace('\\', '/').TrimEnd('/') + "/";
                    if (normalizedParentFolder.Length > normalizedDefaultCloneDir.Length &&
                        normalizedParentFolder.StartsWith(normalizedDefaultCloneDir, StringComparison.Ordinal))
                    {
                        var relativePath = normalizedParentFolder.Substring(normalizedDefaultCloneDir.Length);
                        parent = Preferences.Instance.FindOrCreateGroupRecursive(relativePath.TrimEnd('/'));
                    }
                }
            }
            else if (_selectedGroupIndex > 0 && _selectedGroupIndex < Groups.Count) // Existing group
            {
                parent = Preferences.Instance.FindNode(Groups[_selectedGroupIndex].Id);
            }

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

            _cancellation = null;
            return true;
        }

        public override void Terminate()
        {
            // Just fire cancel event and UI will auto wait the `Sure` complete
            var _ = _cancellation?.CancelAsync();
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
        private string _remote = string.Empty;
        private bool _useSSH = false;
        private string _sshKey = string.Empty;
        private string _parentFolder = string.Empty;
        private string _defaultCloneDir = string.Empty;
        private string _local = string.Empty;
        private string _extraArgs = string.Empty;
        private int _selectedGroupIndex = 0;
        private bool _canAutoSelectGroup = false;
        private int _bookmark = 0;
        private CancellationTokenSource _cancellation = null;
    }
}
