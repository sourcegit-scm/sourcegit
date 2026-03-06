using System;
using System.Collections.Generic;

namespace SourceGit.ViewModels
{
    public class RepositoryCommandPaletteCmd
    {
        public string Label { get; set; }
        public string Keyword { get; set; }
        public string Icon { get; set; }
        public Action Action { get; set; }

        public RepositoryCommandPaletteCmd(string label, string keyword, string icon, Action action)
        {
            Label = label;
            Keyword = keyword;
            Icon = icon;
            Action = action;
        }
    }

    public class RepositoryCommandPalette : ICommandPalette
    {
        public List<RepositoryCommandPaletteCmd> VisibleCmds
        {
            get => _visibleCmds;
            private set => SetProperty(ref _visibleCmds, value);
        }

        public RepositoryCommandPaletteCmd SelectedCmd
        {
            get => _selectedCmd;
            set => SetProperty(ref _selectedCmd, value);
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                    UpdateVisible();
            }
        }

        public RepositoryCommandPalette(Launcher launcher, Repository repo)
        {
            _launcher = launcher;
            _repo = repo;

            _cmds.Add(new($"{App.Text("Blame")}...", "blame", "Blame", () =>
            {
                var sub = new BlameCommandPalette(_launcher, _repo.FullPath);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new($"{App.Text("Checkout")}...", "checkout", "Check", () =>
            {
                var sub = new CheckoutCommandPalette(_launcher, _repo);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new($"{App.Text("Compare.WithHead")}...", "compare", "Compare", () =>
            {
                var sub = new CompareCommandPalette(_launcher, _repo, _repo.CurrentBranch);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new($"{App.Text("FileHistory")}...", "history", "Histories", () =>
            {
                var sub = new FileHistoryCommandPalette(_launcher, _repo.FullPath);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new($"{App.Text("Merge")}...", "merge", "Merge", () =>
            {
                var sub = new MergeCommandPalette(_launcher, _repo);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new($"{App.Text("OpenFile")}...", "open", "OpenWith", () =>
            {
                var sub = new OpenFileCommandPalette(_launcher, _repo.FullPath);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new($"{App.Text("Repository.NewBranch")}...", "create branch", "Branch.Add", () =>
            {
                var repo = _repo;
                _launcher.CancelCommandPalette();
                repo.CreateNewBranch();
            }));

            _cmds.Add(new($"{App.Text("CreateTag.Title")}...", "create tag", "Tag.Add", () =>
            {
                var repo = _repo;
                _launcher.CancelCommandPalette();
                repo.CreateNewTag();
            }));

            _cmds.Add(new($"{App.Text("Fetch")}...", "fetch", "Fetch", async () =>
            {
                var repo = _repo;
                _launcher.CancelCommandPalette();
                await repo.FetchAsync(false);
            }));

            _cmds.Add(new($"{App.Text("Pull.Title")}...", "pull", "Pull", async () =>
            {
                var repo = _repo;
                _launcher.CancelCommandPalette();
                await repo.PullAsync(false);
            }));

            _cmds.Add(new($"{App.Text("Push")}...", "push", "Push", async () =>
            {
                var repo = _repo;
                _launcher.CancelCommandPalette();
                await repo.PushAsync(false);
            }));

            _cmds.Add(new($"{App.Text("Stash.Title")}...", "stash", "Stashes.Add", async () =>
            {
                var repo = _repo;
                _launcher.CancelCommandPalette();
                await repo.StashAllAsync(false);
            }));

            _cmds.Add(new($"{App.Text("Apply.Title")}...", "apply", "Diff", () =>
            {
                var repo = _repo;
                _launcher.CancelCommandPalette();
                repo.ApplyPatch();
            }));

            _cmds.Add(new($"{App.Text("Configure")}...", "configure", "Settings", async () =>
            {
                var repo = _repo;
                _launcher.CancelCommandPalette();
                await App.ShowDialog(new RepositoryConfigure(repo));
            }));

            _cmds.Sort((l, r) => l.Label.CompareTo(r.Label));
            _visibleCmds = _cmds;
            _selectedCmd = _cmds[0];
        }

        public override void Cleanup()
        {
            _launcher = null;
            _repo = null;
            _cmds.Clear();
            _visibleCmds.Clear();
            _selectedCmd = null;
            _filter = null;
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public void Exec()
        {
            if (_selectedCmd != null)
                _selectedCmd.Action?.Invoke();
            else
                _launcher?.CancelCommandPalette();
        }

        private void UpdateVisible()
        {
            if (string.IsNullOrEmpty(_filter))
            {
                VisibleCmds = _cmds;
            }
            else
            {
                var visible = new List<RepositoryCommandPaletteCmd>();

                foreach (var cmd in _cmds)
                {
                    if (cmd.Label.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                        cmd.Keyword.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(cmd);
                }

                var autoSelected = _selectedCmd;
                if (!visible.Contains(_selectedCmd))
                    autoSelected = visible.Count > 0 ? visible[0] : null;

                VisibleCmds = visible;
                SelectedCmd = autoSelected;
            }
        }

        private Launcher _launcher = null;
        private Repository _repo = null;
        private List<RepositoryCommandPaletteCmd> _cmds = [];
        private List<RepositoryCommandPaletteCmd> _visibleCmds = [];
        private RepositoryCommandPaletteCmd _selectedCmd = null;
        private string _filter;
    }
}
