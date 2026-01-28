using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;

namespace SourceGit.Views
{
    public class CommandLogContentPresenter : TextEditor, Models.ICommandLogReceiver
    {
        public class LineStyleTransformer : DocumentColorizingTransformer
        {
            protected override void ColorizeLine(DocumentLine line)
            {
                var content = CurrentContext.Document.GetText(line);
                if (content.StartsWith("$ git ", StringComparison.Ordinal))
                {
                    ChangeLinePart(line.Offset, line.Offset + 1, v =>
                    {
                        v.TextRunProperties.SetForegroundBrush(Brushes.Orange);
                    });

                    ChangeLinePart(line.Offset + 2, line.EndOffset, v =>
                    {
                        var old = v.TextRunProperties.Typeface;
                        v.TextRunProperties.SetTypeface(new Typeface(old.FontFamily, old.Style, FontWeight.Bold));
                    });
                }
                else if (content.StartsWith("remote: ", StringComparison.Ordinal))
                {
                    ChangeLinePart(line.Offset, line.Offset + 7, v =>
                    {
                        v.TextRunProperties.SetForegroundBrush(Brushes.SeaGreen);
                    });
                }
                else
                {
                    foreach (var err in _errors)
                    {
                        var idx = content.IndexOf(err, StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            ChangeLinePart(line.Offset + idx, line.Offset + err.Length + 1, v =>
                            {
                                var old = v.TextRunProperties.Typeface;
                                v.TextRunProperties.SetForegroundBrush(Brushes.Red);
                                v.TextRunProperties.SetTypeface(new Typeface(old.FontFamily, old.Style, FontWeight.Bold));
                            });
                        }
                    }
                }
            }

            private readonly List<string> _errors = ["! [rejected]", "! [remote rejected]"];
        }

        public static readonly StyledProperty<ViewModels.CommandLog> LogProperty =
            AvaloniaProperty.Register<CommandLogContentPresenter, ViewModels.CommandLog>(nameof(Log));

        public ViewModels.CommandLog Log
        {
            get => GetValue(LogProperty);
            set => SetValue(LogProperty, value);
        }

        public static readonly StyledProperty<string> PureTextProperty =
            AvaloniaProperty.Register<CommandLogContentPresenter, string>(nameof(PureText));

        public string PureText
        {
            get => GetValue(PureTextProperty);
            set => SetValue(PureTextProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public CommandLogContentPresenter() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = false;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
            TextArea.TextView.Options.AllowScrollBelowDocument = false;
        }

        public void OnReceiveCommandLog(string line)
        {
            AppendText("\n");
            AppendText(line);
            ScrollToEnd();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (_textMate == null)
            {
                _textMate = Models.TextMateHelper.CreateForEditor(this);
                Models.TextMateHelper.SetGrammarByFileName(_textMate, "Log.log");
                TextArea.TextView.LineTransformers.Add(new LineStyleTransformer());
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
            }

            GC.Collect();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == LogProperty)
            {
                if (change.OldValue is ViewModels.CommandLog oldLog)
                    oldLog.Unsubscribe(this);

                if (change.NewValue is ViewModels.CommandLog newLog)
                {
                    Text = newLog.Content;
                    newLog.Subscribe(this);
                }
                else
                {
                    Text = string.Empty;
                }
            }
            else if (change.Property == PureTextProperty)
            {
                if (!string.IsNullOrEmpty(PureText))
                    Text = PureText;
            }
        }

        private TextMate.Installation _textMate = null;
    }
}
