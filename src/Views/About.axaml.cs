using System.Reflection;
using Avalonia.Interactivity;

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

        private void OnVisitWebsite(object _, RoutedEventArgs e)
        {
            Native.OS.OpenBrowser("https://sourcegit-scm.github.io/");
            e.Handled = true;
        }

        private void OnVisitSourceCode(object _, RoutedEventArgs e)
        {
            Native.OS.OpenBrowser("https://github.com/sourcegit-scm/sourcegit");
            e.Handled = true;
        }
    }
}
