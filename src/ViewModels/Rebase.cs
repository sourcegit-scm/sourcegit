using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace SourceGit.ViewModels
{
    public enum RebaseTestingState
    {
        Disabled = 0,
        Testing,
        WillCauseConflicts,
        UnknownError,
        NoConflicts,
    }

    public class Rebase : Popup
    {
        public Models.Branch Current
        {
            get;
            private set;
        }

        public object On
        {
            get;
            private set;
        }

        public bool AutoStash
        {
            get;
            set;
        }

        public bool NoVerify
        {
            get;
            set;
        }

        public RebaseTestingState TestingState
        {
            get => _testingState;
            private set => SetProperty(ref _testingState, value);
        }

        public Rebase(Repository repo, Models.Branch current, Models.Branch on)
        {
            _repo = repo;
            _revision = on.Head;
            Current = current;
            On = on;
            AutoStash = true;

            Test();
        }

        public Rebase(Repository repo, Models.Branch current, Models.Commit on)
        {
            _repo = repo;
            _revision = on.SHA;
            Current = current;
            On = on;
            AutoStash = true;

            Test();
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            _repo.ClearCommitMessage();
            ProgressDescription = "Rebasing ...";

            var log = _repo.CreateLog("Rebase");
            Use(log);

            await new Commands.Rebase(_repo.FullPath, _revision, AutoStash, NoVerify)
                .Use(log)
                .ExecAsync();

            log.Complete();
            return true;
        }

        private void Test()
        {
            if (Native.OS.GitVersion < Models.GitVersions.REPLAY)
                return;

            var head = Current.Head;
            TestingState = RebaseTestingState.Testing;
            Task.Run(async () =>
            {
                var mergeBase = await new Commands.MergeBase(_repo.FullPath, head, _revision)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                if (string.IsNullOrEmpty(mergeBase))
                {
                    Dispatcher.UIThread.Post(() => TestingState = RebaseTestingState.UnknownError);
                    return;
                }
                else if (head.Equals(mergeBase, StringComparison.Ordinal))
                {
                    Dispatcher.UIThread.Post(() => TestingState = RebaseTestingState.NoConflicts);
                    return;
                }

                var exitCode = await new Commands.Replay(_repo.FullPath, _revision, $"{mergeBase}..{head}")
                    .GetExitCodeAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() => TestingState = exitCode switch
                {
                    0 => RebaseTestingState.NoConflicts,
                    1 => RebaseTestingState.WillCauseConflicts,
                    _ => RebaseTestingState.UnknownError,
                });
            });
        }

        private readonly Repository _repo;
        private readonly string _revision;
        private RebaseTestingState _testingState = RebaseTestingState.Disabled;
    }
}
