using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Documents;

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

        public class Data {
            public Models.TextChanges.LineMode Mode { get; set; } = Models.TextChanges.LineMode.None;
            public string Text { get; set; } = "";
            public List<Models.TextChanges.HighlightRange> Highlights { get; set; } = new List<Models.TextChanges.HighlightRange>();

            public bool IsContent {
                get {
                    return Mode == Models.TextChanges.LineMode.Added
                        || Mode == Models.TextChanges.LineMode.Deleted
                        || Mode == Models.TextChanges.LineMode.Normal;
                }
            }

            public bool IsDifference {
                get {
                    return Mode == Models.TextChanges.LineMode.Added
                        || Mode == Models.TextChanges.LineMode.Deleted
                        || Mode == Models.TextChanges.LineMode.None;
                }
            }

            public string FG {
                get { return Mode == Models.TextChanges.LineMode.Indicator ? "Brush.FG2" : "Brush.FG1"; }
            }
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
            "Content", 
            typeof(Data),
            typeof(HighlightableTextBlock), 
            new PropertyMetadata(null, OnContentChanged));

        public Data Content {
            get { return (Data)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var txt = d as HighlightableTextBlock;
            if (txt == null) return;

            txt.Inlines.Clear();
            txt.Text = null;
            txt.Background = Brushes.Transparent;
            txt.FontStyle = FontStyles.Normal;

            if (txt.Content == null) return;

            Brush highlightBrush = Brushes.Transparent;
            switch (txt.Content.Mode) {
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

            txt.SetResourceReference(ForegroundProperty, txt.Content.FG);

            if (txt.Content.Highlights == null || txt.Content.Highlights.Count == 0) {
                txt.Text = txt.Content.Text;
                return;
            }

            var started = 0;
            foreach (var highlight in txt.Content.Highlights) {
                if (started < highlight.Start) {
                    txt.Inlines.Add(new Run(txt.Content.Text.Substring(started, highlight.Start - started)));
                }

                txt.Inlines.Add(new TextBlock() {
                    Background = highlightBrush,
                    LineHeight = txt.LineHeight,
                    Text = txt.Content.Text.Substring(highlight.Start, highlight.Count),
                });

                started = highlight.Start + highlight.Count;
            }

            if (started < txt.Content.Text.Length) {
                txt.Inlines.Add(new Run(txt.Content.Text.Substring(started)));
            }
        }
    }
}
