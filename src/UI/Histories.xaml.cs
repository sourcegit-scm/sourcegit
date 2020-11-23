using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SourceGit.UI {

    /// <summary>
    ///     Commit histories viewer
    /// </summary>
    public partial class Histories : UserControl {

        /// <summary>
        ///     Current opened repository.
        /// </summary>
        public Git.Repository Repo { get; set; }

        /// <summary>
        ///     Cached commits.
        /// </summary>
        private List<Git.Commit> cachedCommits = new List<Git.Commit>();

        /// <summary>
        ///     Is in search mode?
        /// </summary>
        private bool isSearchMode = false;

        /// <summary>
        ///     Regex to test search input.
        /// </summary>
        private Regex commitRegex = new Regex(@"^[0-9a-f]{6,40}$", RegexOptions.None);

        /// <summary>
        ///     Constructor
        /// </summary>
        public Histories() {
            InitializeComponent();
            ChangeOrientation(null, null);
        }

        /// <summary>
        ///     Navigate to given commit.
        /// </summary>
        /// <param name="commit"></param>
        public void Navigate(string commit) {
            if (string.IsNullOrEmpty(commit)) return;

            foreach (var item in commitList.ItemsSource) {
                var c = item as Git.Commit;
                if (c.SHA.StartsWith(commit)) {
                    commitList.SelectedItem = c;
                    commitList.ScrollIntoView(c);
                    return;
                }
            }
        }

        /// <summary>
        ///     Loading tips.
        /// </summary>
        /// <param name="enabled"></param>
        public void SetLoadingEnabled(bool enabled) {
            if (enabled) {
                loading.Visibility = Visibility.Visible;

                DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
                anim.RepeatBehavior = RepeatBehavior.Forever;
                loading.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            } else {
                loading.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
                loading.Visibility = Visibility.Collapsed;
            }
        }

        #region DATA
        public void SetCommits(List<Git.Commit> commits) {
            cachedCommits = commits;
            if (isSearchMode) return;

            var maker = Helpers.CommitGraphMaker.Parse(commits);

            Dispatcher.Invoke(() => {
                commitGraph.Children.Clear();
                isSearchMode = false;
                txtSearch.Text = "";

                // Draw all lines.
                foreach (var path in maker.Lines) {
                    var size = path.Points.Count;
                    var geo = new StreamGeometry();
                    var last = path.Points[0];

                    using (var ctx = geo.Open()) {
                        ctx.BeginFigure(last, false, false);

                        for (int i = 1; i < size; i++) {
                            var cur = path.Points[i];

                            if (cur.X > last.X) {
                                ctx.QuadraticBezierTo(new Point(cur.X, last.Y), cur, true, false);
                            } else if (cur.X < last.X) {
                                if (i < size - 1) {
                                    cur.Y += Helpers.CommitGraphMaker.HALF_HEIGHT;

                                    var midY = (last.Y + cur.Y) / 2;
                                    var midX = (last.X + cur.X) / 2;
                                    ctx.PolyQuadraticBezierTo(new Point[] {
                                        new Point(last.X, midY),
                                        new Point(midX, midY),
                                        new Point(cur.X, midY),
                                        cur
                                    }, true, false);
                                } else {
                                    ctx.QuadraticBezierTo(new Point(last.X, cur.Y), cur, true, false);
                                }                            
                            } else {
                                ctx.LineTo(cur, true, false);
                            }

                            last = cur;
                        }
                    }

                    geo.Freeze();

                    var p = new Path();
                    p.Data = geo;
                    p.Stroke = path.Brush;
                    p.StrokeThickness = 2;
                    commitGraph.Children.Add(p);
                }
                maker.Lines.Clear();

                // Draw short links
                foreach (var link in maker.Links) {
                    var geo = new StreamGeometry();
                    
                    using (var ctx = geo.Open()) {
                        ctx.BeginFigure(link.Start, false, false);
                        ctx.QuadraticBezierTo(link.Control, link.End, true, false);
                    }

                    geo.Freeze();

                    var p = new Path();
                    p.Data = geo;
                    p.Stroke = link.Brush;
                    p.StrokeThickness = 2;
                    commitGraph.Children.Add(p);
                }
                maker.Links.Clear();

                // Draw points.
                foreach (var dot in maker.Dots) {
                    var ellipse = new Ellipse();
                    ellipse.Height = 6;
                    ellipse.Width = 6;
                    ellipse.Fill = dot.Color;
                    ellipse.SetValue(Canvas.LeftProperty, dot.X);
                    ellipse.SetValue(Canvas.TopProperty, dot.Y);
                    commitGraph.Children.Add(ellipse);
                }
                maker.Dots.Clear();

                commitList.ItemsSource = new List<Git.Commit>(cachedCommits);
                SetLoadingEnabled(false);
            });
        }

        public void SetSearchResult(List<Git.Commit> commits) {
            isSearchMode = true;

            foreach (var c in commits) c.GraphOffset = 0;

            Dispatcher.Invoke(() => {
                commitGraph.Children.Clear();
                commitList.ItemsSource = new List<Git.Commit>(commits);
                SetLoadingEnabled(false);
            });
        }

        private void Cleanup(object sender, RoutedEventArgs e) {
            commitGraph.Children.Clear();
            commitList.ItemsSource = null;
            cachedCommits.Clear();
        }
        #endregion

        #region SEARCH_BAR
        public void OpenSearchBar() {
            if (searchBar.Margin.Top == 0) return;

            ThicknessAnimation anim = new ThicknessAnimation();
            anim.From = new Thickness(0, -32, 0, 0);
            anim.To = new Thickness(0);
            anim.Duration = TimeSpan.FromSeconds(.3);
            searchBar.BeginAnimation(Grid.MarginProperty, anim);

            txtSearch.Focus();
        }

        public void HideSearchBar() {
            if (searchBar.Margin.Top != 0) return;

            ClearSearch(null, null);

            ThicknessAnimation anim = new ThicknessAnimation();
            anim.From = new Thickness(0);
            anim.To = new Thickness(0, -32, 0, 0);
            anim.Duration = TimeSpan.FromSeconds(.3);
            searchBar.BeginAnimation(Grid.MarginProperty, anim);
        }

        private void HideSearchBarByButton(object sender, RoutedEventArgs e) {
            HideSearchBar();
        }

        private void ClearSearch(object sender, RoutedEventArgs e) {
            txtSearch.Text = "";
            if (isSearchMode) {
                isSearchMode = false;
                SetLoadingEnabled(true);
                Task.Run(() => SetCommits(cachedCommits));
            }
        }

        private void PreviewSearchKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                string search = txtSearch.Text;
                if (string.IsNullOrEmpty(search)) {
                    ClearSearch(sender, e);
                } else if (commitRegex.IsMatch(search)) {
                    SetLoadingEnabled(true);
                    Task.Run(() => {
                        var commits = Repo.Commits($"search -n 1");
                        SetSearchResult(commits);
                    });
                } else {
                    SetLoadingEnabled(true);

                    Task.Run(() => {
                        List<Git.Commit> found = new List<Git.Commit>();

                        foreach (var commit in cachedCommits) {
                            if (commit.Subject.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                (commit.Author != null && commit.Author.Name.Equals(search, StringComparison.OrdinalIgnoreCase)) ||
                                (commit.Committer != null && commit.Committer.Name.Equals(search, StringComparison.OrdinalIgnoreCase)) ||
                                commit.Message.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) {
                                found.Add(commit);
                            }
                        }

                        SetSearchResult(found);
                    });
                }
            }
        }
        #endregion

        #region COMMIT_DATAGRID_AND_GRAPH
        private void CommitListScrolled(object sender, ScrollChangedEventArgs e) {
            commitGraph.Margin = new Thickness(0, -e.VerticalOffset * Helpers.CommitGraphMaker.UNIT_HEIGHT, 0, 0);
        }

        private void CommitSelectChanged(object sender, SelectionChangedEventArgs e) {
            mask4MultiSelection.Visibility = Visibility.Collapsed;
            commitViewer.Visibility = Visibility.Collapsed;
            twoCommitDiff.Visibility = Visibility.Collapsed;

            var selected = commitList.SelectedItems;
            if (selected.Count == 1) {
                commitViewer.Visibility = Visibility.Visible;
                commitViewer.SetData(Repo, selected[0] as Git.Commit);
            } else if (selected.Count == 2) {
                twoCommitDiff.Visibility = Visibility.Visible;
                twoCommitDiff.SetData(Repo, (selected[0] as Git.Commit).ShortSHA, (selected[1] as Git.Commit).ShortSHA);
            } else {
                mask4MultiSelection.Visibility = Visibility.Visible;
                txtTotalSelected.Content = $"SELECTED {selected.Count} COMMITS";
            }
        }

        private MenuItem GetCurrentBranchContextMenu(Git.Branch branch) {
            var icon = new Path();
            icon.Style = FindResource("Style.Icon") as Style;
            icon.Data = FindResource("Icon.Branch") as Geometry;
            icon.VerticalAlignment = VerticalAlignment.Bottom;
            icon.Width = 10;

            var submenu = new MenuItem();
            submenu.Header = branch.Name;
            submenu.Icon = icon;

            if (!string.IsNullOrEmpty(branch.Upstream)) {
                var upstream = branch.Upstream.Substring(13);
                var fastForward = new MenuItem();
                fastForward.Header = $"Fast-Forward to '{upstream}'";
                fastForward.Click += (o, e) => {
                    Merge.StartDirectly(Repo, upstream, branch.Name);
                    e.Handled = true;
                };
                submenu.Items.Add(fastForward);

                var pull = new MenuItem();
                pull.Header = $"Pull '{upstream}' ...";
                pull.Click += (o, e) => {
                    Pull.Show(Repo);
                    e.Handled = true;
                };
                submenu.Items.Add(pull);
            }

            var push = new MenuItem();
            push.Header = $"Push '{branch.Name}' ...";
            push.Click += (o, e) => {
                Push.Show(Repo, branch);
                e.Handled = true;
            };
            submenu.Items.Add(push);
            submenu.Items.Add(new Separator());

            if (branch.Kind != Git.Branch.Type.Normal) {
                var flowIcon = new Path();
                flowIcon.Style = FindResource("Style.Icon") as Style;
                flowIcon.Data = FindResource("Icon.Flow") as Geometry;
                flowIcon.Width = 10;

                var finish = new MenuItem();
                finish.Header = $"Git Flow - Finish '{branch.Name}'";
                finish.Icon = flowIcon;
                finish.Click += (o, e) => {
                    GitFlowFinishBranch.Show(Repo, branch);
                    e.Handled = true;
                };

                submenu.Items.Add(finish); 
                submenu.Items.Add(new Separator());
            }

            var rename = new MenuItem();
            rename.Header = "Rename ...";
            rename.Click += (o, e) => {
                RenameBranch.Show(Repo, branch);
                e.Handled = true;
            };
            submenu.Items.Add(rename);

            return submenu;
        }

        private MenuItem GetOtherBranchContextMenu(Git.Branch current, Git.Branch branch, bool merged) {
            var icon = new Path();
            icon.Style = FindResource("Style.Icon") as Style;
            icon.Data = FindResource("Icon.Branch") as Geometry;
            icon.VerticalAlignment = VerticalAlignment.Bottom;
            icon.Width = 10;

            var submenu = new MenuItem();
            submenu.Header = branch.Name;
            submenu.Icon = icon;

            var checkout = new MenuItem();
            checkout.Header = $"Checkout '{branch.Name}'";
            checkout.Click += (o, e) => {
                if (branch.IsLocal) {
                    Task.Run(() => Repo.Checkout(branch.Name));
                } else {
                    var upstream = $"refs/remotes/{branch.Name}";
                    var tracked = Repo.Branches().Find(b => b.IsLocal && b.Upstream == upstream);

                    if (tracked == null) {
                        CreateBranch.Show(Repo, branch);
                    } else if (!tracked.IsCurrent) {
                        Task.Run(() => Repo.Checkout(tracked.Name));
                    }
                }

                e.Handled = true;
            };
            submenu.Items.Add(checkout);

            var merge = new MenuItem();
            merge.Header = $"Merge into '{current.Name}' ...";
            merge.IsEnabled = !merged;
            merge.Click += (o, e) => {
                Merge.Show(Repo, branch.Name, current.Name);
                e.Handled = true;
            };
            submenu.Items.Add(merge);
            submenu.Items.Add(new Separator());

            if (branch.Kind != Git.Branch.Type.Normal) {
                var flowIcon = new Path();
                flowIcon.Style = FindResource("Style.Icon") as Style;
                flowIcon.Data = FindResource("Icon.Flow") as Geometry;
                flowIcon.Width = 10;

                var finish = new MenuItem();
                finish.Header = $"Git Flow - Finish '{branch.Name}'";
                finish.Icon = flowIcon;
                finish.Click += (o, e) => {
                    GitFlowFinishBranch.Show(Repo, branch);
                    e.Handled = true;
                };

                submenu.Items.Add(finish);
                submenu.Items.Add(new Separator());
            }

            var rename = new MenuItem();
            rename.Header = "Rename ...";
            rename.Visibility = branch.IsLocal ? Visibility.Visible : Visibility.Collapsed;
            rename.Click += (o, e) => {
                RenameBranch.Show(Repo, current);
                e.Handled = true;
            };
            submenu.Items.Add(rename);

            var delete = new MenuItem();
            delete.Header = "Delete ...";
            delete.Click += (o, e) => {
                DeleteBranch.Show(Repo, branch);
            };
            submenu.Items.Add(delete);

            return submenu;
        }

        private MenuItem GetTagContextMenu(Git.Tag tag) {
            var icon = new Path();
            icon.Style = FindResource("Style.Icon") as Style;
            icon.Data = FindResource("Icon.Tag") as Geometry;
            icon.Width = 10;

            var submenu = new MenuItem();
            submenu.Header = tag.Name;
            submenu.Icon = icon;
            submenu.MinWidth = 200;

            var push = new MenuItem();
            push.Header = "Push ...";
            push.Click += (o, e) => {
                PushTag.Show(Repo, tag);
                e.Handled = true;
            };
            submenu.Items.Add(push);

            var delete = new MenuItem();
            delete.Header = "Delete ...";
            delete.Click += (o, e) => {
                DeleteTag.Show(Repo, tag);
                e.Handled = true;
            };
            submenu.Items.Add(delete);

            return submenu;
        }

        private void CommitContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var row = sender as DataGridRow;
            if (row == null) return;

            var commit = row.DataContext as Git.Commit;
            if (commit == null) return;
            commitList.SelectedItem = commit;

            var current = Repo.CurrentBranch();
            if (current == null) return;

            var menu = new ContextMenu();
            menu.MinWidth = 200;

            // Decorators.
            {
                var localBranchContextMenus = new List<MenuItem>();
                var remoteBranchContextMenus = new List<MenuItem>();
                var tagContextMenus = new List<MenuItem>();

                foreach (var d in commit.Decorators) {
                    if (d.Type == Git.DecoratorType.CurrentBranchHead) {
                        menu.Items.Add(GetCurrentBranchContextMenu(current));
                    } else if (d.Type == Git.DecoratorType.LocalBranchHead) {
                        var branch = Repo.Branches().Find(b => b.Name == d.Name);
                        if (branch != null) {
                            localBranchContextMenus.Add(GetOtherBranchContextMenu(current, branch, commit.IsMerged));
                        }
                    } else if (d.Type == Git.DecoratorType.RemoteBranchHead) {
                        var branch = Repo.Branches().Find(b => b.Name == d.Name);
                        if (branch != null) {
                            remoteBranchContextMenus.Add(GetOtherBranchContextMenu(current, branch, commit.IsMerged));
                        }
                    } else if (d.Type == Git.DecoratorType.Tag) {
                        var tag = Repo.Tags().Find(t => t.Name == d.Name);
                        if (tag != null) tagContextMenus.Add(GetTagContextMenu(tag));
                    }
                }

                foreach (var m in localBranchContextMenus) menu.Items.Add(m);
                foreach (var m in remoteBranchContextMenus) menu.Items.Add(m);
                if (menu.Items.Count > 0) menu.Items.Add(new Separator());

                if (tagContextMenus.Count > 0) {
                    foreach (var m in tagContextMenus) menu.Items.Add(m);
                    menu.Items.Add(new Separator());
                }
            }            

            // Reset
            var reset = new MenuItem();
            reset.Header = $"Reset '{current.Name}' To Here";
            reset.Visibility = commit.IsHEAD ? Visibility.Collapsed : Visibility.Visible;
            reset.Click += (o, e) => {
                Reset.Show(Repo, commit);
                e.Handled = true;
            };
            menu.Items.Add(reset);

            // Rebase or interactive rebase
            var rebase = new MenuItem();
            rebase.Header = commit.IsMerged ? $"Interactive Rebase '{current.Name}' From Here" : $"Rebase '{current.Name}' To Here";
            rebase.Visibility = commit.IsHEAD ? Visibility.Collapsed : Visibility.Visible;
            rebase.Click += (o, e) => {
                if (commit.IsMerged) {
                    if (Repo.LocalChanges().Count > 0) {
                        App.RaiseError("You have local changes!!!");
                        e.Handled = true;
                        return;
                    }

                    var dialog = new InteractiveRebase(Repo, commit);
                    dialog.Owner = App.Current.MainWindow;
                    dialog.ShowDialog();
                } else {
                    Rebase.Show(Repo, commit);
                }

                e.Handled = true;
            };
            menu.Items.Add(rebase);

            // Cherry-Pick
            var cherryPick = new MenuItem();
            cherryPick.Header = "Cherry-Pick This Commit";
            cherryPick.Visibility = commit.IsMerged ? Visibility.Collapsed : Visibility.Visible;
            cherryPick.Click += (o, e) => {
                CherryPick.Show(Repo, commit);
                e.Handled = true;
            };
            menu.Items.Add(cherryPick);

            // Revert commit
            var revert = new MenuItem();
            revert.Header = "Revert Commit";
            revert.Visibility = !commit.IsMerged ? Visibility.Collapsed : Visibility.Visible;
            revert.Click += (o, e) => {
                Revert.Show(Repo, commit);
                e.Handled = true;
            };
            menu.Items.Add(revert);
            menu.Items.Add(new Separator());

            // Common
            var createBranch = new MenuItem();
            createBranch.Header = "Create Branch";
            createBranch.Click += (o, e) => {
                CreateBranch.Show(Repo, commit);
                e.Handled = true;
            };
            menu.Items.Add(createBranch);
            var createTag = new MenuItem();
            createTag.Header = "Create Tag";
            createTag.Click += (o, e) => {
                CreateTag.Show(Repo, commit);
                e.Handled = true;
            };
            menu.Items.Add(createTag);
            menu.Items.Add(new Separator());

            // Save as patch
            var patch = new MenuItem();
            patch.Header = "Save As Patch";
            patch.Click += (o, e) => {
                var dialog = new FolderDailog("Save To ...", null);
                dialog.Open(saveTo => {
                    Repo.RunCommand($"format-patch {commit.SHA} -1 -o \"{saveTo}\"", null);
                });
            };
            menu.Items.Add(patch);
            menu.Items.Add(new Separator());

            // Copy SHA
            var copySHA = new MenuItem();
            copySHA.Header = "Copy Commit SHA";
            copySHA.Click += (o, e) => {
                Clipboard.SetText(commit.SHA);
            };
            menu.Items.Add(copySHA);

            // Copy info
            var copyInfo = new MenuItem();
            copyInfo.Header = "Copy Commit Info";
            copyInfo.Click += (o, e) => {
                Clipboard.SetText(string.Format(
                    "SHA: {0}\nTITLE: {1}\nAUTHOR: {2} <{3}>\nTIME: {4}",
                    commit.SHA, commit.Subject, commit.Committer.Name, commit.Committer.Email, commit.Committer.Time));
            };
            menu.Items.Add(copyInfo);

            menu.IsOpen = true;
            ev.Handled = true;
        }
        #endregion

        #region LAYOUT
        private void ChangeOrientation(object sender, RoutedEventArgs e) {
            if (commitDetailPanel == null || splitter == null || commitListPanel == null) return;

            layout.RowDefinitions.Clear();
            layout.ColumnDefinitions.Clear();

            if (App.Preference.UIUseHorizontalLayout) {
                layout.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
                layout.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(2) });
                layout.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });

                Grid.SetRow(commitListPanel, 0);
                Grid.SetRow(splitter, 0);
                Grid.SetRow(commitDetailPanel, 0);
                Grid.SetColumn(commitListPanel, 0);
                Grid.SetColumn(splitter, 1);
                Grid.SetColumn(commitDetailPanel, 2);
            } else {
                layout.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star), MinHeight = 100 });
                layout.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(2) });
                layout.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star), MinHeight = 100 });

                Grid.SetRow(commitListPanel, 0);
                Grid.SetRow(splitter, 1);
                Grid.SetRow(commitDetailPanel, 2);
                Grid.SetColumn(commitListPanel, 0);
                Grid.SetColumn(splitter, 0);
                Grid.SetColumn(commitDetailPanel, 0);
            }

            layout.InvalidateVisual();
        }
        #endregion
    }
}
