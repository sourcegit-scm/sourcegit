using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public partial class InitGitFlow : Popup
    {
        [GeneratedRegex(@"^[\w\-/\.]+$")]
        private static partial Regex TAG_PREFIX();

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
        public string FeturePrefix
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
            if (!string.IsNullOrWhiteSpace(tagPrefix) && !TAG_PREFIX().IsMatch(tagPrefix))
                return new ValidationResult("Bad tag prefix format!");

            return ValidationResult.Success;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Init git-flow ...";

            var log = _repo.CreateLog("Gitflow - Init");
            Use(log);

            return Task.Run(() =>
            {
                var succ = Commands.GitFlow.Init(
                    _repo.FullPath,
                    _repo.Branches,
                    _master,
                    _develop,
                    _featurePrefix,
                    _releasePrefix,
                    _hotfixPrefix,
                    _tagPrefix,
                    log);

                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
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
