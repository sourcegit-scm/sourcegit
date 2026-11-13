using System;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ProjectRename : ChromelessWindow
    {
        public ProjectRename()
        {
            InitializeComponent();
            Opened += (_, _) =>
            {
                TxtName.Focus();
                TxtName.SelectAll();
            };
        }

        public void SetName(string name)
        {
            TxtName.Text = name ?? string.Empty;
        }

        private void OnSure(object sender, RoutedEventArgs e)
        {
            var result = (TxtName.Text ?? string.Empty).Trim();
            Close(result.Length == 0 ? null : result);
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
