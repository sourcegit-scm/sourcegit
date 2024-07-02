﻿using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class GitFlowStart : Popup
    {
        [Required(ErrorMessage = "Name is required!!!")]
        [RegularExpression(@"^[\w\-/\.]+$", ErrorMessage = "Bad branch name format!")]
        [CustomValidation(typeof(GitFlowStart), nameof(ValidateBranchName))]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        public string Prefix
        {
            get => _prefix;
        }

        public bool IsFeature => _type == "feature";
        public bool IsRelease => _type == "release";
        public bool IsHotfix => _type == "hotfix";

        public GitFlowStart(Repository repo, string type)
        {
            _repo = repo;
            _type = type;
            _prefix = Commands.GitFlow.Prefix(repo.FullPath, type);

            View = new Views.GitFlowStart() { DataContext = this };
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is GitFlowStart starter)
            {
                var check = $"{starter._prefix}{name}";
                foreach (var b in starter._repo.Branches)
                {
                    if (b.FriendlyName == check)
                        return new ValidationResult("A branch with same name already exists!");
                }
            }

            return ValidationResult.Success;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            return Task.Run(() =>
            {
                SetProgressDescription($"Git Flow - starting {_type} {_name} ...");
                var succ = Commands.GitFlow.Start(_repo.FullPath, _type, _name);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
        private readonly string _type = "feature";
        private readonly string _prefix = string.Empty;
        private string _name = null;
    }
}
