using System;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace SourceGit.Views
{
    public partial class MarkdownResultView : UserControl
    {
        public static readonly StyledProperty<string> MarkdownProperty =
            AvaloniaProperty.Register<MarkdownResultView, string>(nameof(Markdown), string.Empty);

        public string Markdown
        {
            get => GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        public MarkdownResultView()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == MarkdownProperty)
                RenderMarkdown(Markdown);
        }

        private void RenderMarkdown(string markdown)
        {
            ContentPanel.Children.Clear();
            if (string.IsNullOrEmpty(markdown))
                return;

            var codeBg = this.FindResource("Brush.Border2") as IBrush ?? Brushes.Gray;
            var codeFg = this.FindResource("Brush.FG1") as IBrush ?? Brushes.Black;
            var codeBlockBorder = this.FindResource("Brush.Border1") as IBrush ?? Brushes.Gray;

            var lines = markdown.ReplaceLineEndings("\n").Split('\n');
            var i = 0;
            while (i < lines.Length)
            {
                if (string.IsNullOrEmpty(lines[i]))
                {
                    i++;
                    continue;
                }

                if (IsFencedCodeBlock(lines, i, out var codeLines, out i))
                {
                    ContentPanel.Children.Add(CreateCodeBlock(codeLines, codeFg, codeBlockBorder));
                    continue;
                }

                var line = lines[i];
                if (IsSectionHeading(line))
                {
                    ContentPanel.Children.Add(CreateHeading(line));
                }
                else if (line.TrimStart().StartsWith('-'))
                {
                    ContentPanel.Children.Add(CreateBulletItem(line));
                }
                else
                {
                    ContentPanel.Children.Add(CreateParagraph(line));
                }
                i++;
            }

            _codeBg = codeBg;
            _codeFg = codeFg;
        }

        private static bool IsFencedCodeBlock(string[] lines, int start, out string[] codeLines, out int end)
        {
            codeLines = null;
            end = start;
            var trimmed = lines[start].Trim();
            if (!trimmed.StartsWith("```"))
                return false;

            var count = 1;
            for (var j = start + 1; j < lines.Length; j++)
            {
                if (lines[j].Trim().StartsWith("```"))
                {
                    end = j + 1;
                    codeLines = new string[j - start - 1];
                    Array.Copy(lines, start + 1, codeLines, 0, codeLines.Length);
                    return true;
                }
                count++;
            }

            codeLines = new string[lines.Length - start - 1];
            Array.Copy(lines, start + 1, codeLines, 0, codeLines.Length);
            end = lines.Length;
            return true;
        }

        private static bool IsSectionHeading(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 3)
                return false;
            if (!char.IsDigit(trimmed[0]))
                return false;
            var dotIdx = trimmed.IndexOf('.');
            return dotIdx > 0 && dotIdx <= 2;
        }

        private Control CreateHeading(string line)
        {
            var block = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeight.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 6, 0, 2),
            };
            BuildInlines(block.Inlines, line.Trim());
            return block;
        }

        private Control CreateBulletItem(string line)
        {
            var block = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 1, 0, 1),
            };
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- "))
                trimmed = trimmed.Substring(2);
            else if (trimmed.StartsWith('-'))
                trimmed = trimmed.Substring(1);
            BuildInlines(block.Inlines, "• " + trimmed);
            return block;
        }

        private Control CreateParagraph(string line)
        {
            var block = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 1),
            };
            BuildInlines(block.Inlines, line.Trim());
            return block;
        }

        private Control CreateCodeBlock(string[] lines, IBrush fg, IBrush border)
        {
            var builder = new StringBuilder();
            foreach (var l in lines)
                builder.AppendLine(l);
            var code = builder.ToString().TrimEnd();

            var block = new SelectableTextBlock
            {
                Text = code,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"),
                FontSize = 12,
                Foreground = fg,
                Padding = new Thickness(8, 4),
            };
            return new Border
            {
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 4, 0, 4),
                Child = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = block,
                },
            };
        }

        private void BuildInlines(InlineCollection inlines, string text)
        {
            var bg = _codeBg ?? this.FindResource("Brush.Border2") as IBrush ?? Brushes.Gray;
            var fg = _codeFg ?? this.FindResource("Brush.FG2") as IBrush ?? Brushes.DimGray;

            var i = 0;
            while (i < text.Length)
            {
                if (i + 1 < text.Length && text[i] == '`')
                {
                    var end = text.IndexOf('`', i + 1);
                    if (end > i)
                    {
                        var code = text.Substring(i + 1, end - i - 1);
                        inlines.Add(new Run(code)
                        {
                            FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"),
                            FontSize = 12,
                            Background = bg,
                            Foreground = fg,
                        });
                        i = end + 1;
                        continue;
                    }
                }

                if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
                {
                    var end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                    if (end > i)
                    {
                        var bold = text.Substring(i + 2, end - i - 2);
                        inlines.Add(new Run(bold) { FontWeight = FontWeight.Bold });
                        i = end + 2;
                        continue;
                    }
                }

                var nextBold = text.IndexOf("**", i, StringComparison.Ordinal);
                var nextCode = text.IndexOf('`', i);
                var next = (nextBold >= 0 && nextCode >= 0) ? Math.Min(nextBold, nextCode)
                         : nextBold >= 0 ? nextBold
                         : nextCode;

                if (next > i)
                {
                    inlines.Add(new Run(text.Substring(i, next - i)));
                    i = next;
                }
                else if (next < 0)
                {
                    inlines.Add(new Run(text.Substring(i)));
                    break;
                }
                else
                {
                    i++;
                }
            }
        }

        private IBrush _codeBg;
        private IBrush _codeFg;
    }
}
