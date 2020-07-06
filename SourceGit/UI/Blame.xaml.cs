using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SourceGit.UI {

    /// <summary>
    ///     Viewer to show git-blame
    /// </summary>
    public partial class Blame : Window {

        /// <summary>
        ///     Background color for blocks.
        /// </summary>
        public static Brush[] BG = new Brush[] {
            Brushes.Transparent,
            new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
        };

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="file"></param>
        /// <param name="revision"></param>
        public Blame(Git.Repository repo, string file, string revision) {
            InitializeComponent();

            double minWidth = content.ActualWidth;

            // Move to center.
            var parent = App.Current.MainWindow;
            Left = parent.Left + (parent.Width - Width) * 0.5;
            Top = parent.Top + (parent.Height - Height) * 0.5;

            // Show loading.
            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            loading.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            loading.Visibility = Visibility.Visible;

            // Layout content
            blameFile.Content = $"{file}@{revision.Substring(0, 8)}";
            Task.Run(() => {
                var blame = repo.BlameFile(file, revision);

                Dispatcher.Invoke(() => {
                    content.Document.Blocks.Clear();

                    if (blame.IsBinary) {
                        lineNumber.Text = "0";

                        Paragraph p = new Paragraph(new Run("BINARY FILE BLAME NOT SUPPORTED!!!"));
                        p.Margin = new Thickness(0);
                        p.Padding = new Thickness(0);
                        p.LineHeight = 1;
                        p.Background = Brushes.Transparent;
                        p.Foreground = FindResource("Brush.FG") as SolidColorBrush;
                        p.FontStyle = FontStyles.Normal;

                        content.Document.Blocks.Add(p);
                    } else {
                        List<string> numbers = new List<string>();
                        for (int i = 0; i < blame.LineCount; i++) numbers.Add(i.ToString());
                        lineNumber.Text = string.Join("\n", numbers);
                        numbers.Clear();

                        for (int i = 0; i < blame.Blocks.Count; i++) {
                            var frag = blame.Blocks[i];
                            var idx = i;

                            Paragraph p = new Paragraph(new Run(frag.Content));
                            p.DataContext = frag;
                            p.Margin = new Thickness(0);
                            p.Padding = new Thickness(0);
                            p.LineHeight = 1;
                            p.Background = BG[i % 2];
                            p.Foreground = FindResource("Brush.FG") as SolidColorBrush;
                            p.FontStyle = FontStyles.Normal;
                            p.MouseRightButtonDown += (sender, ev) => {
                                Hyperlink link = new Hyperlink(new Run(frag.CommitSHA));
                                link.ToolTip = "CLICK TO GO";
                                link.Click += (o, e) => {
                                    repo.OnNavigateCommit?.Invoke(frag.CommitSHA);
                                    e.Handled = true;
                                };

                                foreach (var block in content.Document.Blocks) {
                                    var paragraph = block as Paragraph;
                                    if ((paragraph.DataContext as Git.Blame.Block).CommitSHA == frag.CommitSHA) {
                                        paragraph.Background = Brushes.Green;
                                    } else {
                                        paragraph.Background = BG[i % 2];
                                    }
                                }

                                commitID.Content = link;
                                authorName.Content = frag.Author;
                                authorTime.Content = frag.Time;
                                popup.IsOpen = true;
                            };

                            var formatter = new FormattedText(
                                frag.Content,
                                CultureInfo.CurrentUICulture,
                                FlowDirection.LeftToRight,
                                new Typeface(content.FontFamily, p.FontStyle, p.FontWeight, p.FontStretch),
                                content.FontSize,
                                Brushes.Black,
                                new NumberSubstitution(),
                                TextFormattingMode.Ideal);
                            if (minWidth < formatter.Width) {
                                content.Document.PageWidth = formatter.Width + 16;
                                minWidth = formatter.Width;
                            }

                            content.Document.Blocks.Add(p);
                        }
                    }

                    // Hide loading.
                    loading.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
                    loading.Visibility = Visibility.Collapsed;
                });
            });
        }

        /// <summary>
        ///     Click logo
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogoMouseButtonDown(object sender, MouseButtonEventArgs e) {
            var element = e.OriginalSource as FrameworkElement;
            if (element == null) return;

            var pos = PointToScreen(new Point(0, 33));
            SystemCommands.ShowSystemMenu(this, pos);
        }

        /// <summary>
        ///     Minimize
        /// </summary>
        private void Minimize(object sender, RoutedEventArgs e) {
            SystemCommands.MinimizeWindow(this);
        }

        /// <summary>
        ///     Maximize/Restore
        /// </summary>
        private void MaximizeOrRestore(object sender, RoutedEventArgs e) {
            if (WindowState == WindowState.Normal) {
                SystemCommands.MaximizeWindow(this);
            } else {
                SystemCommands.RestoreWindow(this);
            }
        }

        /// <summary>
        ///     Quit
        /// </summary>
        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }

        /// <summary>
        ///     Sync scroll
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SyncScrollChanged(object sender, ScrollChangedEventArgs e) {
            if (e.VerticalChange != 0) {
                var margin = new Thickness(4, -e.VerticalOffset, 4, 0);
                lineNumber.Margin = margin;
            }
        }

        /// <summary>
        ///     Mouse wheel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MouseWheelOnContent(object sender, MouseWheelEventArgs e) {
            if (e.Delta > 0) {
                content.LineUp();
            } else {
                content.LineDown();
            }

            e.Handled = true;
        }

        /// <summary>
        ///     Content size changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentSizeChanged(object sender, SizeChangedEventArgs e) {
            if (content.Document.PageWidth < content.ActualWidth) {
                content.Document.PageWidth = content.ActualWidth;
            }
        }
    }
}
