using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace DevBoard.ViewModels
{
    public class AddWorktree : Popup
    {
        public string WorktreeName
        {
            get => _worktreeName;
            set
            {
                if (SetProperty(ref _worktreeName, value) && !_isPathManuallyEdited)
                    SetSuggestedPath(value);
            }
        }

        [Required(ErrorMessage = "Worktree path is required!")]
        [CustomValidation(typeof(AddWorktree), nameof(ValidateWorktreePath))]
        public string Path
        {
            get => _path;
            set
            {
                if (SetProperty(ref _path, value, true) && !_isUpdatingSuggestedPath)
                    _isPathManuallyEdited = !string.IsNullOrWhiteSpace(value);
            }
        }

        public bool CreateNewBranch
        {
            get => _createNewBranch;
            set
            {
                if (SetProperty(ref _createNewBranch, value, true))
                {
                    if (value)
                    {
                        SelectedBranch = null;
                        AutoFillNewBranchNameFromTracking();
                    }
                    else
                    {
                        SelectedBranch = LocalBranches.Count > 0 ? LocalBranches[0] : null;
                    }
                }
            }
        }

        public List<Models.Branch> LocalBranches
        {
            get;
            private set;
        }

        public Models.Branch SelectedBranch
        {
            get => _selectedBranch;
            set => SetProperty(ref _selectedBranch, value);
        }

        public string NewBranchName
        {
            get => _newBranchName;
            set
            {
                if (SetProperty(ref _newBranchName, value) && !_isUpdatingSuggestedBranchName)
                    _isBranchNameManuallyEdited = !string.IsNullOrWhiteSpace(value);
            }
        }

        public bool SetTrackingBranch
        {
            get => _setTrackingBranch;
            set
            {
                if (SetProperty(ref _setTrackingBranch, value))
                {
                    AutoSelectTrackingBranch();
                    AutoFillNewBranchNameFromTracking();
                }
            }
        }

        public List<Models.Branch> RemoteBranches
        {
            get;
            private set;
        }

        public Models.Branch SelectedTrackingBranch
        {
            get => _selectedTrackingBranch;
            set
            {
                if (SetProperty(ref _selectedTrackingBranch, value))
                    AutoFillNewBranchNameFromTracking();
            }
        }

        public bool OpenInNewTab
        {
            get => _openInNewTab;
            set => SetProperty(ref _openInNewTab, value);
        }

        public AddWorktree(Repository repo)
        {
            _repo = repo;

            LocalBranches = new List<Models.Branch>();
            RemoteBranches = new List<Models.Branch>();
            foreach (var branch in repo.Branches)
            {
                if (branch.IsLocal)
                {
                    if (!branch.IsCurrent && !branch.HasWorktree)
                        LocalBranches.Add(branch);
                }
                else
                {
                    RemoteBranches.Add(branch);
                }
            }
        }

        public static ValidationResult ValidateWorktreePath(string path, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is not AddWorktree creator)
                return new ValidationResult("Missing runtime context to create branch!");

            if (string.IsNullOrEmpty(path))
                return new ValidationResult("Worktree path is required!");

            var fullPath = System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(creator._repo.FullPath, path);
            var info = new DirectoryInfo(fullPath);
            if (info.Exists)
            {
                var files = info.GetFiles();
                if (files.Length > 0)
                    return new ValidationResult("Given path is not empty!!!");

                var folders = info.GetDirectories();
                if (folders.Length > 0)
                    return new ValidationResult("Given path is not empty!!!");
            }

            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            ProgressDescription = "Adding worktree ...";

            var branchName = GetBranchName(false);
            var tracking = (_setTrackingBranch && _selectedTrackingBranch != null) ? _selectedTrackingBranch.FriendlyName : string.Empty;
            var log = _repo.CreateLog("Add Worktree");

            Use(log);

            bool succ;
            using (_repo.LockWatcher())
            {
                succ = await new Commands.Worktree(_repo.FullPath)
                    .Use(log)
                    .AddAsync(_path, branchName, _createNewBranch, tracking);
            }

            log.Complete();
            if (succ)
            {
                var fullPath = System.IO.Path.IsPathRooted(_path)
                    ? System.IO.Path.GetFullPath(_path)
                    : System.IO.Path.GetFullPath(System.IO.Path.Combine(_repo.FullPath, _path));

                if (_createNewBranch)
                {
                    var baseBranch = _setTrackingBranch && _selectedTrackingBranch != null
                        ? _selectedTrackingBranch.Name
                        : _repo.CurrentBranch?.Name ?? string.Empty;
                    baseBranch = Models.WorktreeBaseBranch.Normalize(baseBranch);

                    if (Models.WorktreeBaseBranch.GetKind(baseBranch) != Models.WorktreeBaseBranchKind.None)
                    {
                        var gitDir = await new Commands.QueryWorktreeBaseBranch(fullPath).GetGitDirAsync();
                        Models.WorktreeBaseBranch.WritePersisted(gitDir, branchName, baseBranch);
                    }
                }

                if (_openInNewTab)
                    App.GetLauncher()?.OpenRepositoryInTab(fullPath, null);
            }

            return succ;
        }

        private void AutoSelectTrackingBranch()
        {
            if (!_setTrackingBranch || RemoteBranches.Count == 0)
                return;

            var name = GetBranchName(true);
            if (!string.IsNullOrEmpty(name))
            {
                var remoteBranch = RemoteBranches.Find(b => b.Name.Equals(name, StringComparison.Ordinal));
                remoteBranch ??= RemoteBranches.Find(b => b.Name.EndsWith("/" + name, StringComparison.Ordinal));
                if (remoteBranch != null)
                {
                    SelectedTrackingBranch = remoteBranch;
                    return;
                }
            }

            SelectedTrackingBranch = RemoteBranches[0];
        }

        private void AutoFillNewBranchNameFromTracking()
        {
            if (!_createNewBranch || !_setTrackingBranch || _selectedTrackingBranch == null || _isBranchNameManuallyEdited)
                return;

            _isUpdatingSuggestedBranchName = true;
            try
            {
                NewBranchName = _selectedTrackingBranch.Name;
            }
            finally
            {
                _isUpdatingSuggestedBranchName = false;
            }
        }

        private string GetBranchName(bool fallback)
        {
            do
            {
                if (!_createNewBranch)
                {
                    if (_selectedBranch != null)
                        return _selectedBranch.Name;

                    break;
                }

                if (!string.IsNullOrEmpty(_newBranchName))
                    return _newBranchName;
            } while (false);

            return fallback ? System.IO.Path.GetFileName(_path.TrimEnd('/', '\\')) : string.Empty;
        }

        private void SetSuggestedPath(string name)
        {
            _isUpdatingSuggestedPath = true;
            try
            {
                Path = string.IsNullOrWhiteSpace(name) ? string.Empty : System.IO.Path.Combine("..", name.Trim());
            }
            finally
            {
                _isUpdatingSuggestedPath = false;
            }
        }

        private Repository _repo = null;
        private string _worktreeName = string.Empty;
        private string _path = string.Empty;
        private bool _isPathManuallyEdited = false;
        private bool _isUpdatingSuggestedPath = false;
        private bool _createNewBranch = true;
        private Models.Branch _selectedBranch = null;
        private string _newBranchName = string.Empty;
        private bool _isBranchNameManuallyEdited = false;
        private bool _isUpdatingSuggestedBranchName = false;
        private bool _setTrackingBranch = false;
        private Models.Branch _selectedTrackingBranch = null;
        private bool _openInNewTab = true;
    }
}
