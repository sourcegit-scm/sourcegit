using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SourceGit.Views.Widgets {
    /// <summary>
    ///     历史记录
    /// </summary>
    public partial class Histories : UserControl {
        private Models.Repository repo = null;
        private List<Models.Commit> cachedCommits = new List<Models.Commit>();
        private bool searching = false;

        public Histories(Models.Repository repo) {
            this.repo = repo;

            InitializeComponent();
            ChangeOrientation(null, null);
            UpdateCommits();

            var watcher = Models.Watcher.Get(repo.Path);
            watcher.BranchChanged += UpdateCommits;
            watcher.TagChanged += UpdateCommits;
        }

        #region DATA
        public void NavigateTo(string commit) {
            if (string.IsNullOrEmpty(commit)) return;

            foreach (var item in commitList.ItemsSource) {
                var c = item as Models.Commit;
                if (c.SHA.StartsWith(commit, StringComparison.Ordinal)) {
                    commitList.SelectedItem = c;
                    commitList.ScrollIntoView(c);
                    break;
                }
            }
        }

        public void UpdateCommits() {
            Dispatcher.Invoke(() => {
                loading.Visibility = Visibility.Visible;
                loading.IsAnimating = true;
            });

            Task.Run(() => {
                var limits = "-20000 ";
                if (repo.Filters.Count > 0) {
                    limits += string.Join(" ", repo.Filters);
                } else {
                    limits += "--branches --remotes --tags";
                }

                cachedCommits = new Commands.Commits(repo.Path, limits).Result();
                UpdateVisibleCommits();
            });
        }

        private void UpdateVisibleCommits(string filter = null) {
            var visible = new List<Models.Commit>();
            searching = false;

            if (string.IsNullOrEmpty(filter)) {
                visible = cachedCommits;
            } else {
                searching = true;
                foreach (var c in cachedCommits) {
#if NET48
                    if (c.SHA.Contains(filter)
                        || c.Subject.Contains(filter)
                        || c.Message.Contains(filter)
                        || c.Author.Name.Contains(filter)
                        || c.Committer.Name.Contains(filter)) {
                        visible.Add(c);
                    }
#else
                    if (c.SHA.Contains(filter, StringComparison.Ordinal)
                        || c.Subject.Contains(filter, StringComparison.Ordinal)
                        || c.Message.Contains(filter, StringComparison.Ordinal)
                        || c.Author.Name.Contains(filter, StringComparison.Ordinal)
                        || c.Committer.Name.Contains(filter, StringComparison.Ordinal)) {
                        visible.Add(c);
                    }
#endif
                }
            }

            Dispatcher.Invoke(() => {
                loading.IsAnimating = false;
                loading.Visibility = Visibility.Collapsed;
                graph.SetData(visible, searching);
                commitList.ItemsSource = visible;
            });
        }
        #endregion

        #region LAYOUT
        private void ChangeOrientation(object sender, RoutedEventArgs e) {
            if (layout == null || commitListPanel == null || inspector == null || splitter == null) return;

            layout.RowDefinitions.Clear();
            layout.ColumnDefinitions.Clear();

            if (Models.Preference.Instance.Window.MoveCommitInfoRight) {
                layout.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
                layout.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1) });
                layout.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });

                splitter.HorizontalAlignment = HorizontalAlignment.Center;
                splitter.VerticalAlignment = VerticalAlignment.Stretch;
                splitter.Width = 1;
                splitter.Height = double.NaN;

                Grid.SetRow(commitListPanel, 0);
                Grid.SetRow(splitter, 0);
                Grid.SetRow(inspector, 0);
                Grid.SetColumn(commitListPanel, 0);
                Grid.SetColumn(splitter, 1);
                Grid.SetColumn(inspector, 2);
            } else {
                layout.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star), MinHeight = 100 });
                layout.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1) });
                layout.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star), MinHeight = 100 });

                splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                splitter.VerticalAlignment = VerticalAlignment.Center;
                splitter.Width = double.NaN;
                splitter.Height = 1;

                Grid.SetRow(commitListPanel, 0);
                Grid.SetRow(splitter, 1);
                Grid.SetRow(inspector, 2);
                Grid.SetColumn(commitListPanel, 0);
                Grid.SetColumn(splitter, 0);
                Grid.SetColumn(inspector, 0);
            }

            layout.InvalidateArrange();
        }

        public void ChangeGraphMode(object sender, RoutedEventArgs e) {
            graph.InvalidateVisual();
            e.Handled = true;
        }
        #endregion

        #region SEARCH_BAR
        public void ToggleSearch() {
            if (searchBar.Margin.Top == 0) {
                if (searchBar.Margin.Top != 0) return;

                if (searching) {
                    loading.Visibility = Visibility.Visible;
                    loading.IsAnimating = true;
                    txtSearch.Text = "";
                    Task.Run(() => UpdateVisibleCommits());
                }

                ThicknessAnimation anim = new ThicknessAnimation();
                anim.From = new Thickness(0);
                anim.To = new Thickness(0, -32, 0, 0);
                anim.Duration = TimeSpan.FromSeconds(.3);
                searchBar.BeginAnimation(MarginProperty, anim);
            } else {
                ThicknessAnimation anim = new ThicknessAnimation();
                anim.From = new Thickness(0, -32, 0, 0);
                anim.To = new Thickness(0);
                anim.Duration = TimeSpan.FromSeconds(.3);
                searchBar.BeginAnimation(MarginProperty, anim);

                txtSearch.Focus();
            }
        }

        private void ClearSearch(object sender, RoutedEventArgs e) {
            txtSearch.Text = "";
        }

        private void HideSearch(object sender, RoutedEventArgs e) {
            if (searching) {
                loading.Visibility = Visibility.Visible;
                loading.IsAnimating = true;
                txtSearch.Text = "";
                Task.Run(() => UpdateVisibleCommits());
            }

            ThicknessAnimation anim = new ThicknessAnimation();
            anim.From = new Thickness(0);
            anim.To = new Thickness(0, -32, 0, 0);
            anim.Duration = TimeSpan.FromSeconds(.3);
            searchBar.BeginAnimation(MarginProperty, anim);
        }

        private void OnSearchPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                loading.Visibility = Visibility.Visible;
                loading.IsAnimating = true;

                var filter = txtSearch.Text;
                Task.Run(() => UpdateVisibleCommits(filter));
            } else if (e.Key == Key.Escape) {
                ToggleSearch();
            }
        }
        #endregion

        #region COMMIT_LIST
        private void OnCommitListScrolled(object sender, ScrollChangedEventArgs e) {
            graph.SetOffset(e.VerticalOffset * commitList.RowHeight);
        }

        private void OnCommitListKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Up || e.Key == Key.Down) {
                OnCommitSelectionChanged(sender, null);
                e.Handled = true;
            }
        }

        private void OnCommitSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down)) return;

            mask.Visibility = Visibility.Collapsed;
            commitDetail.Visibility = Visibility.Collapsed;
            revisionCompare.Visibility = Visibility.Collapsed;

            var selected = commitList.SelectedItems;
            if (selected.Count == 1) {
                commitDetail.SetData(repo.Path, selected[0] as Models.Commit);
                commitDetail.Visibility = Visibility.Visible;
            } else if (selected.Count == 2) {
                revisionCompare.SetData(repo.Path, selected[0] as Models.Commit, selected[1] as Models.Commit);
                revisionCompare.Visibility = Visibility.Visible;
            } else if (selected.Count > 2) {
                mask.Visibility = Visibility.Visible;
                txtCounter.Text = App.Text("Histories.Selected", selected.Count);
            } else {
                mask.Visibility = Visibility.Visible; 
                txtCounter.Text = App.Text("Histories.Guide");
            }
        }

        private void OnCommitContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var row = sender as DataGridRow;
            if (row == null) return;

            var commit = row.DataContext as Models.Commit;
            if (commit == null) return;

            commitList.SelectedItem = commit;

            var current = repo.Branches.Find(x => x.IsCurrent);
            var merged = commit.IsMerged;
            var menu = new ContextMenu();
            var tags = new List<string>();

            // Decorators
            if (commit.HasDecorators) {
                foreach (var d in commit.Decorators) {
                    if (d.Type == Models.DecoratorType.CurrentBranchHead) {
                        FillCurrentBranchMenu(menu, current);
                    } else if (d.Type == Models.DecoratorType.LocalBranchHead) {
                        FillOtherLocalBranchMenu(menu, repo.Branches.Find(x => x.IsLocal && x.Name == d.Name), current, merged);
                    } else if (d.Type == Models.DecoratorType.RemoteBranchHead) {
                        FillRemoteBranchMenu(menu, repo.Branches.Find(x => !x.IsLocal && d.Name == $"{x.Remote}/{x.Name}"), current, merged);
                    } else if (d.Type == Models.DecoratorType.Tag) {
                        tags.Add(d.Name);
                    }
                }

                if (menu.Items.Count > 0) menu.Items.Add(new Separator());
            }

            // Tags
            if (tags.Count > 0) {
                foreach (var tag in tags) FillTagMenu(menu, tag);
                menu.Items.Add(new Separator());
            }

            if (current.Head != commit.SHA) {
                var reset = new MenuItem();
                reset.Header = App.Text("CommitCM.Reset", current.Name);
                reset.Click += (o, e) => {
                    new Popups.Reset(repo.Path, current.Name, commit).Show();
                    e.Handled = true;
                };
                menu.Items.Add(reset);
            }

            if (!merged) {
                var rebase = new MenuItem();
                rebase.Header = App.Text("CommitCM.Rebase", current.Name);
                rebase.Click += (o, e) => {
                    new Popups.Rebase(repo.Path, current.Name, commit).Show();
                    e.Handled = true;
                };
                menu.Items.Add(rebase);

                var cherryPick = new MenuItem();
                cherryPick.Header = App.Text("CommitCM.CherryPick");
                cherryPick.Click += (o, e) => {
                    new Popups.CherryPick(repo.Path, commit).Show();
                    e.Handled = true;
                };
                menu.Items.Add(cherryPick);
            } else {
                var revert = new MenuItem();
                revert.Header = App.Text("CommitCM.Revert");
                revert.Click += (o, e) => {
                    new Popups.Revert(repo.Path, commit).Show();
                    e.Handled = true;
                };
                menu.Items.Add(revert);
            }

            menu.Items.Add(new Separator());

            var createBranch = new MenuItem();
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (o, e) => {
                new Popups.CreateBranch(repo, commit).Show();
                e.Handled = true;
            };
            menu.Items.Add(createBranch);

            var createTag = new MenuItem();
            createTag.Header = App.Text("CreateTag");
            createTag.Click += (o, e) => {
                new Popups.CreateTag(repo, commit).Show();
                e.Handled = true;
            };
            menu.Items.Add(createTag);
            menu.Items.Add(new Separator());

            var saveToPatch = new MenuItem();
            saveToPatch.Header = App.Text("CommitCM.SaveAsPatch");
            saveToPatch.Click += (o, e) => {
                FolderBrowser.Open(null, "Save patch to ...", saveTo => {
                    new Commands.FormatPatch(repo.Path, commit.SHA, saveTo).Exec();
                });
            };
            menu.Items.Add(saveToPatch);
            menu.Items.Add(new Separator());

            var copySHA = new MenuItem();
            copySHA.Header = App.Text("CommitCM.CopySHA");
            copySHA.Click += (o, e) => {
                Clipboard.SetText(commit.SHA);
                e.Handled = true;
            };
            menu.Items.Add(copySHA);

            var copyInfo = new MenuItem();
            copyInfo.Header = App.Text("CommitCM.CopyInfo");
            copyInfo.Click += (o, e) => {
                Clipboard.SetText(string.Format(
                    "SHA: {0}\nTITLE: {1}\nAUTHOR: {2} <{3}>\nTIME: {4}",
                    commit.SHA, commit.Subject, commit.Committer.Name, commit.Committer.Email, commit.Committer.Time));
            };
            menu.Items.Add(copyInfo);

            menu.IsOpen = true;
            ev.Handled = true;
        }

        private void FillCurrentBranchMenu(ContextMenu menu, Models.Branch current) {
            var icon = new Path();
            icon.Data = FindResource("Icon.Branch") as Geometry;
            icon.VerticalAlignment = VerticalAlignment.Bottom;
            icon.Width = 10;
            icon.Height = 10;

            var dirty = !string.IsNullOrEmpty(current.UpstreamTrackStatus);
            var submenu = new MenuItem();
            submenu.Header = current.Name;
            submenu.Icon = icon;

            if (!string.IsNullOrEmpty(current.Upstream)) {
                var upstream = current.Upstream.Substring(13);

                var fastForward = new MenuItem();
                fastForward.Header = App.Text("BranchCM.FastForward", upstream);
                fastForward.IsEnabled = dirty;
                fastForward.Click += (o, e) => {
                    new Popups.Merge(repo.Path, upstream, current.Name).ShowAndStart();
                    e.Handled = true;
                };
                submenu.Items.Add(fastForward);

                var pull = new MenuItem();
                pull.Header = App.Text("BranchCM.Pull", upstream);
                pull.IsEnabled = dirty;
                pull.Click += (o, e) => {
                    new Popups.Pull(repo, null).Show();
                    e.Handled = true;
                };
                submenu.Items.Add(pull);
            }

            var push = new MenuItem();
            push.Header = App.Text("BranchCM.Push", current.Name);
            push.IsEnabled = dirty;
            push.Click += (o, e) => {
                new Popups.Push(repo, current).Show();
                e.Handled = true;
            };
            submenu.Items.Add(push);
            submenu.Items.Add(new Separator());

            var type = repo.GitFlow.GetBranchType(current.Name);
            if (type != Models.GitFlowBranchType.None) {
                var flowIcon = new Path();
                flowIcon.Data = FindResource("Icon.Flow") as Geometry;
                flowIcon.Width = 10;
                flowIcon.Height = 10;

                var finish = new MenuItem();
                finish.Header = App.Text("BranchCM.Finish", current.Name);
                finish.Icon = flowIcon;
                finish.Click += (o, e) => {
                    new Popups.GitFlowFinish(repo, current.Name, type).Show();
                    e.Handled = true;
                };
                submenu.Items.Add(finish);
                submenu.Items.Add(new Separator());
            }

            var rename = new MenuItem();
            rename.Header = App.Text("BranchCM.Rename", current.Name);
            rename.Click += (o, e) => {
                new Popups.RenameBranch(repo, current.Name).Show();
                e.Handled = true;
            };
            submenu.Items.Add(rename);

            menu.Items.Add(submenu);
        }

        private void FillOtherLocalBranchMenu(ContextMenu menu, Models.Branch branch, Models.Branch current, bool merged) {
            var icon = new Path();
            icon.Data = FindResource("Icon.Branch") as Geometry;
            icon.VerticalAlignment = VerticalAlignment.Bottom;
            icon.Width = 10;
            icon.Height = 10;

            var submenu = new MenuItem();
            submenu.Header = branch.Name;
            submenu.Icon = icon;

            var checkout = new MenuItem();
            checkout.Header = App.Text("BranchCM.Checkout", branch.Name);
            checkout.Click += async (o, e) => {
                Models.Watcher.SetEnabled(repo.Path, false);
                await Task.Run(() => new Commands.Checkout(repo.Path).Branch(branch.Name));
                Models.Watcher.SetEnabled(repo.Path, true);
                e.Handled = true;
            };
            submenu.Items.Add(checkout);

            var merge = new MenuItem();
            merge.Header = App.Text("BranchCM.Merge", branch.Name, current.Name);
            merge.IsEnabled = !merged;
            merge.Click += (o, e) => {
                new Popups.Merge(repo.Path, branch.Name, current.Name).Show();
                e.Handled = true;
            };

            submenu.Items.Add(merge);
            submenu.Items.Add(new Separator());

            var type = repo.GitFlow.GetBranchType(branch.Name);
            if (type != Models.GitFlowBranchType.None) {
                var flowIcon = new Path();
                flowIcon.Data = FindResource("Icon.Flow") as Geometry;
                flowIcon.Width = 10;
                flowIcon.Height = 10;

                var finish = new MenuItem();
                finish.Header = App.Text("BranchCM.Finish", branch.Name);
                finish.Icon = flowIcon;
                finish.Click += (o, e) => {
                    new Popups.GitFlowFinish(repo, branch.Name, type).Show();
                    e.Handled = true;
                };
                submenu.Items.Add(finish);
                submenu.Items.Add(new Separator());
            }

            var rename = new MenuItem();
            rename.Header = App.Text("BranchCM.Rename", branch.Name);
            rename.Click += (o, e) => {
                new Popups.RenameBranch(repo, branch.Name).Show();
                e.Handled = true;
            };
            submenu.Items.Add(rename);

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", branch.Name);
            delete.Click += (o, e) => {
                new Popups.DeleteBranch(repo.Path, branch.Name).Show();
                e.Handled = true;
            };
            submenu.Items.Add(delete);

            menu.Items.Add(submenu);
        }

        private void FillRemoteBranchMenu(ContextMenu menu, Models.Branch branch, Models.Branch current, bool merged) {
            var name = $"{branch.Remote}/{branch.Name}";

            var icon = new Path();
            icon.Data = FindResource("Icon.Branch") as Geometry;
            icon.VerticalAlignment = VerticalAlignment.Bottom;
            icon.Width = 10;
            icon.Height = 10;

            var submenu = new MenuItem();
            submenu.Header = name;
            submenu.Icon = icon;

            var checkout = new MenuItem();
            checkout.Header = App.Text("BranchCM.Checkout", name);
            checkout.Click += async (o, e) => {
                foreach (var b in repo.Branches) {
                    if (b.IsLocal && b.Upstream == branch.FullName) {
                        if (b.IsCurrent) return;

                        Models.Watcher.SetEnabled(repo.Path, false);
                        await Task.Run(() => new Commands.Checkout(repo.Path).Branch(b.Name));
                        Models.Watcher.SetEnabled(repo.Path, true);
                        return;
                    }
                }

                new Popups.CreateBranch(repo, branch).Show();
                e.Handled = true;
            };
            submenu.Items.Add(checkout);

            var merge = new MenuItem();
            merge.Header = App.Text("BranchCM.Merge", name, current.Name);
            merge.IsEnabled = !merged;
            merge.Click += (o, e) => {
                new Popups.Merge(repo.Path, name, current.Name).Show();
                e.Handled = true;
            };

            submenu.Items.Add(merge);
            submenu.Items.Add(new Separator());

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", name);
            delete.Click += (o, e) => {
                new Popups.DeleteBranch(repo.Path, branch.Name, branch.Remote).Show();
                e.Handled = true;
            };
            submenu.Items.Add(delete);

            menu.Items.Add(submenu);
        }

        private void FillTagMenu(ContextMenu menu, string tag) {
            var icon = new Path();
            icon.Data = FindResource("Icon.Tag") as Geometry;
            icon.Width = 10;
            icon.Height = 10;

            var submenu = new MenuItem();
            submenu.Header = tag;
            submenu.Icon = icon;
            submenu.MinWidth = 200;

            var push = new MenuItem();
            push.Header = App.Text("TagCM.Push", tag);
            push.Click += (o, e) => {
                new Popups.PushTag(repo, tag).Show();
                e.Handled = true;
            };
            submenu.Items.Add(push);

            var delete = new MenuItem();
            delete.Header = App.Text("TagCM.Delete", tag);
            delete.Click += (o, e) => {
                new Popups.DeleteTag(repo.Path, tag).Show();
                e.Handled = true;
            };
            submenu.Items.Add(delete);
            menu.Items.Add(submenu);
        }
        #endregion
    }
}
