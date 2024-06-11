using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class StashesPage : ObservableObject
    {
        public int Count
        {
            get => _stashes == null ? 0 : _stashes.Count;
        }

        public List<Models.Stash> Stashes
        {
            get => _stashes;
            set
            {
                if (SetProperty(ref _stashes, value))
                {
                    SelectedStash = null;
                }
            }
        }

        public Models.Stash SelectedStash
        {
            get => _selectedStash;
            set
            {
                if (SetProperty(ref _selectedStash, value))
                {
                    if (value == null)
                    {
                        Changes = null;
                    }
                    else
                    {
                        Task.Run(() =>
                        {
                            var changes = new Commands.QueryStashChanges(_repo.FullPath, value.SHA).Result();
                            Dispatcher.UIThread.Invoke(() =>
                            {
                                Changes = changes;
                            });
                        });
                    }
                }
            }
        }

        public List<Models.Change> Changes
        {
            get => _changes;
            private set
            {
                if (SetProperty(ref _changes, value))
                {
                    SelectedChange = null;
                }
            }
        }

        public Models.Change SelectedChange
        {
            get => _selectedChange;
            set
            {
                if (SetProperty(ref _selectedChange, value))
                {
                    if (value == null)
                    {
                        DiffContext = null;
                    }
                    else
                    {
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption($"{_selectedStash.SHA}^", _selectedStash.SHA, value), _diffContext);
                    }
                }
            }
        }

        public DiffContext DiffContext
        {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        public StashesPage(Repository repo)
        {
            _repo = repo;
        }

        public void Cleanup()
        {
            _repo = null;
            if (_stashes != null)
                _stashes.Clear();
            _selectedStash = null;
            if (_changes != null)
                _changes.Clear();
            _selectedChange = null;
            _diffContext = null;
        }

        public ContextMenu MakeContextMenu(Models.Stash stash)
        {
            if (stash == null)
                return null;

            var apply = new MenuItem();
            apply.Header = App.Text("StashCM.Apply");
            apply.Click += (o, ev) =>
            {
                Task.Run(() => new Commands.Stash(_repo.FullPath).Apply(stash.Name));
                ev.Handled = true;
            };

            var pop = new MenuItem();
            pop.Header = App.Text("StashCM.Pop");
            pop.Click += (o, ev) =>
            {
                Task.Run(() => new Commands.Stash(_repo.FullPath).Pop(stash.Name));
                ev.Handled = true;
            };

            var drop = new MenuItem();
            drop.Header = App.Text("StashCM.Drop");
            drop.Click += (o, ev) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new DropStash(_repo.FullPath, stash));

                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(apply);
            menu.Items.Add(pop);
            menu.Items.Add(drop);
            return menu;
        }

        public void Clear()
        {
            if (PopupHost.CanCreatePopup())
            {
                PopupHost.ShowPopup(new ClearStashes(_repo));
            }
        }

        private Repository _repo = null;
        private List<Models.Stash> _stashes = null;
        private Models.Stash _selectedStash = null;
        private List<Models.Change> _changes = null;
        private Models.Change _selectedChange = null;
        private DiffContext _diffContext = null;
    }
}
