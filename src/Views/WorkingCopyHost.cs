using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    // Transfer a single editor between the sidebar and history details. Only one set of
    // editor/diff hotkeys can be attached, and editor state survives changing pages.
    public class WorkingCopyHost : ContentControl
    {
        public static readonly StyledProperty<bool> ActiveProperty =
            AvaloniaProperty.Register<WorkingCopyHost, bool>(nameof(Active));

        public bool Active
        {
            get => GetValue(ActiveProperty);
            set => SetValue(ActiveProperty, value);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            UpdateContent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ActiveProperty && IsLoaded)
                UpdateContent();
        }

        private void UpdateContent()
        {
            if (!Active)
            {
                Content = null;
                return;
            }

            if (this.FindAncestorOfType<Repository>() is not { } owner)
                return;

            var view = owner.SharedWorkingCopy;
            if (view.Parent is WorkingCopyHost previous && previous != this)
                previous.Content = null;
            Content = view;
        }
    }
}
