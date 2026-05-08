using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CreateBranch : Popup
    {
        [Required(ErrorMessage = "Branch name is required!")]
        [RegularExpression(@"^[\w\-/\.#\+]+$", ErrorMessage = "Bad branch name format!")]
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

        public bool HasLocalChanges
        {
            get => _repo.LocalChangesCount > 0;
        }

        public Models.DealWithLocalChanges DealWithLocalChanges
        {
            get;
            set;
        }

        public bool CheckoutAfterCreated
        {
            get => _repo.UIStates.CheckoutBranchOnCreateBranch;
            set
            {
                if (_repo.UIStates.CheckoutBranchOnCreateBranch != value)
                {
                    _repo.UIStates.CheckoutBranchOnCreateBranch = value;
                    OnPropertyChanged();
                    UpdateOverrideTip();
                }
            }
        }

        public bool IsBareRepository
        {
            get => _repo.IsBare;
        }

        public string OverrideTip
        {
            get => _overrideTip;
            private set => SetProperty(ref _overrideTip, value);
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

        public CreateBranch(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            _baseOnRevision = branch.Head;
            _committerDate = branch.CommitterDate;
            _head = branch.Head;

            if (!branch.IsLocal)
                Name = branch.Name;

            BasedOn = branch;
            DealWithLocalChanges = Models.DealWithLocalChanges.DoNothing;
            UpdateOverrideTip();
        }

        public CreateBranch(Repository repo, Models.Commit commit)
        {
            _repo = repo;
            _baseOnRevision = commit.SHA;
            _committerDate = commit.CommitterTime;
            _head = commit.SHA;

            BasedOn = commit;
            DealWithLocalChanges = Models.DealWithLocalChanges.DoNothing;
            UpdateOverrideTip();
        }

        public CreateBranch(Repository repo, Models.Tag tag)
        {
            _repo = repo;
            _baseOnRevision = tag.SHA;
            _committerDate = tag.CreatorDate;
            _head = tag.SHA;

            BasedOn = tag;
            DealWithLocalChanges = Models.DealWithLocalChanges.DoNothing;
            UpdateOverrideTip();
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is CreateBranch creator)
            {
                if (!creator._allowOverwrite)
                {
                    foreach (var b in creator._repo.Branches)
                    {
                        if (b.FriendlyName.Equals(name, StringComparison.Ordinal))
                            return new ValidationResult("A branch with same name already exists!");
                    }
                }

                return ValidationResult.Success;
            }

            return new ValidationResult("Missing runtime context to create branch!");
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();

            var log = _repo.CreateLog($"Create Branch '{_name}'");
            Use(log);

            if (CheckoutAfterCreated)
            {
                if (_repo.CurrentBranch is { IsDetachedHead: true } && !_repo.CurrentBranch.Head.Equals(_baseOnRevision, StringComparison.Ordinal))
                {
                    var refs = await new Commands.QueryRefsContainsCommit(_repo.FullPath, _repo.CurrentBranch.Head).GetResultAsync();
                    if (refs.Count == 0)
                    {
                        var msg = App.Text("Checkout.WarnLostCommits");
                        var shouldContinue = await App.AskConfirmAsync(msg);
                        if (!shouldContinue)
                            return true;
                    }
                }
            }

            Models.Branch created = new()
            {
                Name = _name,
                FullName = $"refs/heads/{_name}",
                CommitterDate = _committerDate,
                Head = _head,
                IsLocal = true,
            };

            bool succ;
            if (CheckoutAfterCreated && !_repo.IsBare)
            {
                var needPopStash = false;
                if (DealWithLocalChanges == Models.DealWithLocalChanges.DoNothing)
                {
                    succ = await new Commands.Checkout(_repo.FullPath)
                        .Use(log)
                        .BranchAsync(_name, _baseOnRevision, false, _allowOverwrite);
                }
                else if (DealWithLocalChanges == Models.DealWithLocalChanges.StashAndReapply)
                {
                    var changes = await new Commands.CountLocalChanges(_repo.FullPath, false).GetResultAsync();
                    if (changes > 0)
                    {
                        succ = await new Commands.Stash(_repo.FullPath)
                            .Use(log)
                            .PushAsync("CREATE_BRANCH_AUTO_STASH", false);
                        if (!succ)
                        {
                            log.Complete();
                            _repo.MarkWorkingCopyDirtyManually();
                            return false;
                        }

                        needPopStash = true;
                    }

                    succ = await new Commands.Checkout(_repo.FullPath)
                        .Use(log)
                        .BranchAsync(_name, _baseOnRevision, false, _allowOverwrite);
                }
                else
                {
                    succ = await new Commands.Checkout(_repo.FullPath)
                        .Use(log)
                        .BranchAsync(_name, _baseOnRevision, true, _allowOverwrite);
                }

                if (succ)
                {
                    await _repo.AutoUpdateSubmodulesAsync(log);

                    if (needPopStash)
                        await new Commands.Stash(_repo.FullPath)
                            .Use(log)
                            .PopAsync("stash@{0}");
                }
            }
            else
            {
                succ = await new Commands.Branch(_repo.FullPath, _name)
                    .Use(log)
                    .CreateAsync(_baseOnRevision, _allowOverwrite);
            }

            if (succ)
            {
                if (BasedOn is Models.Branch { IsLocal: false } basedOn && _name.Equals(basedOn.Name, StringComparison.Ordinal))
                {
                    await new Commands.Branch(_repo.FullPath, _name)
                        .Use(log)
                        .SetUpstreamAsync(basedOn);

                    created.Upstream = basedOn.FullName;
                }

                _repo.RefreshAfterCreateBranch(created, CheckoutAfterCreated);
            }
            else
            {
                _repo.MarkWorkingCopyDirtyManually();
            }

            log.Complete();
            return true;
        }

        private void UpdateOverrideTip()
        {
            OverrideTip = CheckoutAfterCreated ? "-B in `git checkout`" : "-f in `git branch`";
        }

        private readonly Repository _repo = null;
        private readonly string _baseOnRevision = null;
        private readonly ulong _committerDate = 0;
        private readonly string _head = string.Empty;
        private string _name = null;
        private string _overrideTip = "-B";
        private bool _allowOverwrite = false;
    }
}
