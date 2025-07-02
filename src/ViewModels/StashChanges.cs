﻿using System;
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
            set
            {
                if (_repo.Settings.OnlyStagedWhenStash != value)
                {
                    _repo.Settings.OnlyStagedWhenStash = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ChangesAfterStashing
        {
            get => _repo.Settings.ChangesAfterStashing;
            set => _repo.Settings.ChangesAfterStashing = value;
        }

        public StashChanges(Repository repo, List<Models.Change> changes, bool hasSelectedFiles)
        {
            _repo = repo;
            _changes = changes;
            HasSelectedFiles = hasSelectedFiles;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Stash changes ...";

            var log = _repo.CreateLog("Stash Local Changes");
            Use(log);

            var mode = (DealWithChangesAfterStashing)ChangesAfterStashing;
            var keepIndex = mode == DealWithChangesAfterStashing.KeepIndex;
            bool succ;

            if (!HasSelectedFiles)
            {
                if (OnlyStaged)
                {
                    if (Native.OS.GitVersion >= Models.GitVersions.STASH_PUSH_ONLY_STAGED)
                    {
                        succ = await new Commands.Stash(_repo.FullPath).Use(log).PushOnlyStagedAsync(Message, keepIndex);
                    }
                    else
                    {
                        var staged = new List<Models.Change>();
                        foreach (var c in _changes)
                        {
                            if (c.Index != Models.ChangeState.None && c.Index != Models.ChangeState.Untracked)
                                staged.Add(c);
                        }

                        succ = await StashWithChangesAsync(staged, keepIndex, log);
                    }
                }
                else
                {
                    succ = await new Commands.Stash(_repo.FullPath).Use(log).PushAsync(Message, IncludeUntracked, keepIndex);
                }
            }
            else
            {
                succ = await StashWithChangesAsync(_changes, keepIndex, log);
            }

            if (mode == DealWithChangesAfterStashing.KeepAll && succ)
                succ = await new Commands.Stash(_repo.FullPath).Use(log).ApplyAsync("stash@{0}", true);

            log.Complete();
            await CallUIThreadAsync(() =>
            {
                _repo.MarkWorkingCopyDirtyManually();
                _repo.SetWatcherEnabled(true);
            });

            return succ;
        }

        private async Task<bool> StashWithChangesAsync(List<Models.Change> changes, bool keepIndex, CommandLog log)
        {
            if (changes.Count == 0)
                return true;

            var succ = false;
            if (Native.OS.GitVersion >= Models.GitVersions.STASH_PUSH_WITH_PATHSPECFILE)
            {
                var paths = new List<string>();
                foreach (var c in changes)
                    paths.Add(c.Path);

                var pathSpecFile = Path.GetTempFileName();
                await File.WriteAllLinesAsync(pathSpecFile, paths);
                succ = await new Commands.Stash(_repo.FullPath).Use(log).PushAsync(Message, pathSpecFile, keepIndex);
                File.Delete(pathSpecFile);
            }
            else
            {
                for (int i = 0; i < changes.Count; i += 32)
                {
                    var count = Math.Min(32, changes.Count - i);
                    var step = changes.GetRange(i, count);
                    succ = await new Commands.Stash(_repo.FullPath).Use(log).PushAsync(Message, step, keepIndex);
                    if (!succ)
                        break;
                }
            }

            return succ;
        }

        private enum DealWithChangesAfterStashing
        {
            Discard = 0,
            KeepIndex,
            KeepAll,
        }

        private readonly Repository _repo = null;
        private readonly List<Models.Change> _changes = null;
    }
}
