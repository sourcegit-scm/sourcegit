using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public partial class InitGitFlow : Popup
    {
        [GeneratedRegex(@"^[\w\-/\.]+$")]
        private static partial Regex REG_TAG_PREFIX();

        [Required(ErrorMessage = "Master branch name is required!!!")]
        [RegularExpression(@"^[\w\-/\.]+$", ErrorMessage = "Bad branch name format!")]
        [CustomValidation(typeof(InitGitFlow), nameof(ValidateBaseBranch))]
        public string Master
        {
            get => _master;
            set => SetProperty(ref _master, value, true);
        }

        [Required(ErrorMessage = "Develop branch name is required!!!")]
        [RegularExpression(@"^[\w\-/\.]+$", ErrorMessage = "Bad branch name format!")]
        [CustomValidation(typeof(InitGitFlow), nameof(ValidateBaseBranch))]
        public string Develop
        {
            get => _develop;
            set => SetProperty(ref _develop, value, true);
        }

        [Required(ErrorMessage = "Feature prefix is required!!!")]
        [RegularExpression(@"^[\w\-\.]+/$", ErrorMessage = "Bad feature prefix format!")]
        public string FeaturePrefix
        {
            get => _featurePrefix;
            set => SetProperty(ref _featurePrefix, value, true);
        }

        [Required(ErrorMessage = "Release prefix is required!!!")]
        [RegularExpression(@"^[\w\-\.]+/$", ErrorMessage = "Bad release prefix format!")]
        public string ReleasePrefix
        {
            get => _releasePrefix;
            set => SetProperty(ref _releasePrefix, value, true);
        }

        [Required(ErrorMessage = "Hotfix prefix is required!!!")]
        [RegularExpression(@"^[\w\-\.]+/$", ErrorMessage = "Bad hotfix prefix format!")]
        public string HotfixPrefix
        {
            get => _hotfixPrefix;
            set => SetProperty(ref _hotfixPrefix, value, true);
        }

        [CustomValidation(typeof(InitGitFlow), nameof(ValidateTagPrefix))]
        public string TagPrefix
        {
            get => _tagPrefix;
            set => SetProperty(ref _tagPrefix, value, true);
        }

        public InitGitFlow(Repository repo)
        {
            _repo = repo;

            var localBranches = new List<string>();
            foreach (var branch in repo.Branches)
            {
                if (branch.IsLocal)
                    localBranches.Add(branch.Name);
            }

            if (localBranches.Contains("master"))
                _master = "master";
            else if (localBranches.Contains("main"))
                _master = "main";
            else if (localBranches.Count > 0)
                _master = localBranches[0];
            else
                _master = "master";
        }

        public static ValidationResult ValidateBaseBranch(string _, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is InitGitFlow initializer)
            {
                if (initializer._master == initializer._develop)
                    return new ValidationResult("Develop branch has the same name with master branch!");
            }

            return ValidationResult.Success;
        }

        public static ValidationResult ValidateTagPrefix(string tagPrefix, ValidationContext ctx)
        {
            if (!string.IsNullOrWhiteSpace(tagPrefix) && !REG_TAG_PREFIX().IsMatch(tagPrefix))
                return new ValidationResult("Bad tag prefix format!");

            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Init git-flow ...";

            var log = _repo.CreateLog("Gitflow - Init");
            Use(log);

            bool succ;
            var current = _repo.CurrentBranch;

            var masterBranch = _repo.Branches.Find(x => x.IsLocal && x.Name.Equals(_master, StringComparison.Ordinal));
            if (masterBranch == null)
            {
                succ = await new Commands.Branch(_repo.FullPath, _master)
                    .Use(log)
                    .CreateAsync(current.Head, true);
                if (!succ)
                {
                    log.Complete();
                    return false;
                }
            }

            var developBranch = _repo.Branches.Find(x => x.IsLocal && x.Name.Equals(_develop, StringComparison.Ordinal));
            if (developBranch == null)
            {
                succ = await new Commands.Branch(_repo.FullPath, _develop)
                    .Use(log)
                    .CreateAsync(current.Head, true);
                if (!succ)
                {
                    log.Complete();
                    return false;
                }
            }

            succ = await Commands.GitFlow.InitAsync(
                _repo.FullPath,
                _master,
                _develop,
                _featurePrefix,
                _releasePrefix,
                _hotfixPrefix,
                _tagPrefix,
                log);

            log.Complete();

            if (succ)
            {
                var gitflow = new Models.GitFlow();
                gitflow.Master = _master;
                gitflow.Develop = _develop;
                gitflow.FeaturePrefix = _featurePrefix;
                gitflow.ReleasePrefix = _releasePrefix;
                gitflow.HotfixPrefix = _hotfixPrefix;
                _repo.GitFlow = gitflow;
            }

            return succ;
        }

        private readonly Repository _repo;
        private string _master;
        private string _develop = "develop";
        private string _featurePrefix = "feature/";
        private string _releasePrefix = "release/";
        private string _hotfixPrefix = "hotfix/";
        private string _tagPrefix = string.Empty;
    }
}
