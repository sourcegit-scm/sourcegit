using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Threading;

namespace SourceGit.ViewModels
{
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

            var activeWorkspace = Preferences.Instance.GetActiveWorkspace();
            _parentFolder = activeWorkspace?.DefaultCloneDir;
            if (string.IsNullOrEmpty(ParentFolder))
                _parentFolder = Preferences.Instance.GitDefaultCloneDir;

            Task.Run(async () =>
            {
                try
                {
                    var text = await App.GetClipboardTextAsync();
                    if (Models.Remote.IsValidURL(text))
                        Dispatcher.UIThread.Invoke(() => Remote = text);
                }
                catch
                {
                    // ignore
                }
            });
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

        public override Task<bool> Sure()
        {
            ProgressDescription = "Clone ...";

            // Create a temp log.
            var log = new CommandLog("Clone");
            Use(log);

            return Task.Run(() =>
            {
                var cmd = new Commands.Clone(_pageId, _parentFolder, _remote, _local, _useSSH ? _sshKey : "", _extraArgs).Use(log);
                if (!cmd.Exec())
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
                    path = Path.GetFullPath(Path.Combine(path, name));
                }

                if (!Directory.Exists(path))
                {
                    CallUIThread(() =>
                    {
                        App.RaiseException(_pageId, $"Folder '{path}' can NOT be found");
                    });
                    return false;
                }

                if (_useSSH && !string.IsNullOrEmpty(_sshKey))
                {
                    var config = new Commands.Config(path);
                    config.Set("remote.origin.sshkey", _sshKey);
                }

                // individually update submodule (if any)
                if (InitAndUpdateSubmodules)
                {
                    var submoduleList = new Commands.QuerySubmodules(path).Result();
                    foreach (var submodule in submoduleList)
                        new Commands.Submodule(path).Use(log).Update(submodule.Path, true, true, false);
                }

                log.Complete();

                CallUIThread(() =>
                {
                    var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(path, null, true);
                    var launcher = App.GetLauncer();
                    var page = null as LauncherPage;
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
                });

                return true;
            });
        }

        private string _pageId = string.Empty;
        private string _remote = string.Empty;
        private bool _useSSH = false;
        private string _sshKey = string.Empty;
        private string _parentFolder = string.Empty;
        private string _local = string.Empty;
        private string _extraArgs = string.Empty;
    }
}
