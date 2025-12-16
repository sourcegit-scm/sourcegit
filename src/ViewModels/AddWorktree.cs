using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class AddWorktree : Popup
    {
        [Required(ErrorMessage = "Worktree path is required!")]
        [CustomValidation(typeof(AddWorktree), nameof(ValidateWorktreePath))]
        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value, true);
        }

        public bool CreateNewBranch
        {
            get => _createNewBranch;
            set
            {
                if (SetProperty(ref _createNewBranch, value, true))
                {
                    if (value)
                        SelectedBranch = string.Empty;
                    else
                        SelectedBranch = LocalBranches.Count > 0 ? LocalBranches[0] : string.Empty;
                }
            }
        }

        public List<string> LocalBranches
        {
            get;
            private set;
        }

        public List<string> RemoteBranches
        {
            get;
            private set;
        }

        public string SelectedBranch
        {
            get => _selectedBranch;
            set => SetProperty(ref _selectedBranch, value);
        }

        public bool SetTrackingBranch
        {
            get => _setTrackingBranch;
            set
            {
                if (SetProperty(ref _setTrackingBranch, value))
                {
                    if (value)
                    {
                        var remoteAndBranchName = RemoteBranches.Where(b => b.EndsWith(SelectedBranch));
                        SetSelectedTrackingBranch = remoteAndBranchName.FirstOrDefault();
                    }
                        
                }
            }
        }

        [Required(ErrorMessage = "Tracking branch is required!")]
        [CustomValidation(typeof(AddWorktree), nameof(ValidateTrackingBranch))]
        public string SetSelectedTrackingBranch
        {
            get => _setTrackingBranch ? _selectedTrackingBranch : string.Empty;
            set => SetProperty(ref _selectedTrackingBranch, value, validate: true);
        }

        public static ValidationResult ValidateTrackingBranch(string trackingBranch, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is not AddWorktree creator)
                return new ValidationResult("Missing runtime context to create branch!");

            if(!creator._setTrackingBranch)
                return ValidationResult.Success;
            
            if (string.IsNullOrEmpty(trackingBranch))
                return new ValidationResult("Tracking branch is required!");
            
            if(!creator.RemoteBranches.Contains(trackingBranch))
                return new ValidationResult("Invalid tracking branch!");
            
            return ValidationResult.Success;
        }

        public AddWorktree(Repository repo)
        {
            _repo = repo;

            LocalBranches = new List<string>();
            RemoteBranches = new List<string>();
            foreach (var branch in repo.Branches)
            {
                if (branch.IsLocal)
                    LocalBranches.Add(branch.Name);
                else
                    RemoteBranches.Add(branch.FriendlyName);
            }

            if (RemoteBranches.Count > 0)
                SetSelectedTrackingBranch = RemoteBranches[0];
            else
                SetSelectedTrackingBranch = string.Empty;
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
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Adding worktree ...";

            var branchName = _selectedBranch;
            var tracking = _setTrackingBranch ? _selectedTrackingBranch : string.Empty;
            var log = _repo.CreateLog("Add Worktree");

            Use(log);

            var succ = await new Commands.Worktree(_repo.FullPath)
                .Use(log)
                .AddAsync(_path, branchName, _createNewBranch, tracking);

            log.Complete();
            return succ;
        }

        private Repository _repo = null;
        private string _path = string.Empty;
        private bool _createNewBranch = true;
        private string _selectedBranch = string.Empty;
        private bool _setTrackingBranch = false;
        private string _selectedTrackingBranch = string.Empty;
    }
}
