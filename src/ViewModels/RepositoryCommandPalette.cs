using System;
using System.Collections.Generic;

namespace SourceGit.ViewModels
{
    public class RepositoryCommandPaletteCmd
    {
        public string Label { get; set; }
        public string Keyword { get; set; }
        public string Icon { get; set; }
        public bool CloseBeforeExec { get; set; }
        public Action Action { get; set; }

        public RepositoryCommandPaletteCmd(string labelKey, string keyword, string icon, Action action)
        {
            Label = $"{App.Text(labelKey)}...";
            Keyword = keyword;
            Icon = icon;
            CloseBeforeExec = true;
            Action = action;
        }

        public RepositoryCommandPaletteCmd(string labelKey, string keyword, string icon, ICommandPalette child)
        {
            Label = $"{App.Text(labelKey)}...";
            Keyword = keyword;
            Icon = icon;
            CloseBeforeExec = false;
            Action = () => child.Open();
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

        public RepositoryCommandPalette(Repository repo)
        {
            // Sub-CommandPalettes
            _cmds.Add(new("Blame", "blame", "Blame", new BlameCommandPalette(repo.FullPath)));
            _cmds.Add(new("Checkout", "checkout", "Check", new CheckoutCommandPalette(repo)));
            _cmds.Add(new("Compare", "compare", "Compare", new CompareCommandPalette(repo, null)));
            _cmds.Add(new("FileHistory", "history", "Histories", new FileHistoryCommandPalette(repo.FullPath)));
            _cmds.Add(new("Merge", "merge", "Merge", new MergeCommandPalette(repo)));
            _cmds.Add(new("OpenFile", "open", "OpenWith", new OpenFileCommandPalette(repo.FullPath)));
            _cmds.Add(new("Repository.CustomActions", "custom actions", "Action", new ExecuteCustomActionCommandPalette(repo)));

            // Raw-Actions
            _cmds.Add(new("Repository.NewBranch", "create branch", "Branch.Add", () => repo.CreateNewBranch()));
            _cmds.Add(new("CreateTag.Title", "create tag", "Tag.Add", () => repo.CreateNewTag()));
            _cmds.Add(new("Fetch", "fetch", "Fetch", async () => await repo.FetchAsync(false)));
            _cmds.Add(new("Pull.Title", "pull", "Pull", async () => await repo.PullAsync(false)));
            _cmds.Add(new("Push", "push", "Push", async () => await repo.PushAsync(false)));
            _cmds.Add(new("Stash.Title", "stash", "Stashes.Add", async () => await repo.StashAllAsync(false)));
            _cmds.Add(new("Apply.Title", "apply", "ApplyPatch", () => repo.ApplyPatch()));

            _cmds.Sort((l, r) => l.Label.CompareTo(r.Label));
            _visibleCmds = _cmds;
            _selectedCmd = _cmds[0];
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public void Exec()
        {
            _cmds.Clear();
            _visibleCmds.Clear();

            if (_selectedCmd != null)
            {
                if (_selectedCmd.CloseBeforeExec)
                    Close();

                _selectedCmd.Action?.Invoke();
            }
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

        private List<RepositoryCommandPaletteCmd> _cmds = [];
        private List<RepositoryCommandPaletteCmd> _visibleCmds = [];
        private RepositoryCommandPaletteCmd _selectedCmd = null;
        private string _filter;
    }
}
