using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public record InteractiveRebasePrefill(string SHA, Models.InteractiveRebaseAction Action);

    public class InteractiveRebaseItem : ObservableObject
    {
        public int OriginalOrder
        {
            get;
        }

        public Models.Commit Commit
        {
            get;
        }

        public bool CanSquashOrFixup
        {
            get => _canSquashOrFixup;
            set
            {
                if (SetProperty(ref _canSquashOrFixup, value))
                {
                    if (_action == Models.InteractiveRebaseAction.Squash || _action == Models.InteractiveRebaseAction.Fixup)
                        Action = Models.InteractiveRebaseAction.Pick;
                }
            }
        }

        public Models.InteractiveRebaseAction Action
        {
            get => _action;
            set => SetProperty(ref _action, value);
        }

        public string Subject
        {
            get => _subject;
            private set => SetProperty(ref _subject, value);
        }

        public string FullMessage
        {
            get => _fullMessage;
            set
            {
                if (SetProperty(ref _fullMessage, value))
                {
                    var normalized = value.ReplaceLineEndings("\n");
                    var parts = normalized.Split("\n\n", 2);
                    Subject = parts[0].ReplaceLineEndings(" ");
                }
            }
        }

        public bool IsDropBeforeVisible
        {
            get => _isDropBeforeVisible;
            set => SetProperty(ref _isDropBeforeVisible, value);
        }

        public bool IsDropAfterVisible
        {
            get => _isDropAfterVisible;
            set => SetProperty(ref _isDropAfterVisible, value);
        }

        public InteractiveRebaseItem(int order, Models.Commit c, string message, bool canSquashOrFixup)
        {
            OriginalOrder = order;
            Commit = c;
            FullMessage = message;
            CanSquashOrFixup = canSquashOrFixup;
        }

        private Models.InteractiveRebaseAction _action = Models.InteractiveRebaseAction.Pick;
        private string _subject;
        private string _fullMessage;
        private bool _canSquashOrFixup = true;
        private bool _isDropBeforeVisible = false;
        private bool _isDropAfterVisible = false;
    }

    public class InteractiveRebase : ObservableObject
    {
        public Models.Branch Current
        {
            get;
            private set;
        }

        public Models.Commit On
        {
            get;
        }

        public bool AutoStash
        {
            get;
            set;
        } = true;

        public AvaloniaList<Models.IssueTracker> IssueTrackers
        {
            get => _repo.IssueTrackers;
        }

        public string ConventionalTypesOverride
        {
            get => _repo.Settings.ConventionalTypesOverride;
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public AvaloniaList<InteractiveRebaseItem> Items
        {
            get;
        } = [];

        public InteractiveRebaseItem PreSelected
        {
            get => _preSelected;
            private set => SetProperty(ref _preSelected, value);
        }

        public object Detail
        {
            get => _detail;
            private set => SetProperty(ref _detail, value);
        }

        public InteractiveRebase(Repository repo, Models.Commit on, InteractiveRebasePrefill prefill = null)
        {
            _repo = repo;
            _commitDetail = new CommitDetail(repo, null);
            Current = repo.CurrentBranch;
            On = on;
            IsLoading = true;

            Task.Run(async () =>
            {
                var commits = await new Commands.QueryCommitsForInteractiveRebase(_repo.FullPath, on.SHA)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                var list = new List<InteractiveRebaseItem>();
                for (var i = 0; i < commits.Count; i++)
                {
                    var c = commits[i];
                    list.Add(new InteractiveRebaseItem(commits.Count - i, c.Commit, c.Message, i < commits.Count - 1));
                }

                var selected = list.Count > 0 ? list[0] : null;
                if (prefill != null)
                {
                    var item = list.Find(x => x.Commit.SHA.Equals(prefill.SHA, StringComparison.Ordinal));
                    if (item != null)
                    {
                        item.Action = prefill.Action;
                        selected = item;
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    Items.AddRange(list);
                    PreSelected = selected;
                    IsLoading = false;
                });
            });
        }

        public void SelectCommits(List<InteractiveRebaseItem> items)
        {
            if (items.Count == 0)
            {
                Detail = null;
            }
            else if (items.Count == 1)
            {
                _commitDetail.Commit = items[0].Commit;
                Detail = _commitDetail;
            }
            else
            {
                Detail = new Models.Count(items.Count);
            }
        }

        public void ChangeAction(List<InteractiveRebaseItem> selected, Models.InteractiveRebaseAction action)
        {
            if (action == Models.InteractiveRebaseAction.Squash || action == Models.InteractiveRebaseAction.Fixup)
            {
                foreach (var item in selected)
                {
                    if (item.CanSquashOrFixup)
                        item.Action = action;
                }
            }
            else
            {
                foreach (var item in selected)
                    item.Action = action;
            }

            UpdateItems();
        }

        public void Move(List<InteractiveRebaseItem> commits, int index)
        {
            var hashes = new HashSet<string>();
            foreach (var c in commits)
                hashes.Add(c.Commit.SHA);

            var before = new List<InteractiveRebaseItem>();
            var ordered = new List<InteractiveRebaseItem>();
            var after = new List<InteractiveRebaseItem>();

            for (int i = 0; i < index; i++)
            {
                var item = Items[i];
                if (!hashes.Contains(item.Commit.SHA))
                    before.Add(item);
                else
                    ordered.Add(item);
            }

            for (int i = index; i < Items.Count; i++)
            {
                var item = Items[i];
                if (!hashes.Contains(item.Commit.SHA))
                    after.Add(item);
                else
                    ordered.Add(item);
            }

            Items.Clear();
            Items.AddRange(before);
            Items.AddRange(ordered);
            Items.AddRange(after);
            UpdateItems();
        }

        public async Task<bool> Start()
        {
            using var lockWatcher = _repo.LockWatcher();

            var saveFile = Path.Combine(_repo.GitDir, "sourcegit.interactive_rebase");
            var collection = new Models.InteractiveRebaseJobCollection();
            collection.OrigHead = _repo.CurrentBranch.Head;
            collection.Onto = On.SHA;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                collection.Jobs.Add(new Models.InteractiveRebaseJob()
                {
                    SHA = item.Commit.SHA,
                    Action = item.Action,
                    Message = item.FullMessage,
                });
            }
            await using (var stream = File.Create(saveFile))
            {
                await JsonSerializer.SerializeAsync(stream, collection, JsonCodeGen.Default.InteractiveRebaseJobCollection);
            }

            var log = _repo.CreateLog("Interactive Rebase");
            var succ = await new Commands.InteractiveRebase(_repo.FullPath, On.SHA, AutoStash)
                .Use(log)
                .ExecAsync();

            log.Complete();
            return succ;
        }

        private void UpdateItems()
        {
            if (Items.Count == 0)
                return;

            var hasValidParent = false;
            for (var i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                if (hasValidParent)
                {
                    item.CanSquashOrFixup = true;
                }
                else
                {
                    item.CanSquashOrFixup = false;
                    hasValidParent = item.Action != Models.InteractiveRebaseAction.Drop;
                }
            }
        }

        private Repository _repo = null;
        private bool _isLoading = false;
        private InteractiveRebaseItem _preSelected = null;
        private object _detail = null;
        private CommitDetail _commitDetail = null;
    }
}
