using System;
using System.Collections.Generic;

namespace SourceGit.ViewModels
{
    public class RepositoryCommandPaletteCmd
    {
        public string Key { get; set; }
        public Action Action { get; set; }
        public string Label => $"{App.Text(Key)}...";

        public RepositoryCommandPaletteCmd(string key, Action action)
        {
            Key = key;
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

            _cmds.Add(new("Blame", () =>
            {
                var sub = new BlameCommandPalette(_launcher, _repo.FullPath);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new("Checkout", () =>
            {
                var sub = new CheckoutCommandPalette(_launcher, _repo);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new("Compare.WithHead", () =>
            {
                var sub = new CompareCommandPalette(_launcher, _repo, _repo.CurrentBranch);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new("FileHistory", () =>
            {
                var sub = new FileHistoryCommandPalette(_launcher, _repo.FullPath);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new("Merge", () =>
            {
                var sub = new MergeCommandPalette(_launcher, _repo);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new("OpenFile", () =>
            {
                var sub = new OpenFileCommandPalette(_launcher, _repo.FullPath);
                _launcher.OpenCommandPalette(sub);
            }));

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
                    if (cmd.Key.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                        cmd.Label.Contains(_filter, StringComparison.OrdinalIgnoreCase))
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
