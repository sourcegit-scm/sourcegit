using Avalonia.Controls;

namespace SourceGit.Views
{
    public partial class Statistics : ChromelessWindow
    {
        public Statistics()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        private void OnAuthorSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.Statistics vm && sender is ListBox listBox)
                vm.ChangeAuthor(listBox.SelectedItem as Models.StatisticsAuthor);
        }
    }
}
