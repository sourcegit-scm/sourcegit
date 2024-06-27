using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public partial class AddWorktree : Popup
    {
        [GeneratedRegex(@"^[\w\-/\.]+$")]
        private static partial Regex REG_NAME();

        [Required(ErrorMessage = "Worktree path is required!")]
        [CustomValidation(typeof(AddWorktree), nameof(ValidateWorktreePath))]
        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value, true);
        }

        [CustomValidation(typeof(AddWorktree), nameof(ValidateBranchName))]
        public string CustomName
        {
            get => _customName;
            set => SetProperty(ref _customName, value, true);
        }

        public bool SetTrackingBranch
        {
            get => _setTrackingBranch;
            set => SetProperty(ref _setTrackingBranch, value);
        }

        public List<string> TrackingBranches
        {
            get;
            private set;
        }

        public string SelectedTrackingBranch
        {
            get;
            set;
        }

        public AddWorktree(Repository repo)
        {
            _repo = repo;

            TrackingBranches = new List<string>();
            foreach (var branch in repo.Branches)
            {
                if (!branch.IsLocal)
                    TrackingBranches.Add($"{branch.Remote}/{branch.Name}");
            }

            if (TrackingBranches.Count > 0)
                SelectedTrackingBranch = TrackingBranches[0];
            else
                SelectedTrackingBranch = string.Empty;

            View = new Views.AddWorktree() { DataContext = this };
        }

        public static ValidationResult ValidateWorktreePath(string path, ValidationContext ctx)
        {
            var creator = ctx.ObjectInstance as AddWorktree;
            if (creator == null)
                return new ValidationResult("Missing runtime context to create branch!");

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

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (string.IsNullOrEmpty(name))
                return ValidationResult.Success;

            var creator = ctx.ObjectInstance as AddWorktree;
            if (creator == null)
                return new ValidationResult("Missing runtime context to create branch!");

            foreach (var b in creator._repo.Branches)
            {
                var test = b.IsLocal ? b.Name : $"{b.Remote}/{b.Name}";
                if (test == name)
                    return new ValidationResult("A branch with same name already exists!");
            }

            return ValidationResult.Success;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Adding worktree ...";

            var tracking = _setTrackingBranch ? SelectedTrackingBranch : string.Empty;

            return Task.Run(() =>
            {
                var succ = new Commands.Worktree(_repo.FullPath).Add(_path, _customName, tracking, SetProgressDescription);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private Repository _repo = null;
        private string _path = string.Empty;
        private string _customName = string.Empty;
        private bool _setTrackingBranch = false;
    }
}
