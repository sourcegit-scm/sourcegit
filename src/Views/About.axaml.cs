using System.Reflection;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class About : ChromelessWindow
    {
        public About()
        {
            InitializeComponent();

            var assembly = Assembly.GetExecutingAssembly();
            var ver = assembly.GetName().Version;
            if (ver != null)
                TxtVersion.Text = $"{ver.Major}.{ver.Minor:D2}";

            var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
            if (copyright != null)
                TxtCopyright.Text = copyright.Copyright;
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

        private void OnVisitLiveCharts2(object _, PointerPressedEventArgs e)
        {
            Native.OS.OpenBrowser("https://livecharts.dev/");
            e.Handled = true;
        }

        private void OnVisitSourceCode(object _, PointerPressedEventArgs e)
        {
            Native.OS.OpenBrowser("https://github.com/sourcegit-scm/sourcegit");
            e.Handled = true;
        }
    }
}
