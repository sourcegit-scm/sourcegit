using System;
using System.Reflection;

using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class About : ChromelessWindow
    {
        public About()
        {
            CloseOnESC = true;
            InitializeComponent();

            var assembly = Assembly.GetExecutingAssembly();
            var ver = assembly.GetName().Version;
            if (ver != null)
                TxtVersion.Text = $"{ver.Major}.{ver.Minor:D2}";

            var meta = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            foreach (var attr in meta)
            {
                if (attr.Key.Equals("BuildDate", StringComparison.OrdinalIgnoreCase) && DateTime.TryParse(attr.Value, out var date))
                {
                    TxtReleaseDate.Text = App.Text("About.ReleaseDate", Models.DateTimeFormat.Format(date, true));
                    break;
                }
            }

            var informationVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (informationVersion != null)
            {
                var infoVer = informationVersion.InformationalVersion;
                var idx = infoVer.IndexOf('+');
                if (idx > 0 && infoVer.Length > idx + 11)
                {
                    TxtGitSourceRevision.Text = infoVer.Substring(idx + 1, 10);
                    PnlGitSourceRevision.IsVisible = true;
                }
            }

            var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
            if (copyright != null)
                TxtCopyright.Text = copyright.Copyright;
        }

        private void OnVisitReleaseNotes(object _, RoutedEventArgs e)
        {
            Native.OS.OpenBrowser($"https://github.com/sourcegit-scm/sourcegit/releases/tag/v{TxtVersion.Text}");
            e.Handled = true;
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
