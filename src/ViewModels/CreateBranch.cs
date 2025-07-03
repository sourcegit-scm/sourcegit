using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CreateBranch : Popup
    {
        [Required(ErrorMessage = "Branch name is required!")]
        [RegularExpression(@"^[\w \-/\.#\+]+$", ErrorMessage = "Bad branch name format!")]
        [CustomValidation(typeof(CreateBranch), nameof(ValidateBranchName))]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        public object BasedOn
        {
            get;
        }

        public bool DiscardLocalChanges
        {
            get;
            set;
        }

        public bool CheckoutAfterCreated
        {
            get => _repo.Settings.CheckoutBranchOnCreateBranch;
            set
            {
                if (_repo.Settings.CheckoutBranchOnCreateBranch != value)
                {
                    _repo.Settings.CheckoutBranchOnCreateBranch = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsBareRepository
        {
            get => _repo.IsBare;
        }

        public bool AllowOverwrite
        {
            get => _allowOverwrite;
            set
            {
                if (SetProperty(ref _allowOverwrite, value))
                    ValidateProperty(_name, nameof(Name));
            }
        }

        public bool IsRecurseSubmoduleVisible
        {
            get => _repo.Submodules.Count > 0;
        }

        public bool RecurseSubmodules
        {
            get => _repo.Settings.UpdateSubmodulesOnCheckoutBranch;
            set => _repo.Settings.UpdateSubmodulesOnCheckoutBranch = value;
        }

        public CreateBranch(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            _baseOnRevision = branch.IsDetachedHead ? branch.Head : branch.FullName;

            if (!branch.IsLocal && repo.Branches.Find(x => x.IsLocal && x.Name == branch.Name) == null)
                Name = branch.Name;

            BasedOn = branch;
            DiscardLocalChanges = false;
        }

        public CreateBranch(Repository repo, Models.Commit commit)
        {
            _repo = repo;
            _baseOnRevision = commit.SHA;

            BasedOn = commit;
            DiscardLocalChanges = false;
        }

        public CreateBranch(Repository repo, Models.Tag tag)
        {
            _repo = repo;
            _baseOnRevision = tag.SHA;

            BasedOn = tag;
            DiscardLocalChanges = false;
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is CreateBranch creator)
            {
                if (!creator._allowOverwrite)
                {
                    var fixedName = creator.FixName(name);
                    foreach (var b in creator._repo.Branches)
                    {
                        if (b.FriendlyName == fixedName)
                            return new ValidationResult("A branch with same name already exists!");
                    }
                }

                return ValidationResult.Success;
            }

            return new ValidationResult("Missing runtime context to create branch!");
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);

            var fixedName = FixName(_name);
            var log = _repo.CreateLog($"Create Branch '{fixedName}'");
            Use(log);

            var updateSubmodules = IsRecurseSubmoduleVisible && RecurseSubmodules;
            bool succ;

            if (CheckoutAfterCreated)
            {
                var confirmed = await _repo.ConfirmCheckoutBranchAsync();
                if (!confirmed)
                {
                    _repo.SetWatcherEnabled(true);
                    return true;
                }
            }

            if (CheckoutAfterCreated && !_repo.IsBare)
            {
                var needPopStash = false;
                if (DiscardLocalChanges)
                {
                    succ = await new Commands.Checkout(_repo.FullPath)
                        .Use(log)
                        .BranchAsync(fixedName, _baseOnRevision, true, _allowOverwrite);
                }
                else
                {
                    var changes = await new Commands.CountLocalChangesWithoutUntracked(_repo.FullPath).GetResultAsync();
                    if (changes > 0)
                    {
                        succ = await new Commands.Stash(_repo.FullPath)
                            .Use(log)
                            .PushAsync("CREATE_BRANCH_AUTO_STASH");
                        if (!succ)
                        {
                            log.Complete();
                            _repo.SetWatcherEnabled(true);
                            return false;
                        }

                        needPopStash = true;
                    }

                    succ = await new Commands.Checkout(_repo.FullPath)
                        .Use(log)
                        .BranchAsync(fixedName, _baseOnRevision, false, _allowOverwrite);
                }

                if (succ)
                {
                    if (updateSubmodules)
                    {
                        var submodules = await new Commands.QueryUpdatableSubmodules(_repo.FullPath).GetResultAsync();
                        if (submodules.Count > 0)
                            await new Commands.Submodule(_repo.FullPath)
                                .Use(log)
                                .UpdateAsync(submodules, true, true);
                    }

                    if (needPopStash)
                        await new Commands.Stash(_repo.FullPath)
                            .Use(log)
                            .PopAsync("stash@{0}");
                }
            }
            else
            {
                succ = await Commands.Branch.CreateAsync(_repo.FullPath, fixedName, _baseOnRevision, _allowOverwrite, log);
            }

            log.Complete();

            if (succ && CheckoutAfterCreated)
            {
                var fake = new Models.Branch() { IsLocal = true, FullName = $"refs/heads/{fixedName}" };
                if (BasedOn is Models.Branch { IsLocal: false } based)
                    fake.Upstream = based.FullName;

                var folderEndIdx = fake.FullName.LastIndexOf('/');
                if (folderEndIdx > 10)
                    _repo.Settings.ExpandedBranchNodesInSideBar.Add(fake.FullName.Substring(0, folderEndIdx));

                if (_repo.HistoriesFilterMode == Models.FilterMode.Included)
                    _repo.SetBranchFilterMode(fake, Models.FilterMode.Included, true, false);

            }

            _repo.MarkBranchesDirtyManually();
            _repo.SetWatcherEnabled(true);

            if (CheckoutAfterCreated)
            {
                ProgressDescription = "Waiting for branch updated...";
                await Task.Delay(400);
            }

            return true;
        }

        private string FixName(string name)
        {
            if (!name.Contains(' '))
                return name;

            var parts = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            return string.Join("-", parts);
        }

        private readonly Repository _repo = null;
        private string _name = null;
        private readonly string _baseOnRevision = null;
        private bool _allowOverwrite = false;
    }
}
