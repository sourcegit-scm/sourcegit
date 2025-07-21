using System;
using System.IO;

using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class CommitMessageEditor : ChromelessWindow
    {
        public CommitMessageEditor()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        public void AsStandalone(string file)
        {
            _onSave = msg => File.WriteAllText(file, msg);
            _shouldExitApp = true;

            var content = File.ReadAllText(file).ReplaceLineEndings("\n").Trim();
            var parts = content.Split('\n', 2);
            Editor.SubjectEditor.Text = parts[0];
            if (parts.Length > 1)
                Editor.DescriptionEditor.Text = parts[1].Trim();
        }

        public void AsBuiltin(string msg, Action<string> onSave)
        {
            _onSave = onSave;
            _shouldExitApp = false;

            var parts = msg.Split('\n', 2);
            Editor.SubjectEditor.Text = parts[0];
            if (parts.Length > 1)
                Editor.DescriptionEditor.Text = parts[1].Trim();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_shouldExitApp)
                App.Quit(_exitCode);
        }

        private void SaveAndClose(object _1, RoutedEventArgs _2)
        {
            _onSave?.Invoke(Editor.Text);
            _exitCode = 0;
            Close();
        }

        private Action<string> _onSave = null;
        private bool _shouldExitApp = true;
        private int _exitCode = -1;
    }
}
