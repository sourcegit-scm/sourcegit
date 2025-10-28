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

            _cmds.Add(new("FileHistory", () =>
            {
                var sub = new FileHistoryCommandPalette(_launcher, _repo.FullPath);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new("Blame", () =>
            {
                var sub = new BlameCommandPalette(_launcher, _repo.FullPath);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new("Merge", () =>
            {
                var sub = new MergeCommandPalette(_launcher, _repo);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new("BranchCompare", () =>
            {
                var sub = new BranchCompareCommandPalette(_launcher, _repo);
                _launcher.OpenCommandPalette(sub);
            }));

            _visibleCmds = _cmds;
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
                foreach (var cmd in VisibleCmds)
                {
                    if (cmd.Key.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                        cmd.Label.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(cmd);
                }
                VisibleCmds = visible;
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
