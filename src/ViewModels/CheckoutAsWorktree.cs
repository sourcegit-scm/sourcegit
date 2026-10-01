using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CheckoutAsWorktree : Popup
    {
        public Models.Branch Branch
        {
            get => _branch;
        }

        [CustomValidation(typeof(CheckoutAsWorktree), nameof(ValidateWorktreePath))]
        public string WorktreePath
        {
            get => _worktreePath;
            set => SetProperty(ref _worktreePath, value, true);
        }

        public CheckoutAsWorktree(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            _branch = branch;
            _worktreePath = Path.Combine(repo.GetRecommandedWorktreeDir(), branch.Name.Replace('/', '-').Replace('\\', '-'));
        }

        public static ValidationResult ValidateWorktreePath(string path, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is not CheckoutAsWorktree creator)
                return new ValidationResult("Missing runtime context to checkout branch as worktree!");

            if (string.IsNullOrEmpty(path))
                return new ValidationResult("Worktree path is required!");

            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(creator._repo.FullPath, path);
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
            ProgressDescription = $"Checkout branch '{_branch.Name}' as worktree ...";

            var log = _repo.CreateLog("Add Worktree");
            Use(log);

            var succ = await new Commands.Worktree(_repo.FullPath)
                .Use(log)
                .AddWithLocalBranchAsync(_worktreePath, _branch.Name);

            log.Complete();
            return succ;
        }

        private readonly Repository _repo;
        private readonly Models.Branch _branch;
        private string _worktreePath;
    }
}
