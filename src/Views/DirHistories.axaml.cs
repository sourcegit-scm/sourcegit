using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class DirHistories : ChromelessWindow
    {
        public DirHistories()
        {
            InitializeComponent();
        }

        private void OnPressCommitSHA(object sender, PointerPressedEventArgs e)
        {
            if (sender is TextBlock { DataContext: Models.Commit commit } &&
                DataContext is ViewModels.DirHistories vm)
            {
                vm.NavigateToCommit(commit);
            }

            e.Handled = true;
        }

        private void OnCommitSubjectDataContextChanged(object sender, EventArgs e)
        {
            if (sender is Border border)
                ToolTip.SetTip(border, null);
        }

        private void OnCommitSubjectPointerMoved(object sender, PointerEventArgs e)
        {
            if (sender is Border { DataContext: Models.Commit commit } border &&
                DataContext is ViewModels.DirHistories vm)
            {
                var tooltip = ToolTip.GetTip(border);
                if (tooltip == null)
                    ToolTip.SetTip(border, vm.GetCommitFullMessage(commit));
            }
        }
    }
}


