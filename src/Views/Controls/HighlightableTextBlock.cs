using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SourceGit.Views.Controls {
    /// <summary>
    ///     支持部分高亮的文本组件
    /// </summary>
    public class HighlightableTextBlock : TextBlock {
        private static readonly Brush BG_EMPTY = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
        private static readonly Brush BG_ADDED = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
        private static readonly Brush BG_DELETED = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));
        private static readonly Brush HL_ADDED = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));
        private static readonly Brush HL_DELETED = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            "Data",
            typeof(Models.TextChanges.Line),
            typeof(HighlightableTextBlock),
            new PropertyMetadata(null, OnContentChanged));

        public Models.TextChanges.Line Data {
            get { return (Models.TextChanges.Line)GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var txt = d as HighlightableTextBlock;
            if (txt == null) return;

            txt.Inlines.Clear();
            txt.Text = null;
            txt.Background = Brushes.Transparent;
            txt.FontStyle = FontStyles.Normal;

            if (txt.Data == null) return;

            Brush highlightBrush = Brushes.Transparent;
            switch (txt.Data.Mode) {
            case Models.TextChanges.LineMode.None:
                txt.Background = BG_EMPTY;
                break;
            case Models.TextChanges.LineMode.Indicator:
                txt.FontStyle = FontStyles.Italic;
                break;
            case Models.TextChanges.LineMode.Added:
                txt.Background = BG_ADDED;
                highlightBrush = HL_ADDED;
                break;
            case Models.TextChanges.LineMode.Deleted:
                txt.Background = BG_DELETED;
                highlightBrush = HL_DELETED;
                break;
            default:
                break;
            }

            txt.SetResourceReference(ForegroundProperty, txt.Data.Mode == Models.TextChanges.LineMode.Indicator ? "Brush.FG2" : "Brush.FG1");

            if (txt.Data.Highlights == null || txt.Data.Highlights.Count == 0) {
                txt.Text = txt.Data.Content;
                return;
            }

            var started = 0;
            foreach (var highlight in txt.Data.Highlights) {
                if (started < highlight.Start) {
                    txt.Inlines.Add(new Run(txt.Data.Content.Substring(started, highlight.Start - started)));
                }

                txt.Inlines.Add(new Run() {
                    Background = highlightBrush,
                    Text = txt.Data.Content.Substring(highlight.Start, highlight.Count),
                });

                started = highlight.Start + highlight.Count;
            }

            if (started < txt.Data.Content.Length) {
                txt.Inlines.Add(new Run(txt.Data.Content.Substring(started)));
            }
        }
    }
}
