﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
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
            set => SetProperty(ref _path, value);
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
            set => SetProperty(ref _setTrackingBranch, value);
        }

        public string SelectedTrackingBranch
        {
            get;
            set;
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
                SelectedTrackingBranch = RemoteBranches[0];
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

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Adding worktree ...";

            var branchName = _selectedBranch;
            var tracking = _setTrackingBranch ? SelectedTrackingBranch : string.Empty;

            return Task.Run(() =>
            {
                var succ = new Commands.Worktree(_repo.FullPath).Add(_path, branchName, _createNewBranch, tracking, SetProgressDescription);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private Repository _repo = null;
        private string _path = string.Empty;
        private bool _createNewBranch = true;
        private string _selectedBranch = string.Empty;
        private bool _setTrackingBranch = false;
    }
}
