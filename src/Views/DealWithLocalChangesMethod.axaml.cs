using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class DealWithLocalChangesMethod : UserControl
    {
        public static readonly StyledProperty<Models.DealWithLocalChanges> MethodProperty =
            AvaloniaProperty.Register<DealWithLocalChangesMethod, Models.DealWithLocalChanges>(nameof(Method), Models.DealWithLocalChanges.DoNothing);

        public Models.DealWithLocalChanges Method
        {
            get => GetValue(MethodProperty);
            set => SetValue(MethodProperty, value);
        }

        public DealWithLocalChangesMethod()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == MethodProperty)
                UpdateRadioButtons();
        }

        private void OnRadioButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { Tag: Models.DealWithLocalChanges way })
            {
                Method = way;
                UpdateRadioButtons();
                e.Handled = true;
            }
        }

        private void UpdateRadioButtons()
        {
            switch (Method)
            {
                case Models.DealWithLocalChanges.DoNothing:
                    RadioDoNothing.IsChecked = true;
                    RadioStashAndReapply.IsChecked = false;
                    RadioDiscard.IsChecked = false;
                    break;
                case Models.DealWithLocalChanges.StashAndReapply:
                    RadioDoNothing.IsChecked = false;
                    RadioStashAndReapply.IsChecked = true;
                    RadioDiscard.IsChecked = false;
                    break;
                default:
                    RadioDoNothing.IsChecked = false;
                    RadioStashAndReapply.IsChecked = false;
                    RadioDiscard.IsChecked = true;
                    break;
            }
        }
    }
}
