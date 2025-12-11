using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
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

        public Models.InteractiveRebaseAction Action
        {
            get => _action;
            set => SetProperty(ref _action, value);
        }

        public Models.InteractiveRebasePendingType PendingType
        {
            get => _pendingType;
            set => SetProperty(ref _pendingType, value);
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

        public string OriginalFullMessage
        {
            get;
            set;
        }

        public bool CanSquashOrFixup
        {
            get => _canSquashOrFixup;
            set => SetProperty(ref _canSquashOrFixup, value);
        }

        public bool ShowEditMessageButton
        {
            get => _showEditMessageButton;
            set => SetProperty(ref _showEditMessageButton, value);
        }

        public bool IsFullMessageUsed
        {
            get => _isFullMessageUsed;
            set => SetProperty(ref _isFullMessageUsed, value);
        }

        public Thickness DropDirectionIndicator
        {
            get => _dropDirectionIndicator;
            set => SetProperty(ref _dropDirectionIndicator, value);
        }

        public bool IsMessageUserEdited
        {
            get;
            set;
        } = false;

        public InteractiveRebaseItem(int order, Models.Commit c, string message)
        {
            OriginalOrder = order;
            Commit = c;
            FullMessage = message;
            OriginalFullMessage = message;
        }

        private Models.InteractiveRebaseAction _action = Models.InteractiveRebaseAction.Pick;
        private Models.InteractiveRebasePendingType _pendingType = Models.InteractiveRebasePendingType.None;
        private string _subject;
        private string _fullMessage;
        private bool _canSquashOrFixup = true;
        private bool _showEditMessageButton = false;
        private bool _isFullMessageUsed = true;
        private Thickness _dropDirectionIndicator = new Thickness(0);
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
                    list.Add(new InteractiveRebaseItem(commits.Count - i, c.Commit, c.Message));
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
                    UpdateItems();
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

            InteractiveRebaseItem pending = null;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                var job = new Models.InteractiveRebaseJob()
                {
                    SHA = item.Commit.SHA,
                    Action = item.Action,
                };

                if (pending != null && item.PendingType != Models.InteractiveRebasePendingType.Ignore)
                    job.Message = pending.FullMessage;
                else
                    job.Message = item.FullMessage;

                collection.Jobs.Add(job);

                if (item.PendingType == Models.InteractiveRebasePendingType.Last)
                    pending = null;
                else if (item.PendingType == Models.InteractiveRebasePendingType.Target)
                    pending = item;
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
                    if (item.Action == Models.InteractiveRebaseAction.Squash || item.Action == Models.InteractiveRebaseAction.Fixup)
                        item.Action = Models.InteractiveRebaseAction.Pick;

                    hasValidParent = item.Action != Models.InteractiveRebaseAction.Drop;
                }
            }

            var hasPending = false;
            var pendingMessages = new List<string>();
            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];

                if (item.Action == Models.InteractiveRebaseAction.Drop)
                {
                    item.IsFullMessageUsed = false;
                    item.ShowEditMessageButton = false;
                    item.PendingType = hasPending ? Models.InteractiveRebasePendingType.Ignore : Models.InteractiveRebasePendingType.None;
                    item.FullMessage = item.OriginalFullMessage;
                    item.IsMessageUserEdited = false;
                    continue;
                }

                if (item.Action == Models.InteractiveRebaseAction.Fixup ||
                    item.Action == Models.InteractiveRebaseAction.Squash)
                {
                    item.IsFullMessageUsed = false;
                    item.ShowEditMessageButton = false;
                    item.PendingType = hasPending ? Models.InteractiveRebasePendingType.Pending : Models.InteractiveRebasePendingType.Last;
                    item.FullMessage = item.OriginalFullMessage;
                    item.IsMessageUserEdited = false;

                    if (item.Action == Models.InteractiveRebaseAction.Squash)
                        pendingMessages.Add(item.OriginalFullMessage);

                    hasPending = true;
                    continue;
                }

                if (item.Action == Models.InteractiveRebaseAction.Reword ||
                    item.Action == Models.InteractiveRebaseAction.Edit)
                {
                    var oldPendingType = item.PendingType;
                    item.IsFullMessageUsed = true;
                    item.ShowEditMessageButton = true;
                    item.PendingType = hasPending ? Models.InteractiveRebasePendingType.Target : Models.InteractiveRebasePendingType.None;

                    if (hasPending)
                    {
                        if (!item.IsMessageUserEdited)
                        {
                            var builder = new StringBuilder();
                            builder.Append(item.OriginalFullMessage);
                            for (var j = pendingMessages.Count - 1; j >= 0; j--)
                                builder.Append("\n").Append(pendingMessages[j]);

                            item.FullMessage = builder.ToString();
                        }

                        hasPending = false;
                        pendingMessages.Clear();
                    }
                    else if (oldPendingType == Models.InteractiveRebasePendingType.Target)
                    {
                        if (!item.IsMessageUserEdited)
                            item.FullMessage = item.OriginalFullMessage;
                    }

                    continue;
                }

                if (item.Action == Models.InteractiveRebaseAction.Pick)
                {
                    item.IsFullMessageUsed = true;
                    item.IsMessageUserEdited = false;

                    if (hasPending)
                    {
                        var builder = new StringBuilder();
                        builder.Append(item.OriginalFullMessage);
                        for (var j = pendingMessages.Count - 1; j >= 0; j--)
                            builder.Append("\n").Append(pendingMessages[j]);

                        item.Action = Models.InteractiveRebaseAction.Reword;
                        item.PendingType = Models.InteractiveRebasePendingType.Target;
                        item.ShowEditMessageButton = true;
                        item.FullMessage = builder.ToString();

                        hasPending = false;
                        pendingMessages.Clear();
                    }
                    else
                    {
                        item.PendingType = Models.InteractiveRebasePendingType.None;
                        item.ShowEditMessageButton = false;
                        item.FullMessage = item.OriginalFullMessage;
                    }
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
