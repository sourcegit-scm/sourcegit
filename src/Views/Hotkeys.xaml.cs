using System.Collections.Generic;
using System.Windows;

namespace SourceGit.Views {

    /// <summary>
    ///     快捷键说明
    /// </summary>
    public partial class Hotkeys : Controls.Window {

        public class Keymap {
            public string Key { get; set; }
            public string Desc { get; set; }
            public Keymap(string k, string d) { Key = k; Desc = App.Text($"Hotkeys.{d}"); }
        }

        public Hotkeys() {
            InitializeComponent();

            container.ItemsSource = new List<Keymap>() {
                new Keymap("CTRL + T", "NewTab"),
                new Keymap("CTRL + W", "CloseTab"),
                new Keymap("CTRL + TAB", "NextTab"),
                new Keymap("CTRL + [1-9]", "SwitchTo"),
                new Keymap("CTRL + F", "Search"),
                new Keymap("F5", "Refresh"),
                new Keymap("SPACE", "ToggleStage"),
            };
        }

        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
