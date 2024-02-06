using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views {
    public partial class CommitBaseInfo : UserControl {
        public CommitBaseInfo() {
            InitializeComponent();
        }

        private void OnParentSHAPressed(object sender, PointerPressedEventArgs e) {
            if (DataContext is ViewModels.CommitDetail detail) {
                detail.NavigateTo((sender as Control).DataContext as string);
            }
            e.Handled = true;
        }
    }
}
