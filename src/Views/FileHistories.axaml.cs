using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class FileHistories : ChromelessWindow
    {
        public static readonly StyledProperty<bool> HasLeftCaptionButtonProperty =
            AvaloniaProperty.Register<FileHistories, bool>(nameof(HasLeftCaptionButton));

        public bool HasLeftCaptionButton
        {
            get => GetValue(HasLeftCaptionButtonProperty);
            set => SetValue(HasLeftCaptionButtonProperty, value);
        }
        
        public FileHistories()
        {
            if (OperatingSystem.IsMacOS())
                HasLeftCaptionButton = true;
            
            InitializeComponent();
        }
        
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            
            if (change.Property == WindowStateProperty)
                HasLeftCaptionButton = WindowState != WindowState.FullScreen;
        }

        private void OnPressCommitSHA(object sender, PointerPressedEventArgs e)
        {
            if (sender is TextBlock { DataContext: Models.Commit commit } &&
                DataContext is ViewModels.FileHistories vm)
            {
                vm.NavigateToCommit(commit);
            }

            e.Handled = true;
        }

        private void OnResetToSelectedRevision(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.FileHistories vm)
            {
                vm.ResetToSelectedRevision();
                NotifyDonePanel.IsVisible = true;
            }

            e.Handled = true;
        }

        private void OnCloseNotifyPanel(object _, PointerPressedEventArgs e)
        {
            NotifyDonePanel.IsVisible = false;
            e.Handled = true;
        }
    }
}
