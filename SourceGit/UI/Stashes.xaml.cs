using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Stashes viewer.
    /// </summary>
    public partial class Stashes : UserControl {
        private Git.Repository repo = null;
        private string selectedStash = null;

        /// <summary>
        ///     File tree node.
        /// </summary>
        public class Node {
            public string FilePath { get; set; } = "";
            public string OriginalPath { get; set; } = "";
            public string Name { get; set; } = "";
            public bool IsFile { get; set; } = false;
            public bool IsNodeExpanded { get; set; } = true;
            public Git.Change.Status Status { get; set; } = Git.Change.Status.None;
            public List<Node> Children { get; set; } = new List<Node>();
        }

        /// <summary>
        ///     Constructor.
        /// </summary>
        public Stashes() {
            InitializeComponent();
        }

        /// <summary>
        ///     Cleanup
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cleanup(object sender, RoutedEventArgs e) {
            stashList.ItemsSource = null;
            changeList.ItemsSource = null;
            diff.Reset();
        }

        /// <summary>
        ///     Set data.
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="stashes"></param>
        public void SetData(Git.Repository opened, List<Git.Stash> stashes) {
            repo = opened;
            selectedStash = null;
            stashList.ItemsSource = stashes;
            changeList.ItemsSource = null;
            diff.Reset();
        }

        /// <summary>
        ///     Stash list selection changed event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StashSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count != 1) return;

            var stash = e.AddedItems[0] as Git.Stash;
            if (stash == null) return;

            selectedStash = stash.SHA;
            diff.Reset();
            changeList.ItemsSource = stash.GetChanges(repo);
        }

        /// <summary>
        ///     File selection changed in TreeView.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count != 1) return;

            var change = e.AddedItems[0] as Git.Change;
            if (change == null) return;

            diff.Diff(repo, new DiffViewer.Option() {
                RevisionRange = new string[] { $"{selectedStash}^", selectedStash },
                Path = change.Path,
                OrgPath = change.OriginalPath
            });
        }

        /// <summary>
        ///     Stash context menu.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ev"></param>
        private void StashContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var stash = (sender as ListViewItem).DataContext as Git.Stash;
            if (stash == null) return;

            var apply = new MenuItem();
            apply.Header = "Apply";
            apply.Click += (o, e) => stash.Apply(repo);

            var pop = new MenuItem();
            pop.Header = "Pop";
            pop.Click += (o, e) => stash.Pop(repo);

            var delete = new MenuItem();
            delete.Header = "Drop";
            delete.Click += (o, e) => stash.Drop(repo);

            var menu = new ContextMenu();
            menu.Items.Add(apply);
            menu.Items.Add(pop);
            menu.Items.Add(delete);
            menu.IsOpen = true;
            ev.Handled = true;
        }
    }
}
