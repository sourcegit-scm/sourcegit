using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views {
    /// <summary>
    ///     修改仓库显示名称
    /// </summary>
    public partial class EditRepositoryDisplayName : Controls.Window {
        private Models.Repository repository = null;

        public string NewName {
            get;
            set;
        }

        public EditRepositoryDisplayName(Models.Repository repository) {
            this.repository = repository;
            NewName = repository.Name;
            InitializeComponent();
        }

        private void OnSure(object s, RoutedEventArgs e) {
            txtName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtName)) return;
            repository.Name = NewName;
            DialogResult = true;
            Close();
        }

        private void OnCancel(object s, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
