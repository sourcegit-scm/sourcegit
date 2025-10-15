using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace SourceGit.Views
{
    public partial class CommitSubjectPresenter : Control
    {
        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            AvaloniaProperty.Register<CommitSubjectPresenter, FontFamily>(nameof(FontFamily));

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty =
            AvaloniaProperty.Register<CommitSubjectPresenter, FontFamily>(nameof(CodeFontFamily));

        public FontFamily CodeFontFamily
        {
            get => GetValue(CodeFontFamilyProperty);
            set => SetValue(CodeFontFamilyProperty, value);
        }

        public static readonly StyledProperty<double> FontSizeProperty =
           TextBlock.FontSizeProperty.AddOwner<CommitSubjectPresenter>();

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public static readonly StyledProperty<FontWeight> FontWeightProperty =
           TextBlock.FontWeightProperty.AddOwner<CommitSubjectPresenter>();

        public FontWeight FontWeight
        {
            get => GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        public static readonly StyledProperty<IBrush> InlineCodeBackgroundProperty =
            AvaloniaProperty.Register<CommitSubjectPresenter, IBrush>(nameof(InlineCodeBackground), Brushes.Transparent);

        public IBrush InlineCodeBackground
        {
            get => GetValue(InlineCodeBackgroundProperty);
            set => SetValue(InlineCodeBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<CommitSubjectPresenter, IBrush>(nameof(Foreground), Brushes.White);

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> LinkForegroundProperty =
            AvaloniaProperty.Register<CommitSubjectPresenter, IBrush>(nameof(LinkForeground), Brushes.White);

        public IBrush LinkForeground
        {
            get => GetValue(LinkForegroundProperty);
            set => SetValue(LinkForegroundProperty, value);
        }

        public static readonly StyledProperty<string> SubjectProperty =
            AvaloniaProperty.Register<CommitSubjectPresenter, string>(nameof(Subject));

        public string Subject
        {
            get => GetValue(SubjectProperty);
            set => SetValue(SubjectProperty, value);
        }

        public static readonly StyledProperty<AvaloniaList<Models.IssueTracker>> IssueTrackersProperty =
            AvaloniaProperty.Register<CommitSubjectPresenter, AvaloniaList<Models.IssueTracker>>(nameof(IssueTrackers));

        public AvaloniaList<Models.IssueTracker> IssueTrackers
        {
            get => GetValue(IssueTrackersProperty);
            set => SetValue(IssueTrackersProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            if (_needRebuildInlines)
            {
                _needRebuildInlines = false;
                GenerateFormattedTextElements();
            }

            if (_inlines.Count == 0)
                return;

            var ro = new RenderOptions()
            {
                TextRenderingMode = TextRenderingMode.SubpixelAntialias,
                EdgeMode = EdgeMode.Antialias
            };

            using (context.PushRenderOptions(ro))
            {
                var height = Bounds.Height;
                var width = Bounds.Width;
                foreach (var inline in _inlines)
                {
                    if (inline.X > width)
                        return;

                    if (inline.Element is { Type: Models.InlineElementType.Code })
                    {
                        var rect = new Rect(inline.X, (height - inline.Text.Height - 2) * 0.5, inline.Text.WidthIncludingTrailingWhitespace + 8, inline.Text.Height + 2);
                        var roundedRect = new RoundedRect(rect, new CornerRadius(4));
                        context.DrawRectangle(InlineCodeBackground, null, roundedRect);
                        context.DrawText(inline.Text, new Point(inline.X + 4, (height - inline.Text.Height) * 0.5));
                    }
                    else
                    {
                        context.DrawText(inline.Text, new Point(inline.X, (height - inline.Text.Height) * 0.5));
                    }
                }
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SubjectProperty)
            {
                _needRebuildInlines = true;
                GenerateInlineElements();
                InvalidateVisual();
            }
            else if (change.Property == IssueTrackersProperty)
            {
                if (change.OldValue is AvaloniaList<Models.IssueTracker> oldValue)
                    oldValue.CollectionChanged -= OnIssueTrackersChanged;
                if (change.NewValue is AvaloniaList<Models.IssueTracker> newValue)
                    newValue.CollectionChanged += OnIssueTrackersChanged;

                OnIssueTrackersChanged(null, null);
            }
            else if (change.Property == FontFamilyProperty ||
                change.Property == CodeFontFamilyProperty ||
                change.Property == FontSizeProperty ||
                change.Property == FontWeightProperty ||
                change.Property == ForegroundProperty ||
                change.Property == LinkForegroundProperty)
            {
                _needRebuildInlines = true;
                InvalidateVisual();
            }
            else if (change.Property == InlineCodeBackgroundProperty)
            {
                InvalidateVisual();
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            var point = e.GetPosition(this);
            foreach (var inline in _inlines)
            {
                if (inline.Element is not { Type: Models.InlineElementType.Link } link)
                    continue;

                if (inline.X > point.X || inline.X + inline.Text.WidthIncludingTrailingWhitespace < point.X)
                    continue;

                _lastHover = link;
                SetCurrentValue(CursorProperty, Cursor.Parse("Hand"));
                ToolTip.SetTip(this, link.Link);
                e.Handled = true;
                return;
            }

            ClearHoveredIssueLink();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (_lastHover != null)
                Native.OS.OpenBrowser(_lastHover.Link);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            ClearHoveredIssueLink();
        }

        private void OnIssueTrackersChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _needRebuildInlines = true;
            GenerateInlineElements();
            InvalidateVisual();
        }

        private void GenerateInlineElements()
        {
            _elements.Clear();
            ClearHoveredIssueLink();

            var subject = Subject;
            if (string.IsNullOrEmpty(subject))
            {
                _needRebuildInlines = true;
                InvalidateVisual();
                return;
            }

            var rules = IssueTrackers ?? [];
            foreach (var rule in rules)
                rule.Matches(_elements, subject);

            var keywordMatch = REG_KEYWORD_FORMAT1().Match(subject);
            if (!keywordMatch.Success)
                keywordMatch = REG_KEYWORD_FORMAT2().Match(subject);

            if (keywordMatch.Success && _elements.Intersect(0, keywordMatch.Length) == null)
                _elements.Add(new Models.InlineElement(Models.InlineElementType.Keyword, 0, keywordMatch.Length, string.Empty));

            var codeMatches = REG_INLINECODE_FORMAT().Matches(subject);
            foreach (Match match in codeMatches)
            {
                var start = match.Index;
                var len = match.Length;
                if (_elements.Intersect(start, len) != null)
                    continue;

                _elements.Add(new Models.InlineElement(Models.InlineElementType.Code, start, len, string.Empty));
            }

            _elements.Sort();
        }

        private void GenerateFormattedTextElements()
        {
            _inlines.Clear();

            var subject = Subject;
            if (string.IsNullOrEmpty(subject))
                return;

            var fontFamily = FontFamily;
            var codeFontFamily = CodeFontFamily;
            var fontSize = FontSize;
            var foreground = Foreground;
            var linkForeground = LinkForeground;
            var typeface = new Typeface(fontFamily, FontStyle.Normal, FontWeight);
            var codeTypeface = new Typeface(codeFontFamily, FontStyle.Normal, FontWeight);
            var pos = 0;
            var x = 0.0;
            for (var i = 0; i < _elements.Count; i++)
            {
                var elem = _elements[i];
                if (elem.Start > pos)
                {
                    var normal = new FormattedText(
                        subject.Substring(pos, elem.Start - pos),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        fontSize,
                        foreground);

                    _inlines.Add(new Inline(x, normal, null));
                    x += normal.WidthIncludingTrailingWhitespace;
                }

                if (elem.Type == Models.InlineElementType.Keyword)
                {
                    var keyword = new FormattedText(
                        subject.Substring(elem.Start, elem.Length),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(fontFamily, FontStyle.Normal, FontWeight.Bold),
                        fontSize,
                        foreground);
                    _inlines.Add(new Inline(x, keyword, elem));
                    x += keyword.WidthIncludingTrailingWhitespace;
                }
                else if (elem.Type == Models.InlineElementType.Link)
                {
                    var link = new FormattedText(
                        subject.Substring(elem.Start, elem.Length),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        fontSize,
                        linkForeground);
                    _inlines.Add(new Inline(x, link, elem));
                    x += link.WidthIncludingTrailingWhitespace;
                }
                else if (elem.Type == Models.InlineElementType.Code)
                {
                    var link = new FormattedText(
                        subject.Substring(elem.Start + 1, elem.Length - 2),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        codeTypeface,
                        fontSize - 0.5,
                        foreground);
                    _inlines.Add(new Inline(x, link, elem));
                    x += link.WidthIncludingTrailingWhitespace + 8;
                }

                pos = elem.Start + elem.Length;
            }

            if (pos < subject.Length)
            {
                var normal = new FormattedText(
                        subject.Substring(pos),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        fontSize,
                        foreground);

                _inlines.Add(new Inline(x, normal, null));
            }
        }

        private void ClearHoveredIssueLink()
        {
            if (_lastHover != null)
            {
                ToolTip.SetTip(this, null);
                SetCurrentValue(CursorProperty, Cursor.Parse("Arrow"));
                _lastHover = null;
            }
        }

        [GeneratedRegex(@"`.*?`")]
        private static partial Regex REG_INLINECODE_FORMAT();

        [GeneratedRegex(@"^\[[\w\s]+\]")]
        private static partial Regex REG_KEYWORD_FORMAT1();

        [GeneratedRegex(@"^\S+([\<\(][\w\s_\-\*,]+[\>\)])?\!?\s?:\s")]
        private static partial Regex REG_KEYWORD_FORMAT2();

        private class Inline
        {
            public double X { get; set; } = 0;
            public FormattedText Text { get; set; } = null;
            public Models.InlineElement Element { get; set; } = null;

            public Inline(double x, FormattedText text, Models.InlineElement elem)
            {
                X = x;
                Text = text;
                Element = elem;
            }
        }

        private Models.InlineElementCollector _elements = new();
        private List<Inline> _inlines = [];
        private Models.InlineElement _lastHover = null;
        private bool _needRebuildInlines = false;
    }
}
