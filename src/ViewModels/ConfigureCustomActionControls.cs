using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class ConfigureCustomActionControls : ObservableObject
    {
        public AvaloniaList<Models.CustomActionControl> Controls
        {
            get;
        }

        public Models.CustomActionControl Edit
        {
            get => _edit;
            set => SetProperty(ref _edit, value);
        }

        public ConfigureCustomActionControls(AvaloniaList<Models.CustomActionControl> controls)
        {
            Controls = controls;
        }

        public void Add()
        {
            var added = new Models.CustomActionControl()
            {
                Label = "Unnamed",
                Type = Models.CustomActionControlType.TextBox
            };

            Controls.Add(added);
            Edit = added;
        }

        public void Remove()
        {
            if (_edit == null)
                return;

            Controls.Remove(_edit);
            Edit = null;
        }

        public void MoveUp()
        {
            if (_edit == null)
                return;

            var idx = Controls.IndexOf(_edit);
            if (idx > 0)
                Controls.Move(idx - 1, idx);
        }

        public void MoveDown()
        {
            if (_edit == null)
                return;

            var idx = Controls.IndexOf(_edit);
            if (idx < Controls.Count - 1)
                Controls.Move(idx + 1, idx);
        }

        private Models.CustomActionControl _edit;
    }
}
