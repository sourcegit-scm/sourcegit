using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CreateBranch : Popup
    {
        [Required(ErrorMessage = "Branch name is required!")]
        [RegularExpression(@"^[\w\-/\.#]+$", ErrorMessage = "Bad branch name format!")]
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

        public Models.DealWithLocalChanges PreAction
        {
            get => _repo.Settings.DealWithLocalChangesOnCreateBranch;
            set => _repo.Settings.DealWithLocalChangesOnCreateBranch = value;
        }

        public bool CheckoutAfterCreated
        {
            get => _repo.Settings.CheckoutBranchOnCreateBranch;
            set => _repo.Settings.CheckoutBranchOnCreateBranch = value;
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
            View = new Views.CreateBranch() { DataContext = this };
        }

        public CreateBranch(Repository repo, Models.Commit commit)
        {
            _repo = repo;
            _baseOnRevision = commit.SHA;

            BasedOn = commit;
            View = new Views.CreateBranch() { DataContext = this };
        }

        public CreateBranch(Repository repo, Models.Tag tag)
        {
            _repo = repo;
            _baseOnRevision = tag.SHA;

            BasedOn = tag;
            View = new Views.CreateBranch() { DataContext = this };
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            var creator = ctx.ObjectInstance as CreateBranch;
            if (creator == null)
                return new ValidationResult("Missing runtime context to create branch!");

            foreach (var b in creator._repo.Branches)
            {
                if (b.FriendlyName == name)
                    return new ValidationResult("A branch with same name already exists!");
            }

            return ValidationResult.Success;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            return Task.Run(() =>
            {
                var succ = false;
                if (CheckoutAfterCreated)
                {
                    var changes = new Commands.CountLocalChangesWithoutUntracked(_repo.FullPath).Result();
                    var needPopStash = false;
                    if (changes > 0)
                    {
                        if (PreAction == Models.DealWithLocalChanges.StashAndReaply)
                        {
                            SetProgressDescription("Stash local changes");
                            succ = new Commands.Stash(_repo.FullPath).Push("CREATE_BRANCH_AUTO_STASH");
                            if (!succ)
                            {
                                CallUIThread(() => _repo.SetWatcherEnabled(true));
                                return false;
                            }

                            needPopStash = true;
                        }
                        else if (PreAction == Models.DealWithLocalChanges.Discard)
                        {
                            SetProgressDescription("Discard local changes...");
                            Commands.Discard.All(_repo.FullPath, false);
                        }
                    }

                    SetProgressDescription($"Create new branch '{_name}'");
                    succ = new Commands.Checkout(_repo.FullPath).Branch(_name, _baseOnRevision, SetProgressDescription);

                    if (needPopStash)
                    {
                        SetProgressDescription("Re-apply local changes...");
                        new Commands.Stash(_repo.FullPath).Pop("stash@{0}");
                    }
                }
                else
                {
                    SetProgressDescription($"Create new branch '{_name}'");
                    succ = Commands.Branch.Create(_repo.FullPath, _name, _baseOnRevision);
                }

                CallUIThread(() =>
                {
                    if (succ && CheckoutAfterCreated)
                    {
                        var fake = new Models.Branch() { IsLocal = true, FullName = $"refs/heads/{_name}" };
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

                return true;
            });
        }

        private readonly Repository _repo = null;
        private string _name = null;
        private readonly string _baseOnRevision = null;
    }
}
