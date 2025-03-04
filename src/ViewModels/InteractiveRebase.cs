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

        public Models.InteractiveRebaseAction Action
        {
            get => _action;
            private set => SetProperty(ref _action, value);
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

        public InteractiveRebaseItem(Models.Commit c, string message)
        {
            Commit = c;
            FullMessage = message;
        }

        public void SetAction(object param)
        {
            Action = (Models.InteractiveRebaseAction)param;
        }

        private Models.InteractiveRebaseAction _action = Models.InteractiveRebaseAction.Pick;
        private string _subject;
        private string _fullMessage;
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

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public AvaloniaList<InteractiveRebaseItem> Items
        {
            get;
            private set;
        } = new AvaloniaList<InteractiveRebaseItem>();

        public InteractiveRebaseItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                    DetailContext.Commit = value != null ? value.Commit : null;
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
            DetailContext = new CommitDetail(repo);

            Task.Run(() =>
            {
                var commits = new Commands.QueryCommitsForInteractiveRebase(repoPath, on.SHA).Result();
                var list = new List<InteractiveRebaseItem>();

                foreach (var c in commits)
                    list.Add(new InteractiveRebaseItem(c.Commit, c.Message));

                Dispatcher.UIThread.Invoke(() =>
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
            }
        }

        public Task<bool> Start()
        {
            _repo.SetWatcherEnabled(false);

            var saveFile = Path.Combine(_repo.GitDir, "sourcegit_rebase_jobs.json");
            var collection = new Models.InteractiveRebaseJobCollection();
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
            File.WriteAllText(saveFile, JsonSerializer.Serialize(collection, JsonCodeGen.Default.InteractiveRebaseJobCollection));

            return Task.Run(() =>
            {
                var succ = new Commands.InteractiveRebase(_repo.FullPath, On.SHA).Exec();
                if (succ)
                    File.Delete(saveFile);

                Dispatcher.UIThread.Invoke(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private Repository _repo = null;
        private bool _isLoading = false;
        private InteractiveRebaseItem _selectedItem = null;
    }
}
