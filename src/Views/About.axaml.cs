using System.Reflection;

using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class About : ChromelessWindow
    {
        public string Version
        {
            get;
            private set;
        }

        public About()
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            if (ver != null)
                Version = $"{ver.Major}.{ver.Minor}";

            DataContext = this;
            InitializeComponent();
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close();
        }

        private void OnVisitAvaloniaUI(object _, PointerPressedEventArgs e)
        {
            Native.OS.OpenBrowser("https://www.avaloniaui.net/");
            e.Handled = true;
        }

        private void OnVisitAvaloniaEdit(object _, PointerPressedEventArgs e)
        {
            Native.OS.OpenBrowser("https://github.com/AvaloniaUI/AvaloniaEdit");
            e.Handled = true;
        }

        private void OnVisitJetBrainsMonoFont(object _, PointerPressedEventArgs e)
        {
            Native.OS.OpenBrowser("https://www.jetbrains.com/lp/mono/");
            e.Handled = true;
        }

        private void OnVisitSourceCode(object _, PointerPressedEventArgs e)
        {
            Native.OS.OpenBrowser("https://github.com/sourcegit-scm/sourcegit");
            e.Handled = true;
        }
    }
}
