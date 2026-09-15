using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public enum ScanErrorResult
    {
        Continue,
        OpenPath,
        Stop,
    }

    public partial class ScanError : ChromelessWindow
    {
        private string _errorPath;

        public ScanError()
        {
            InitializeComponent();
        }

        public void SetData(string path, string errorMessage)
        {
            _errorPath = path;
            Message.Text = App.Text("Welcome.DirectoryTree.ScanError", path, errorMessage);
            BtnContinue.Content = App.Text("Welcome.DirectoryTree.Continue");
            BtnOpenPath.Content = App.Text("Welcome.DirectoryTree.OpenPath");
            BtnStop.Content = App.Text("Welcome.DirectoryTree.Stop");
        }

        private void OnContinue(object _1, RoutedEventArgs _2)
        {
            Close(ScanErrorResult.Continue);
        }

        private void OnOpenPath(object _1, RoutedEventArgs _2)
        {
            if (!string.IsNullOrEmpty(_errorPath))
                Native.OS.OpenInFileManager(_errorPath);
            Close(ScanErrorResult.OpenPath);
        }

        private void OnStop(object _1, RoutedEventArgs _2)
        {
            Close(ScanErrorResult.Stop);
        }
    }
}
