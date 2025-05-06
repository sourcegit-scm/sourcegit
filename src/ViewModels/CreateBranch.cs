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

        public bool IsRecurseSubmoduleVisible
        {
            get;
            private set;
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
            {
                Name = branch.Name;
            }

            BasedOn = branch;
            DiscardLocalChanges = false;
            IsRecurseSubmoduleVisible = repo.Submodules.Count > 0;
        }

        public CreateBranch(Repository repo, Models.Commit commit)
        {
            _repo = repo;
            _baseOnRevision = commit.SHA;

            BasedOn = commit;
            DiscardLocalChanges = false;
            IsRecurseSubmoduleVisible = repo.Submodules.Count > 0;
        }

        public CreateBranch(Repository repo, Models.Tag tag)
        {
            _repo = repo;
            _baseOnRevision = tag.SHA;

            BasedOn = tag;
            DiscardLocalChanges = false;
            IsRecurseSubmoduleVisible = repo.Submodules.Count > 0;
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            var creator = ctx.ObjectInstance as CreateBranch;
            if (creator == null)
                return new ValidationResult("Missing runtime context to create branch!");

            var fixedName = creator.FixName(name);
            foreach (var b in creator._repo.Branches)
            {
                if (b.FriendlyName == fixedName)
                    return new ValidationResult("A branch with same name already exists!");
            }

            return ValidationResult.Success;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);

            var fixedName = FixName(_name);
            var log = _repo.CreateLog($"Create Branch '{fixedName}'");
            Use(log);

            var updateSubmodules = IsRecurseSubmoduleVisible && RecurseSubmodules;
            return Task.Run(() =>
            {
                bool succ;
                if (CheckoutAfterCreated && !_repo.IsBare)
                {
                    var changes = new Commands.CountLocalChangesWithoutUntracked(_repo.FullPath).Result();
                    var needPopStash = false;
                    if (changes > 0)
                    {
                        if (DiscardLocalChanges)
                        {
                            Commands.Discard.All(_repo.FullPath, false, log);
                        }
                        else
                        {
                            succ = new Commands.Stash(_repo.FullPath).Use(log).Push("CREATE_BRANCH_AUTO_STASH");
                            if (!succ)
                            {
                                log.Complete();
                                CallUIThread(() => _repo.SetWatcherEnabled(true));
                                return false;
                            }

                            needPopStash = true;
                        }
                    }

                    succ = new Commands.Checkout(_repo.FullPath).Use(log).Branch(fixedName, _baseOnRevision, updateSubmodules);
                    if (needPopStash)
                        new Commands.Stash(_repo.FullPath).Use(log).Pop("stash@{0}");
                }
                else
                {
                    succ = Commands.Branch.Create(_repo.FullPath, fixedName, _baseOnRevision, log);
                }

                log.Complete();

                CallUIThread(() =>
                {
                    if (succ && CheckoutAfterCreated)
                    {
                        var fake = new Models.Branch() { IsLocal = true, FullName = $"refs/heads/{fixedName}" };
                        if (BasedOn is Models.Branch based && !based.IsLocal)
                            fake.Upstream = based.FullName;

                        var folderEndIdx = fake.FullName.LastIndexOf('/');
                        if (folderEndIdx > 10)
                            _repo.Settings.ExpandedBranchNodesInSideBar.Add(fake.FullName.Substring(0, folderEndIdx));

                        if (_repo.HistoriesFilterMode == Models.FilterMode.Included)
                            _repo.SetBranchFilterMode(fake, Models.FilterMode.Included, true, false);
                    }

                    _repo.MarkBranchesDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });

                if (CheckoutAfterCreated)
                {
                    CallUIThread(() => ProgressDescription = "Waiting for branch updated...");
                    Task.Delay(400).Wait();
                }

                return true;
            });
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
    }
}
