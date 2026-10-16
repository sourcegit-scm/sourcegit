using System;
using System.Diagnostics;
using System.Net.Mail;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;

using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace SourceGit.Views
{
    public class CommitMessageInlineLink : TextBlock
    {
        protected override Type StyleKeyOverride => typeof(TextBlock);

        public CommitMessageInlineLink(string url)
        {
            Cursor = Cursor.Parse("Hand");
            Classes.Add(url.StartsWith("commit://", StringComparison.Ordinal) ? "commit_link" : "link");
            _url = url;
            _tooltip = null;
        }

        protected override async void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);

            if (_tooltip != null || string.IsNullOrEmpty(_url))
                return;

            if (_url.StartsWith("commit://", StringComparison.Ordinal))
            {
                var commitHash = _url.Substring("commit://".Length);
                var parentView = this.FindAncestorOfType<CommitBaseInfo>();
                if (parentView is not { DataContext: ViewModels.CommitDetail detail })
                    return;

                _tooltip = await detail.GetCommitAsync(commitHash);
            }
            else
            {
                _tooltip = _url;
            }

            ToolTip.SetTip(this, _tooltip);
            ToolTip.SetIsOpen(this, IsPointerOver);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (string.IsNullOrEmpty(_url))
                return;

            if (_url.StartsWith("commit://", StringComparison.Ordinal))
            {
                var commitHash = _url.Substring("commit://".Length);
                var parentView = this.FindAncestorOfType<CommitBaseInfo>();
                if (parentView is { DataContext: ViewModels.CommitDetail detail })
                    detail.NavigateTo(commitHash);

                return;
            }

            if (_url.StartsWith("mailto:", StringComparison.Ordinal))
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = _url,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                });

                return;
            }

            if (_url.IndexOf('@') > 0 && MailAddress.TryCreate(_url, out var _))
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = $"mailto:{_url}",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                });

                return;
            }

            if (!Uri.IsWellFormedUriString(_url, UriKind.Absolute) ||
                !Uri.TryCreate(_url, UriKind.Absolute, out var uri))
                return;

            if (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps ||
                uri.Scheme == Uri.UriSchemeFtp)
            {
                Native.OS.OpenBrowser(_url);
                return;
            }
        }

        private string _url;
        private object _tooltip;
    }

    public class CommitMessagePresenter : ContentControl
    {
        public static readonly DirectProperty<CommitMessagePresenter, string> MessageProperty =
            AvaloniaProperty.RegisterDirect<CommitMessagePresenter, string>(
                nameof(Message),
                static o => o.Message,
                static (o, v) => o.Message = v);

        public string Message
        {
            get => _message;
            set => SetAndRaise(MessageProperty, ref _message, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == MessageProperty)
                Content = BuildContent();
        }

        private Control BuildContent()
        {
            if (string.IsNullOrEmpty(_message))
                return null;

            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

            var document = Markdown.Parse(_message, pipeline);
            var panel = new StackPanel() { Orientation = Orientation.Vertical };
            foreach (var block in document)
                panel.Children.Add(BuildBlock(block, panel.Children.Count == 0 ? ApplyMargin.OnlyBottom : ApplyMargin.TopAndBottom));
            return panel;
        }

        private Control BuildBlock(Block block, ApplyMargin margin)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    return BuildHeading(heading, margin);
                case ThematicBreakBlock thematicBreak:
                    return BuildHorizontalLine(margin);
                case ParagraphBlock paragraph:
                    return BuildParagraph(paragraph, margin);
                case ListBlock list:
                    return BuildList(list, margin);
                case QuoteBlock quote:
                    return BuildQuote(quote, margin);
                case CodeBlock code:
                    return BuildCode(code, margin);
                case Table table:
                    return BuildTable(table, margin);
                default:
                    return new TextBlock()
                    {
                        Text = block.ToString(),
                        Margin = new Thickness(0, 4)
                    };
            }
        }

        private Control BuildHeading(HeadingBlock block, ApplyMargin margin)
        {
            var textBlock = new TextBlock() { TextWrapping = TextWrapping.NoWrap };
            switch (block.Level)
            {
                case 1:
                    textBlock.FontSize = 18;
                    textBlock.FontWeight = FontWeight.Bold;
                    break;
                case 2:
                    textBlock.FontSize = 17;
                    textBlock.FontWeight = FontWeight.Bold;
                    break;
                case 3:
                    textBlock.FontSize = 16;
                    textBlock.FontWeight = FontWeight.SemiBold;
                    break;
                case 4:
                    textBlock.FontSize = 15;
                    textBlock.FontWeight = FontWeight.SemiBold;
                    break;
                case 5:
                    textBlock.FontSize = 14;
                    textBlock.FontWeight = FontWeight.Medium;
                    break;
                case 6:
                    textBlock.FontSize = 13;
                    textBlock.FontWeight = FontWeight.Medium;
                    break;
            }

            if (block.Inline != null)
                BuildTextInline(textBlock.Inlines, block.Inline);

            textBlock.Margin = margin switch
            {
                ApplyMargin.None => new Thickness(0),
                ApplyMargin.OnlyBottom => new Thickness(0, 0, 0, 8),
                _ => new Thickness(0, 8)
            };

            return textBlock;
        }

        private Control BuildHorizontalLine(ApplyMargin margin)
        {
            var rect = new Rectangle()
            {
                Height = 1,
                Fill = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            rect.Margin = margin switch
            {
                ApplyMargin.None => new Thickness(0),
                ApplyMargin.OnlyBottom => new Thickness(0, 0, 0, 2),
                _ => new Thickness(0, 2),
            };

            return rect;
        }

        private Control BuildParagraph(ParagraphBlock block, ApplyMargin margin)
        {
            var textBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap };

            foreach (var inline in block.Inline)
                BuildTextInline(textBlock.Inlines, inline);

            textBlock.Margin = margin switch
            {
                ApplyMargin.None => new Thickness(0),
                ApplyMargin.OnlyBottom => new Thickness(0, 0, 0, 7),
                _ => new Thickness(0, 7)
            };

            return textBlock;
        }

        private Control BuildList(ListBlock block, ApplyMargin margin)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Star });

            grid.Margin = margin switch
            {
                ApplyMargin.None => new Thickness(0),
                ApplyMargin.OnlyBottom => new Thickness(0, 0, 0, 7),
                _ => new Thickness(0, 7)
            };

            var index = 0;
            foreach (var item in block)
            {
                if (item is not ListItemBlock listItem)
                    continue;

                var bullet = new TextBlock()
                {
                    Text = block.IsOrdered ? $"{index + 1}." : (block.Column / 2) % 2 == 0 ? "•" : "◦",
                    Width = 16,
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = VerticalAlignment.Top,
                };

                var itemContent = new StackPanel() { Orientation = Orientation.Vertical };
                foreach (var child in listItem)
                    itemContent.Children.Add(BuildBlock(child, ApplyMargin.None));

                Grid.SetColumn(bullet, 0);
                Grid.SetRow(bullet, index);
                Grid.SetRow(itemContent, index);
                Grid.SetColumn(itemContent, 1);

                grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                grid.Children.Add(bullet);
                grid.Children.Add(itemContent);

                index++;
            }

            return grid;
        }

        private Control BuildQuote(QuoteBlock block, ApplyMargin margin)
        {
            var border = new Border()
            {
                BorderThickness = new Thickness(4, 0, 0, 0),
                BorderBrush = Brushes.Green,
                Background = Brushes.Transparent,
            };

            border.Margin = margin switch
            {
                ApplyMargin.None => new Thickness(0),
                ApplyMargin.OnlyBottom => new Thickness(0, 0, 0, 7),
                _ => new Thickness(0, 7)
            };

            var panel = new StackPanel()
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10, 0, 8, 0)
            };

            foreach (var child in block)
                panel.Children.Add(BuildBlock(child, ApplyMargin.TopAndBottom));

            border.Child = panel;
            return border;
        }

        private Control BuildCode(CodeBlock block, ApplyMargin margin)
        {
            var border = new Border()
            {
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Colors.Black, 0.15),
                Padding = new Thickness(8, 6),
                CornerRadius = new CornerRadius(4)
            };

            border.Margin = margin switch
            {
                ApplyMargin.None => new Thickness(0),
                ApplyMargin.OnlyBottom => new Thickness(0, 0, 0, 7),
                _ => new Thickness(0, 7)
            };

            var textBlock = new TextBlock()
            {
                Text = block.Lines.ToString(),
                TextWrapping = TextWrapping.Wrap
            };
            textBlock.Classes.Add("monospace");

            border.Child = textBlock;
            return border;
        }

        private Control BuildTable(Table table, ApplyMargin margin)
        {
            var grid = new Grid();

            grid.Margin = margin switch
            {
                ApplyMargin.None => new Thickness(0),
                ApplyMargin.OnlyBottom => new Thickness(0, 0, 0, 7),
                _ => new Thickness(0, 7)
            };

            int rowCount = 0;
            int colCount = 0;
            foreach (var row in table)
            {
                if (row is TableRow r)
                {
                    colCount = Math.Max(colCount, r.Count);
                    rowCount++;
                }
            }

            if (colCount == 0)
                return grid;

            for (int r = 0; r < rowCount; r++)
                grid.RowDefinitions.Add(new() { Height = GridLength.Auto });

            for (int c = 0; c < colCount; c++)
                grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });

            int rowIdx = 0;
            foreach (var block in table)
            {
                if (block is not TableRow row)
                    continue;

                int colIdx = 0;
                foreach (var child in row)
                {
                    if (child is not TableCell cell)
                    {
                        colIdx++;
                        continue;
                    }

                    var topBorderThickness = rowIdx == 0 ? 1.0 : 0.0;
                    var leftBorderThickness = colIdx == 0 ? 1.0 : 0.0;
                    var cellBorder = new Border()
                    {
                        BorderThickness = new Thickness(leftBorderThickness, topBorderThickness, 1, 1),
                        BorderBrush = Brushes.DarkGray,
                        Padding = new Thickness(4, 2),
                    };

                    if (row.IsHeader)
                        cellBorder.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Brush.TableHeaderBG"));

                    var cellContent = new TextBlock()
                    {
                        TextWrapping = TextWrapping.Wrap
                    };

                    foreach (var cellBlock in cell)
                    {
                        switch (cellBlock)
                        {
                            case ParagraphBlock paragraph:
                                {
                                    var span = new Span();
                                    foreach (var inline in paragraph.Inline)
                                        BuildTextInline(span.Inlines, inline);

                                    if (row.IsHeader)
                                        span.FontWeight = FontWeight.Bold;

                                    cellContent.Inlines.Add(span);
                                }
                                break;
                            case LeafBlock leaf:
                                {
                                    if (leaf.Inline != null)
                                    {
                                        var span = new Span();
                                        foreach (var inline in leaf.Inline)
                                            BuildTextInline(span.Inlines, inline);

                                        if (row.IsHeader)
                                            span.FontWeight = FontWeight.Bold;

                                        cellContent.Inlines.Add(span);
                                    }
                                    else
                                    {
                                        var run = new Run(leaf.ToString());
                                        if (row.IsHeader)
                                            run.FontWeight = FontWeight.Bold;
                                        cellContent.Inlines.Add(run);
                                    }
                                }
                                break;
                            default:
                                {
                                    var run = new Run(cellBlock.ToString());
                                    if (row.IsHeader)
                                        run.FontWeight = FontWeight.Bold;
                                    cellContent.Inlines.Add(run);
                                }
                                break;
                        }
                    }

                    if (table.ColumnDefinitions != null && colIdx < table.ColumnDefinitions.Count)
                    {
                        var colDef = table.ColumnDefinitions[colIdx];
                        cellContent.TextAlignment = colDef.Alignment switch
                        {
                            TableColumnAlign.Center => TextAlignment.Center,
                            TableColumnAlign.Right => TextAlignment.Right,
                            _ => TextAlignment.Left
                        };
                    }

                    cellBorder.Child = cellContent;
                    Grid.SetRow(cellBorder, rowIdx);
                    Grid.SetColumn(cellBorder, colIdx);
                    grid.Children.Add(cellBorder);

                    colIdx++;
                }

                rowIdx++;
            }

            return grid;
        }

        private void BuildTextInline(InlineCollection output, Markdig.Syntax.Inlines.Inline inline)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    output.Add(new Run(literal.Content.ToString()));
                    break;
                case LineBreakInline:
                    output.Add(new LineBreak());
                    break;
                case EmphasisInline emphasis:
                    {
                        var span = new Span();
                        foreach (var child in emphasis)
                            BuildTextInline(span.Inlines, child);

                        if (emphasis.DelimiterCount >= 2)
                            span.FontWeight = FontWeight.Bold;
                        else
                            span.FontStyle = FontStyle.Italic;

                        output.Add(span);
                    }
                    break;
                case CodeInline code:
                    {
                        var run = new Run(code.Content.ToString());
                        run.Classes.Add("inline_code");
                        output.Add(run);
                    }
                    break;
                case LinkInline link:
                    {
                        var linkTextBlock = new CommitMessageInlineLink(link.Url);
                        foreach (var childInline in link)
                            BuildTextInline(linkTextBlock.Inlines, childInline);

                        var container = new InlineUIContainer()
                        {
                            Child = linkTextBlock,
                            BaselineAlignment = BaselineAlignment.Subscript,
                        };

                        output.Add(container);
                    }
                    break;
                case AutolinkInline autolink:
                    {
                        var linkTextBlock = new CommitMessageInlineLink(autolink.Url);
                        linkTextBlock.Text = autolink.ToString();

                        var container = new InlineUIContainer()
                        {
                            Child = linkTextBlock,
                            BaselineAlignment = BaselineAlignment.Subscript,
                        };

                        output.Add(container);
                    }
                    break;
                case TaskList task:
                    {
                        var child = new TextBlock()
                        {
                            Text = task.Checked ? "☑" : "☐",
                            Cursor = Cursor.Parse("Hand"),
                        };
                        var container = new InlineUIContainer()
                        {
                            Child = child,
                            BaselineAlignment = BaselineAlignment.Subscript,
                        };
                        output.Add(container);
                    }
                    break;
                default:
                    output.Add(new Run(inline.ToString()));
                    break;
            }
            ;
        }

        private enum ApplyMargin
        {
            None = 0,
            OnlyBottom,
            TopAndBottom,
        }

        private string _message = string.Empty;
    }
}
