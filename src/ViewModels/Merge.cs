using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace SourceGit.ViewModels
{
    public enum MergeTestingState
    {
        Disabled = 0,
        Testing,
        WillCauseConflicts,
        UnknownError,
        NoConflicts,
    }

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
                    CanEditMessage = _mode == Models.MergeMode.Default || _mode == Models.MergeMode.NoFastForward;
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

        public MergeTestingState TestingState
        {
            get => _testingState;
            private set => SetProperty(ref _testingState, value);
        }

        public Merge(Repository repo, Models.Branch source, string into, bool forceFastForward)
        {
            _repo = repo;
            _sourceName = source.FriendlyName;

            Source = source;
            Into = into;
            Mode = forceFastForward ? Models.MergeMode.FastForward : AutoSelectMergeMode();

            if (!forceFastForward)
                Test();
        }

        public Merge(Repository repo, Models.Commit source, string into)
        {
            _repo = repo;
            _sourceName = source.SHA;

            Source = source;
            Into = into;
            Mode = AutoSelectMergeMode();

            Test();
        }

        public Merge(Repository repo, Models.Tag source, string into)
        {
            _repo = repo;
            _sourceName = source.Name;

            Source = source;
            Into = into;
            Mode = AutoSelectMergeMode();

            Test();
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

                await _repo.AutoUpdateSubmodulesAsync(log);
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

        private void Test()
        {
            if (Native.OS.GitVersion < Models.GitVersions.TESTING_MERGE)
                return;

            TestingState = MergeTestingState.Testing;

            Task.Run(async () =>
            {
                var exitCode = await new Commands.MergeTree(_repo.FullPath, _sourceName, Into)
                    .GetExitCodeAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() => TestingState = exitCode switch
                {
                    0 => MergeTestingState.NoConflicts,
                    1 => MergeTestingState.WillCauseConflicts,
                    _ => MergeTestingState.UnknownError,
                });
            });
        }

        private readonly Repository _repo = null;
        private readonly string _sourceName;
        private Models.MergeMode _mode = Models.MergeMode.Default;
        private bool _canEditMessage = true;
        private MergeTestingState _testingState = MergeTestingState.Disabled;
    }
}
