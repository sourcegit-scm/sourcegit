using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace SourceGit.Views {
    public partial class StashesPage : UserControl {
        public StashesPage() {
            InitializeComponent();
        }

        protected override void OnUnloaded(RoutedEventArgs e) {
            base.OnUnloaded(e);
            GC.Collect();
        }
    }
}
