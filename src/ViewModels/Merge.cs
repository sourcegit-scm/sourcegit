using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Merge : Popup
    {
        public object Source
        {
            get;
        }

        public string Into
        {
            get;
        }

        public Models.MergeMode Mode
        {
            get => _mode;
            set
            {
                if (SetProperty(ref _mode, value))
                    CanEditMessage = _mode == Models.MergeMode.Default ||
                        _mode == Models.MergeMode.FastForward ||
                        _mode == Models.MergeMode.NoFastForward;
            }
        }

        public bool CanEditMessage
        {
            get => _canEditMessage;
            set => SetProperty(ref _canEditMessage, value);
        }

        public bool Edit
        {
            get;
            set;
        } = false;

        public Merge(Repository repo, Models.Branch source, string into, bool forceFastForward)
        {
            _repo = repo;
            _sourceName = source.FriendlyName;

            Source = source;
            Into = into;
            Mode = forceFastForward ? Models.MergeMode.FastForward : AutoSelectMergeMode();
        }

        public Merge(Repository repo, Models.Commit source, string into)
        {
            _repo = repo;
            _sourceName = source.SHA;

            Source = source;
            Into = into;
            Mode = AutoSelectMergeMode();
        }

        public Merge(Repository repo, Models.Tag source, string into)
        {
            _repo = repo;
            _sourceName = source.Name;

            Source = source;
            Into = into;
            Mode = AutoSelectMergeMode();
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            _repo.ClearCommitMessage();
            ProgressDescription = $"Merging '{_sourceName}' into '{Into}' ...";

            var log = _repo.CreateLog($"Merging '{_sourceName}' into '{Into}'");
            Use(log);

            var succ = await new Commands.Merge(_repo.FullPath, _sourceName, Mode.Arg, _canEditMessage && Edit)
                .Use(log)
                .ExecAsync();

            if (succ)
            {
                var squashMsgFile = Path.Combine(_repo.GitDir, "SQUASH_MSG");
                if (Mode == Models.MergeMode.Squash && File.Exists(squashMsgFile))
                {
                    var msg = await File.ReadAllTextAsync(squashMsgFile);
                    _repo.SetCommitMessage(msg);
                }

                var submodules = await new Commands.QueryUpdatableSubmodules(_repo.FullPath, false).GetResultAsync();
                if (submodules.Count > 0)
                    await new Commands.Submodule(_repo.FullPath)
                        .Use(log)
                        .UpdateAsync(submodules, false, true);
            }

            log.Complete();

            if (succ && _repo.SelectedViewIndex == 0)
            {
                var head = await new Commands.QueryRevisionByRefName(_repo.FullPath, "HEAD").GetResultAsync();
                _repo.NavigateToCommit(head, true);
            }

            return true;
        }

        private Models.MergeMode AutoSelectMergeMode()
        {
            var config = new Commands.Config(_repo.FullPath).Get($"branch.{Into}.mergeoptions");
            var mode = config switch
            {
                "--ff-only" => Models.MergeMode.FastForward,
                "--no-ff" => Models.MergeMode.NoFastForward,
                "--squash" => Models.MergeMode.Squash,
                "--no-commit" or "--no-ff --no-commit" => Models.MergeMode.DontCommit,
                _ => null,
            };

            if (mode != null)
                return mode;

            var preferredMergeModeIdx = _repo.Settings.PreferredMergeMode;
            if (preferredMergeModeIdx < 0 || preferredMergeModeIdx > Models.MergeMode.Supported.Length)
                return Models.MergeMode.Default;

            return Models.MergeMode.Supported[preferredMergeModeIdx];
        }

        private readonly Repository _repo = null;
        private readonly string _sourceName;
        private Models.MergeMode _mode = Models.MergeMode.Default;
        private bool _canEditMessage = true;
    }
}
