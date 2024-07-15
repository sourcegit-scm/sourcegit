using System.IO;

using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class StandaloneCommitMessageEditor : ChromelessWindow
    {
        public StandaloneCommitMessageEditor()
        {
            _file = string.Empty;
            DataContext = this;
            InitializeComponent();
        }

        public StandaloneCommitMessageEditor(string file)
        {
            _file = file;
            DataContext = this;
            InitializeComponent();

            var content = File.ReadAllText(file).ReplaceLineEndings("\n");
            var firstLineEnd = content.IndexOf('\n');
            if (firstLineEnd == -1)
            {
                Editor.SubjectEditor.Text = content;
            }
            else
            {
                Editor.SubjectEditor.Text = content.Substring(0, firstLineEnd);
                Editor.DescriptionEditor.Text = content.Substring(firstLineEnd + 1);
            }
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            App.Quit(-1);
        }

        private void SaveAndClose(object _1, RoutedEventArgs _2)
        {
            File.WriteAllText(_file, Editor.Text);
            App.Quit(0);
        }

        private readonly string _file;
    }
}
