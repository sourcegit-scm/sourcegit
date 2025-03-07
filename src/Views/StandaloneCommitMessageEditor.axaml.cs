using System;
using System.IO;

using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class StandaloneCommitMessageEditor : ChromelessWindow
    {
        public StandaloneCommitMessageEditor()
        {
            InitializeComponent();
        }

        public void SetFile(string file)
        {
            _file = file;

            var content = File.ReadAllText(file).ReplaceLineEndings("\n").Trim();
            var firstLineEnd = content.IndexOf('\n');
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

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            App.Quit(_exitCode);
        }

        private void SaveAndClose(object _1, RoutedEventArgs _2)
        {
            if (!string.IsNullOrEmpty(_file))
            {
                File.WriteAllText(_file, Editor.Text);
                _exitCode = 0;
            }

            Close();
        }

        private string _file = string.Empty;
        private int _exitCode = -1;
    }
}
