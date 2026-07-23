using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class DiscardAllMode : ObservableObject
    {
        public bool IncludeModified
        {
            get => _includeModified;
            set
            {
                if (SetProperty(ref _includeModified, value))
                    OnPropertyChanged(nameof(CanRestoreFromTrash));
            }
        }

        public bool IncludeUntracked
        {
            get => _includeUntracked;
            set
            {
                if (SetProperty(ref _includeUntracked, value))
                    OnPropertyChanged(nameof(CanRestoreFromTrash));
            }
        }

        public bool IncludeIgnored
        {
            get => _includeIgnored;
            set
            {
                if (SetProperty(ref _includeIgnored, value))
                    OnPropertyChanged(nameof(CanRestoreFromTrash));
            }
        }

        // With the safetynet on, everything a discard-all removes is recoverable from the trash bin:
        // untracked/ignored files are recycled, and modified files are snapshotted to the bin before
        // `git reset --hard`. So the reassurance shows whenever the safetynet is on and something is
        // actually being discarded.
        public bool CanRestoreFromTrash =>
            Preferences.Instance.TrashOnDiscardAll && (_includeModified || _includeUntracked || _includeIgnored);

        private bool _includeModified = true;
        private bool _includeUntracked = false;
        private bool _includeIgnored = false;
    }

    public class DiscardSingleFile
    {
        public string Path
        {
            get;
            set;
        } = string.Empty;

        public bool CanRestoreFromTrash
        {
            get;
            set;
        } = false;
    }

    public class DiscardMultipleFiles
    {
        public int Count
        {
            get;
            set;
        } = 0;

        public bool CanRestoreFromTrash
        {
            get;
            set;
        } = false;
    }

    public class Discard : Popup
    {
        public object Mode
        {
            get;
        }

        public Discard(Repository repo)
        {
            _repo = repo;
            Mode = new DiscardAllMode();
        }

        public Discard(Repository repo, List<Models.Change> changes)
        {
            _repo = repo;
            _changes = changes;

            // With the safetynet on, every selected change is recoverable from the trash bin:
            // untracked/added files are recycled, and modified files are snapshotted to the bin
            // before `git restore` reverts them.
            var canRestore = Preferences.Instance.TrashOnDiscard;

            if (_changes == null)
                Mode = new DiscardAllMode();
            else if (_changes.Count == 1)
                Mode = new DiscardSingleFile() { Path = _changes[0].Path, CanRestoreFromTrash = canRestore };
            else
                Mode = new DiscardMultipleFiles() { Count = _changes.Count, CanRestoreFromTrash = canRestore };
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = _changes == null ? "Discard all local changes ..." : $"Discard total {_changes.Count} changes ...";

            var log = _repo.CreateLog("Discard Changes");
            Use(log);

            if (Mode is DiscardAllMode all)
            {
                await Commands.Discard.AllAsync(_repo.FullPath, all.IncludeModified, all.IncludeUntracked, all.IncludeIgnored, Preferences.Instance.TrashOnDiscardAll, log);
                _repo.ClearCommitMessage();
            }
            else
            {
                await Commands.Discard.ChangesAsync(_repo.FullPath, _changes, Preferences.Instance.TrashOnDiscard, log);
            }

            log.Complete();
            _repo.MarkWorkingCopyDirtyManually();
            return true;
        }

        private readonly Repository _repo = null;
        private readonly List<Models.Change> _changes = null;
    }
}
