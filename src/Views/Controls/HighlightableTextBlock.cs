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

        public class Data {
            public string Text { get; set; } = "";
            public List<Models.TextChanges.HighlightRange> Highlights { get; set; } = new List<Models.TextChanges.HighlightRange>();
            public Brush HighlightBrush { get; set; } = Brushes.Transparent;
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
            if (txt.Content == null) return;

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
                    Background = txt.Content.HighlightBrush,
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
