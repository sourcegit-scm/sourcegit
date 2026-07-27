using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class GitFlowStart : Popup
    {
        public Models.GitFlowBranchType Type
        {
            get;
            private set;
        }

        public List<Models.Branch> LocalBranches
        {
            get;
            private set;
        }

        public Models.Branch StartPoint
        {
            get => _startPoint;
            set => SetProperty(ref _startPoint, value);
        }

        public string Prefix
        {
            get;
            private set;
        }

        [Required(ErrorMessage = "Name is required!!!")]
        [RegularExpression(@"^[\w\-/\.#]+$", ErrorMessage = "Bad branch name format!")]
        [CustomValidation(typeof(GitFlowStart), nameof(ValidateBranchName))]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        public GitFlowStart(Repository repo, Models.GitFlowBranchType type)
        {
            _repo = repo;

            Type = type;
            Prefix = _repo.GitFlow.GetPrefix(type);
            LocalBranches = new List<Models.Branch>();

            foreach (var b in _repo.Branches)
            {
                if (b.IsLocal && !b.IsDetachedHead)
                    LocalBranches.Add(b);
            }

            var defBranch = type switch
            {
                Models.GitFlowBranchType.Hotfix => _repo.GitFlow.ProductionBranch,
                _ => _repo.GitFlow.DevelopmentBranch,
            };

            StartPoint = LocalBranches.Find(b => b.Name.Equals(defBranch, StringComparison.Ordinal));
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is GitFlowStart starter)
            {
                var check = $"{starter.Prefix}{name}";
                foreach (var b in starter._repo.Branches)
                {
                    if (b.FriendlyName == check)
                        return new ValidationResult("A branch with same name already exists!");
                }
            }

            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Git Flow - Start {Prefix}{_name} ...";

            var log = _repo.CreateLog("GitFlow - Start");
            Use(log);

            var succ = await new Commands.GitFlow(_repo.FullPath)
                .Use(log)
                .StartAsync(Type, _name, _startPoint);

            log.Complete();
            return succ;
        }

        private readonly Repository _repo;
        private string _name = null;
        private Models.Branch _startPoint = null;
    }
}
