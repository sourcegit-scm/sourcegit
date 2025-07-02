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
    public class InteractiveRebaseItem : ObservableObject
    {
        public Models.Commit Commit
        {
            get;
            private set;
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
                    var idx = normalized.IndexOf("\n\n", StringComparison.Ordinal);
                    if (idx > 0)
                        Subject = normalized.Substring(0, idx).ReplaceLineEndings(" ");
                    else
                        Subject = value.ReplaceLineEndings(" ");
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
            private set;
        }

        public AvaloniaList<Models.IssueTrackerRule> IssueTrackerRules
        {
            get => _repo.Settings.IssueTrackerRules;
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public AvaloniaList<InteractiveRebaseItem> Items
        {
            get;
            private set;
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
            private set;
        }

        public InteractiveRebase(Repository repo, Models.Branch current, Models.Commit on)
        {
            var repoPath = repo.FullPath;
            _repo = repo;

            Current = current;
            On = on;
            IsLoading = true;
            DetailContext = new CommitDetail(repo, false);

            Task.Run(async () =>
            {
                var commits = await new Commands.QueryCommitsForInteractiveRebase(repoPath, on.SHA).ResultAsync();
                var list = new List<InteractiveRebaseItem>();

                for (var i = 0; i < commits.Count; i++)
                {
                    var c = commits[i];
                    list.Add(new InteractiveRebaseItem(c.Commit, c.Message, i < commits.Count - 1));
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Items.AddRange(list);
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

        public Task<bool> Start()
        {
            _repo.SetWatcherEnabled(false);

            var saveFile = Path.Combine(_repo.GitDir, "sourcegit_rebase_jobs.json");
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
            using (var stream = File.Create(saveFile))
            {
                JsonSerializer.Serialize(stream, collection, JsonCodeGen.Default.InteractiveRebaseJobCollection);
            }

            var log = _repo.CreateLog("Interactive Rebase");
            return Task.Run(async () =>
            {
                var succ = await new Commands.InteractiveRebase(_repo.FullPath, On.SHA).Use(log).ExecAsync();
                log.Complete();
                await Dispatcher.UIThread.InvokeAsync(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
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
