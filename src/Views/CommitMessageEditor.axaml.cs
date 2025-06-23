using System;
using System.IO;

using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class CommitMessageEditor : ChromelessWindow
    {
        public CommitMessageEditor()
        {
            InitializeComponent();
        }

        public void AsStandalone(string file)
        {
            _onSave = msg => File.WriteAllText(file, msg);
            _shouldExitApp = true;

            var content = File.ReadAllText(file).ReplaceLineEndings("\n").Trim();
            var firstLineEnd = content.IndexOf('\n', StringComparison.Ordinal);
            if (firstLineEnd == -1)
            {
                Editor.SubjectEditor.Text = content;
            }
            else
            {
                Editor.SubjectEditor.Text = content.Substring(0, firstLineEnd);
                Editor.DescriptionEditor.Text = content.Substring(firstLineEnd + 1).Trim();
            }
        }

        public void AsBuiltin(string msg, Action<string> onSave)
        {
            _onSave = onSave;
            _shouldExitApp = false;

            var firstLineEnd = msg.IndexOf('\n', StringComparison.Ordinal);
            if (firstLineEnd == -1)
            {
                Editor.SubjectEditor.Text = msg;
            }
            else
            {
                Editor.SubjectEditor.Text = msg.Substring(0, firstLineEnd);
                Editor.DescriptionEditor.Text = msg.Substring(firstLineEnd + 1).Trim();
            }
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
