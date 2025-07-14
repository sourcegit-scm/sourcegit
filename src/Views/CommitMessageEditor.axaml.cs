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
            Editor.Text = File.ReadAllText(file).ReplaceLineEndings("\n").Trim();
        }

        public void AsBuiltin(string msg, Action<string> onSave)
        {
            _onSave = onSave;
            _shouldExitApp = false;
            Editor.Text = msg.Trim();
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
