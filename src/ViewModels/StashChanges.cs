using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class StashChanges : Popup
    {
        public string Message
        {
            get;
            set;
        }

        public bool HasSelectedFiles
        {
            get;
        }

        public bool IncludeUntracked
        {
            get => _repo.Settings.IncludeUntrackedWhenStash;
            set => _repo.Settings.IncludeUntrackedWhenStash = value;
        }

        public bool OnlyStaged
        {
            get => _repo.Settings.OnlyStagedWhenStash;
            set => _repo.Settings.OnlyStagedWhenStash = value;
        }

        public bool KeepIndex
        {
            get => _repo.Settings.KeepIndexWhenStash;
            set => _repo.Settings.KeepIndexWhenStash = value;
        }

        public bool AutoRestore
        {
            get => _repo.Settings.AutoRestoreAfterStash;
            set => _repo.Settings.AutoRestoreAfterStash = value;
        }

        public StashChanges(Repository repo, List<Models.Change> changes, bool hasSelectedFiles)
        {
            _repo = repo;
            _changes = changes;
            HasSelectedFiles = hasSelectedFiles;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Stash changes ...";

            var log = _repo.CreateLog("Stash Local Changes");
            Use(log);

            return Task.Run(() =>
            {
                var succ = false;

                if (!HasSelectedFiles)
                {
                    if (OnlyStaged)
                    {
                        if (Native.OS.GitVersion >= Models.GitVersions.STASH_PUSH_ONLY_STAGED)
                        {
                            succ = new Commands.Stash(_repo.FullPath).Use(log).PushOnlyStaged(Message, KeepIndex);
                        }
                        else
                        {
                            var staged = new List<Models.Change>();
                            foreach (var c in _changes)
                            {
                                if (c.Index != Models.ChangeState.None && c.Index != Models.ChangeState.Untracked)
                                    staged.Add(c);
                            }

                            succ = StashWithChanges(staged, log);
                        }
                    }
                    else
                    {
                        succ = new Commands.Stash(_repo.FullPath).Use(log).Push(Message, IncludeUntracked, KeepIndex);
                    }
                }
                else
                {
                    succ = StashWithChanges(_changes, log);
                }

                if (AutoRestore && succ)
                    succ = new Commands.Stash(_repo.FullPath).Use(log).Apply("stash@{0}", true);

                log.Complete();
                CallUIThread(() =>
                {
                    _repo.MarkWorkingCopyDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });

                return succ;
            });
        }

        private bool StashWithChanges(List<Models.Change> changes, CommandLog log)
        {
            if (changes.Count == 0)
                return true;

            var succ = false;
            if (Native.OS.GitVersion >= Models.GitVersions.STASH_PUSH_WITH_PATHSPECFILE)
            {
                var paths = new List<string>();
                foreach (var c in changes)
                    paths.Add(c.Path);

                var tmpFile = Path.GetTempFileName();
                File.WriteAllLines(tmpFile, paths);
                succ = new Commands.Stash(_repo.FullPath).Use(log).Push(Message, tmpFile, KeepIndex);
                File.Delete(tmpFile);
            }
            else
            {
                for (int i = 0; i < changes.Count; i += 10)
                {
                    var count = Math.Min(10, changes.Count - i);
                    var step = changes.GetRange(i, count);
                    succ = new Commands.Stash(_repo.FullPath).Use(log).Push(Message, step, KeepIndex);
                    if (!succ)
                        break;
                }
            }

            return succ;
        }

        private readonly Repository _repo = null;
        private readonly List<Models.Change> _changes = null;
    }
}
