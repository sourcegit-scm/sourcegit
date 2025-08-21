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

        public InteractiveRebaseItem(Models.Commit c, string message, bool canSquashOrFixup)
        {
            Commit = c;
            FullMessage = message;
            CanSquashOrFixup = canSquashOrFixup;
        }

        private Models.InteractiveRebaseAction _action = Models.InteractiveRebaseAction.Pick;
        private string _subject;
        private string _fullMessage;
        private bool _canSquashOrFixup = true;
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

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public AvaloniaList<InteractiveRebaseItem> Items
        {
            get;
        } = [];

        public InteractiveRebaseItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                    DetailContext.Commit = value?.Commit;
            }
        }

        public CommitDetail DetailContext
        {
            get;
        }

        public InteractiveRebase(Repository repo, Models.Commit on, InteractiveRebasePrefill prefill = null)
        {
            _repo = repo;
            Current = repo.CurrentBranch;
            On = on;
            IsLoading = true;
            DetailContext = new CommitDetail(repo, false);

            Task.Run(async () =>
            {
                var commits = await new Commands.QueryCommitsForInteractiveRebase(_repo.FullPath, on.SHA)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                var list = new List<InteractiveRebaseItem>();
                for (var i = 0; i < commits.Count; i++)
                {
                    var c = commits[i];
                    list.Add(new InteractiveRebaseItem(c.Commit, c.Message, i < commits.Count - 1));
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
                    SelectedItem = selected;
                    IsLoading = false;
                });
            });
        }

        public void MoveItemUp(InteractiveRebaseItem item)
        {
            var idx = Items.IndexOf(item);
            if (idx > 0)
            {
                var prev = Items[idx - 1];
                Items.RemoveAt(idx - 1);
                Items.Insert(idx, prev);
                SelectedItem = item;
                UpdateItems();
            }
        }

        public void MoveItemDown(InteractiveRebaseItem item)
        {
            var idx = Items.IndexOf(item);
            if (idx < Items.Count - 1)
            {
                var next = Items[idx + 1];
                Items.RemoveAt(idx + 1);
                Items.Insert(idx, next);
                SelectedItem = item;
                UpdateItems();
            }
        }

        public void ChangeAction(InteractiveRebaseItem item, Models.InteractiveRebaseAction action)
        {
            if (!item.CanSquashOrFixup)
            {
                if (action == Models.InteractiveRebaseAction.Squash || action == Models.InteractiveRebaseAction.Fixup)
                    return;
            }

            item.Action = action;
            UpdateItems();
        }

        public async Task<bool> Start()
        {
            _repo.SetWatcherEnabled(false);

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
            _repo.SetWatcherEnabled(true);
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
        private InteractiveRebaseItem _selectedItem = null;
    }
}
