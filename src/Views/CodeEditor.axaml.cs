using System;
using System.IO;
using System.Text;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class CodeEditor : ChromelessWindow
    {
        public CodeEditor()
        {
            DataContext = this;
            InitializeComponent();
        }

        public CodeEditor(string file)
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

        private void BeginMoveWindow(object sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Environment.Exit(-1);
        }

        private void SaveAndClose(object sender, RoutedEventArgs e)
        {
            File.WriteAllText(_file, Editor.Text);
            Environment.Exit(0);
        }

        private string _file = string.Empty;
    }
}
