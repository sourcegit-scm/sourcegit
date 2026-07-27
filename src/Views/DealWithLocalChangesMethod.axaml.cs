using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class DealWithLocalChangesMethod : UserControl
    {
        public static readonly DirectProperty<DealWithLocalChangesMethod, Models.DealWithLocalChanges> MethodProperty =
            AvaloniaProperty.RegisterDirect<DealWithLocalChangesMethod, Models.DealWithLocalChanges>(
                nameof(Method),
                static o => o.Method,
                static (o, v) => o.Method = v);

        public Models.DealWithLocalChanges Method
        {
            get => _method;
            set => SetAndRaise(MethodProperty, ref _method, value);
        }

        public DealWithLocalChangesMethod()
        {
            Focusable = true;
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == MethodProperty)
                UpdateRadioButtons();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Up)
            {
                if (Method == Models.DealWithLocalChanges.StashAndReapply)
                    Method = Models.DealWithLocalChanges.DoNothing;
                else if (Method == Models.DealWithLocalChanges.Discard)
                    Method = Models.DealWithLocalChanges.StashAndReapply;
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (Method == Models.DealWithLocalChanges.DoNothing)
                    Method = Models.DealWithLocalChanges.StashAndReapply;
                else if (Method == Models.DealWithLocalChanges.StashAndReapply)
                    Method = Models.DealWithLocalChanges.Discard;
                e.Handled = true;
            }
        }

        private void OnRadioButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { Tag: Models.DealWithLocalChanges way })
            {
                Method = way;
                e.Handled = true;
            }
        }

        private void UpdateRadioButtons()
        {
            switch (_method)
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

        private Models.DealWithLocalChanges _method = Models.DealWithLocalChanges.DoNothing;
    }
}
