using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Checkout : Popup
    {
        public string Branch
        {
            get;
            private set;
        }
        
        public bool HasLocalChanges
        {
            get => _repo.WorkingCopyChangesCount > 0;
        }

        public bool LeaveLocalChanges
        {
            get => _leaveLocalChanges;
            set => SetProperty(ref _leaveLocalChanges, value);
        }

        public bool DiscardLocalChanges
        {
            get => _discardLocalChanges;
            set => SetProperty(ref _discardLocalChanges, value);
        }

        public bool StashLocalChanges
        {
            get => _stashLocalChanges;
            set => SetProperty(ref _stashLocalChanges, value);
        }

        public Checkout(Repository repo, string branch)
        {
            _repo = repo;
            Branch = branch;
            View = new Views.Checkout() { DataContext = this };

            StashLocalChanges = true;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Checkout '{Branch}' ...";
            var hasLocalChanges = HasLocalChanges;

            return Task.Run(() =>
            {
                var succ = false;
                if (hasLocalChanges)
                {
                    if (DiscardLocalChanges)
                    {
                        SetProgressDescription("Discard local changes...");
                        Commands.Discard.All(_repo.FullPath);
                    }

                    if (StashLocalChanges)
                    {
                        SetProgressDescription("Stash local changes...");
                        succ = new Commands.Add(_repo.FullPath).Exec();
                        succ = new Commands.Stash(_repo.FullPath).Push("CHECKOUT_AUTO_STASH");
                    }
                }
                
                SetProgressDescription("Checkout branch ...");
                succ = new Commands.Checkout(_repo.FullPath).Branch(Branch, SetProgressDescription);
                
                if(hasLocalChanges && StashLocalChanges)
                {
                    SetProgressDescription("Re-apply local changes...");
                    succ = new Commands.Stash(_repo.FullPath).Apply("stash@{0}");
                    if (succ)
                    {
                        succ = new Commands.Stash(_repo.FullPath).Drop("stash@{0}");
                    }
                }
                
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }
        
        public static void ShowPopup(Repository repo, string branch)
        {
            var checkout = new Checkout(repo, branch);
            if (repo.WorkingCopyChangesCount > 0)
            {
                PopupHost.ShowPopup(checkout);   
            }
            else
            {
                PopupHost.ShowAndStartPopup(checkout);
            }
        }

        private readonly Repository _repo;
        private bool _leaveLocalChanges;
        private bool _discardLocalChanges;
        private bool _stashLocalChanges;
    }
}
