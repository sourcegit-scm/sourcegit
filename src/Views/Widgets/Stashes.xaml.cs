using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views.Widgets {

    /// <summary>
    ///     贮藏管理
    /// </summary>
    public partial class Stashes : UserControl {
        private string repo = null;
        private string selected = null;
        private bool isLFSEnabled = false;

        public Stashes(string repo) {
            this.repo = repo;
            this.isLFSEnabled = new Commands.LFS(repo).IsEnabled();
            InitializeComponent();
        }

        public void SetData(List<Models.Stash> data) {
            stashList.ItemsSource = data;
            changeList.ItemsSource = null;
        }

        private async void OnStashSelectionChanged(object sender, SelectionChangedEventArgs e) {
            changeList.ItemsSource = null;
            selected = null;

            var stash = stashList.SelectedItem as Models.Stash;
            if (stash == null) return;

            selected = stash.SHA;
            diffViewer.Reset();

            var changes = await Task.Run(() => new Commands.StashChanges(repo, selected).Result());
            changeList.ItemsSource = changes;
        }

        private void OnChangeSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var change = changeList.SelectedItem as Models.Change;
            if (change == null) return;

            diffViewer.Diff(repo, new DiffViewer.Option() {
                RevisionRange = new string[] { selected + "^", selected },
                Path = change.Path,
                OrgPath = change.OriginalPath,
                UseLFS = isLFSEnabled,
            });
        }

        private void OnStashContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var stash = (sender as Border).DataContext as Models.Stash;
            if (stash == null) return;

            var apply = new MenuItem();
            apply.Header = App.Text("StashCM.Apply");
            apply.Click += (o, e) => Start(() => new Commands.Stash(repo).Apply(stash.Name));

            var pop = new MenuItem();
            pop.Header = App.Text("StashCM.Pop");
            pop.Click += (o, e) => Start(() => new Commands.Stash(repo).Pop(stash.Name));

            var delete = new MenuItem();
            delete.Header = App.Text("StashCM.Drop");
            delete.Click += (o, e) => new Popups.StashDropConfirm(repo, stash.Name, stash.Message).Show();

            var menu = new ContextMenu() { PlacementTarget = sender as UIElement };
            menu.Items.Add(apply);
            menu.Items.Add(pop);
            menu.Items.Add(delete);
            menu.IsOpen = true;
            ev.Handled = true;
        }

        private async void Start(Func<bool> job) {
            waiting.Visibility = Visibility.Visible;
            waiting.IsAnimating = true;
            Models.Watcher.SetEnabled(repo, false);
            await Task.Run(job);
            Models.Watcher.SetEnabled(repo, true);
            waiting.Visibility = Visibility.Collapsed;
            waiting.IsAnimating = false;
        }
    }
}
