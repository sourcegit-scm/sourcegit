using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.UI {

    /// <summary>
    ///     Viewer for git diff
    /// </summary>
    public partial class DiffViewer : UserControl {
        private double minWidth = 0;

        /// <summary>
        ///     Line mode.
        /// </summary>
        public enum LineMode {
            Normal,
            Indicator,
            Empty,
            Added,
            Deleted,
        }

        /// <summary>
        ///     Constructor
        /// </summary>
        public DiffViewer() {
            InitializeComponent();
            Reset();
        }

        /// <summary>
        ///     
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="file"></param>
        /// <param name="orgFile"></param>
        public void SetData(List<string> lines, string file, string orgFile = null) {
            minWidth = Math.Max(leftText.ActualWidth, rightText.ActualWidth) - 16;

            fileName.Text = file;
            if (!string.IsNullOrEmpty(orgFile)) {
                orgFileNamePanel.Visibility = Visibility.Visible;
                orgFileName.Text = orgFile;
            } else {
                orgFileNamePanel.Visibility = Visibility.Collapsed;
            }

            leftText.Document.Blocks.Clear();
            rightText.Document.Blocks.Clear();

            leftLineNumber.Text = "";
            rightLineNumber.Text = "";

            Regex regex = new Regex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@", RegexOptions.None);
            bool started = false;

            List<Paragraph> leftData = new List<Paragraph>();
            List<Paragraph> rightData = new List<Paragraph>();
            List<string> leftNumbers = new List<string>();
            List<string> rightNumbers = new List<string>();

            int leftLine = 0;
            int rightLine = 0;
            bool bLastLeft = true;

            foreach (var line in lines) {
                if (!started) {
                    var match = regex.Match(line);
                    if (!match.Success) continue;

                    MakeParagraph(leftData, line, LineMode.Indicator);
                    MakeParagraph(rightData, line, LineMode.Indicator);
                    leftNumbers.Add("");
                    rightNumbers.Add("");

                    leftLine = int.Parse(match.Groups[1].Value);
                    rightLine = int.Parse(match.Groups[2].Value);
                    started = true;
                    continue;
                }

                if (line[0] == '-') {
                    MakeParagraph(leftData, line.Substring(1), LineMode.Deleted);
                    leftNumbers.Add(leftLine.ToString());
                    leftLine++;
                    bLastLeft = true;
                } else if (line[0] == '+') {
                    MakeParagraph(rightData, line.Substring(1), LineMode.Added);
                    rightNumbers.Add(rightLine.ToString());
                    rightLine++;
                    bLastLeft = false;
                } else if (line[0] == '\\') {
                    if (bLastLeft) {
                        MakeParagraph(leftData, line.Substring(1), LineMode.Indicator);
                        leftNumbers.Add("");
                    } else {
                        MakeParagraph(rightData, line.Substring(1), LineMode.Indicator);
                        rightNumbers.Add("");
                    }
                } else {
                    FitBothSide(leftData, leftNumbers, rightData, rightNumbers);
                    bLastLeft = true;

                    var match = regex.Match(line);
                    if (match.Success) {
                        MakeParagraph(leftData, line, LineMode.Indicator);
                        MakeParagraph(rightData, line, LineMode.Indicator);
                        leftNumbers.Add("");
                        rightNumbers.Add("");

                        leftLine = int.Parse(match.Groups[1].Value);
                        rightLine = int.Parse(match.Groups[2].Value);
                    } else {
                        var data = line.Substring(1);
                        MakeParagraph(leftData, data, LineMode.Normal);
                        MakeParagraph(rightData, data, LineMode.Normal);
                        leftNumbers.Add(leftLine.ToString());
                        rightNumbers.Add(rightLine.ToString());
                        leftLine++;
                        rightLine++;
                    }
                }
            }

            FitBothSide(leftData, leftNumbers, rightData, rightNumbers);

            if (leftData.Count == 0) {
                MakeParagraph(leftData, "NOT SUPPORTED OR NO DATA", LineMode.Indicator);
                MakeParagraph(rightData, "NOT SUPPORTED OR NO DATA", LineMode.Indicator);
                leftNumbers.Add("");
                rightNumbers.Add("");
            }

            leftLineNumber.Text = string.Join("\n", leftNumbers);
            rightLineNumber.Text = string.Join("\n", rightNumbers);
            leftText.Document.PageWidth = minWidth + 16;
            rightText.Document.PageWidth = minWidth + 16;
            leftText.Document.Blocks.AddRange(leftData);
            rightText.Document.Blocks.AddRange(rightData);
            leftText.ScrollToHome();

            mask.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        ///     Reset data.
        /// </summary>
        public void Reset() {
            mask.Visibility = Visibility.Visible;
        }

        /// <summary>
        ///     Make paragraph.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="content"></param>
        /// <param name="mode"></param>
        private void MakeParagraph(List<Paragraph> collection, string content, LineMode mode) {
            Paragraph p = new Paragraph(new Run(content));
            p.Margin = new Thickness(0);
            p.Padding = new Thickness();
            p.LineHeight = 1;
            p.Background = Brushes.Transparent;
            p.Foreground = FindResource("Brush.FG") as SolidColorBrush;
            p.FontStyle = FontStyles.Normal;
            
            switch (mode) {
            case LineMode.Normal:
                break;
            case LineMode.Indicator:
                p.Foreground = Brushes.Gray;
                p.FontStyle = FontStyles.Italic;
                break;
            case LineMode.Empty:
                p.Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
                break;
            case LineMode.Added:
                p.Background = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
                break;
            case LineMode.Deleted:
                p.Background = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));
                break;
            }

            var formatter = new FormattedText(
                content,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(leftText.FontFamily, p.FontStyle, p.FontWeight, p.FontStretch),
                leftText.FontSize,
                Brushes.Black,
                new NumberSubstitution(),
                TextFormattingMode.Ideal);

            if (minWidth < formatter.Width) minWidth = formatter.Width;
            collection.Add(p);
        }

        /// <summary>
        ///     Fit both side with empty lines.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="leftNumbers"></param>
        /// <param name="right"></param>
        /// <param name="rightNumbers"></param>
        private void FitBothSide(List<Paragraph> left, List<string> leftNumbers, List<Paragraph> right, List<string> rightNumbers) {
            int leftCount = left.Count;
            int rightCount = right.Count;
            int diff = 0;
            List<Paragraph> fitContent = null;
            List<string> fitNumber = null;

            if (leftCount > rightCount) {
                diff = leftCount - rightCount;
                fitContent = right;
                fitNumber = rightNumbers;
            } else if (rightCount > leftCount) {
                diff = rightCount - leftCount;
                fitContent = left;
                fitNumber = leftNumbers;
            }

            for (int i = 0; i < diff; i++) {
                MakeParagraph(fitContent, "", LineMode.Empty);
                fitNumber.Add("");
            }
        }

        /// <summary>
        ///     Sync scroll both sides.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnViewerScroll(object sender, ScrollChangedEventArgs e) {
            if (e.VerticalChange != 0) {
                if (leftText.VerticalOffset != e.VerticalOffset) {
                    leftText.ScrollToVerticalOffset(e.VerticalOffset);
                }

                if (rightText.VerticalOffset != e.VerticalOffset) {
                    rightText.ScrollToVerticalOffset(e.VerticalOffset);
                }

                leftLineNumber.Margin = new Thickness(4, -e.VerticalOffset, 4, 0);
                rightLineNumber.Margin = new Thickness(4, -e.VerticalOffset, 4, 0);
            } else {
                if (leftText.HorizontalOffset != e.HorizontalOffset) {
                    leftText.ScrollToHorizontalOffset(e.HorizontalOffset);
                }

                if (rightText.HorizontalOffset != e.HorizontalOffset) {
                    rightText.ScrollToHorizontalOffset(e.HorizontalOffset);
                }
            }
        }

        /// <summary>
        ///     Scroll using mouse wheel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnViewerMouseWheel(object sender, MouseWheelEventArgs e) {
            var text = sender as RichTextBox;
            if (text == null) return;

            if (e.Delta > 0) {
                text.LineUp();
            } else {
                text.LineDown();
            }

            e.Handled = true;
        }

        private void LeftSizeChanged(object sender, SizeChangedEventArgs e) {
            if (leftText.Document.PageWidth < leftText.ActualWidth) {
                leftText.Document.PageWidth = leftText.ActualWidth;
            }
        }

        private void RightSizeChanged(object sender, SizeChangedEventArgs e) {
            if (rightText.Document.PageWidth < rightText.ActualWidth) {
                rightText.Document.PageWidth = rightText.ActualWidth;
            }
        }
    }
}
