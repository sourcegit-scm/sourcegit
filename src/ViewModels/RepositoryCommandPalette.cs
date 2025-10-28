using System;
using System.Collections.Generic;

namespace SourceGit.ViewModels
{
    public record RepositoryCommandPaletteCmd(string Name, string Label, bool AutoCloseCommandPalette, Action Action);

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

            _cmds.Add(new("File History", App.Text("FileHistory") + "...", false, () =>
            {
                var sub = new FileHistoryCommandPalette(_launcher, _repo.FullPath);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new("Blame", App.Text("Blame") + "...", false, () =>
            {
                var sub = new BlameCommandPalette(_launcher, _repo.FullPath);
                _launcher.OpenCommandPalette(sub);
            }));

            _cmds.Add(new("Merge", App.Text("Merge") + "...", false, () =>
            {
                var sub = new MergeCommandPalette(_launcher, _repo);
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
            if (_selectedCmd == null)
            {
                _launcher?.CancelCommandPalette();
                return;
            }

            var autoClose = _selectedCmd.AutoCloseCommandPalette;
            _selectedCmd.Action?.Invoke();

            if (autoClose)
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
                    if (cmd.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
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
