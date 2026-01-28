using System;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class EditBranchDescription : Popup
    {
        public Models.Branch Target
        {
            get;
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public EditBranchDescription(Repository repo, Models.Branch target, string desc)
        {
            Target = target;

            _repo = repo;
            _originalDescription = desc;
            _description = desc;
        }

        public override async Task<bool> Sure()
        {
            var trimmed = _description.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                if (string.IsNullOrEmpty(_originalDescription))
                    return true;
            }
            else if (trimmed.Equals(_originalDescription, StringComparison.Ordinal))
            {
                return true;
            }

            var log = _repo.CreateLog("Edit Branch Description");
            Use(log);

            await new Commands.Config(_repo.FullPath)
                .Use(log)
                .SetAsync($"branch.{Target.Name}.description", trimmed);

            log.Complete();
            return true;
        }

        private readonly Repository _repo;
        private string _originalDescription = string.Empty;
        private string _description = string.Empty;
    }
}
