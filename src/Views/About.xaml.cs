using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace SourceGit.Views {

    /// <summary>
    ///     关于对话框
    /// </summary>
    public partial class About : Controls.Window {

        public class Keymap {
            public string Key { get; set; }
            public string Desc { get; set; }
            public Keymap(string k, string d) { Key = k; Desc = App.Text($"Hotkeys.{d}"); }
        }

        public About() {
            InitializeComponent();

            var asm = Assembly.GetExecutingAssembly().GetName();
            version.Text = $"VERSION : v{asm.Version.Major}.{asm.Version.Minor}";

            hotkeys.ItemsSource = new List<Keymap>() {
                new Keymap("CTRL + T", "NewTab"),
                new Keymap("CTRL + W", "CloseTab"),
                new Keymap("CTRL + TAB", "NextTab"),
                new Keymap("CTRL + [1-9]", "SwitchTo"),
                new Keymap("CTRL + F", "Search"),
                new Keymap("F5", "Refresh"),
                new Keymap("SPACE", "ToggleStage"),
                new Keymap("ESC", "CancelPopup"),
            };
        }

        private void OnRequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) {
            var info = new ProcessStartInfo("cmd", $"/c start {e.Uri.AbsoluteUri}");
            info.CreateNoWindow = true;
            Process.Start(info);
        }

        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
