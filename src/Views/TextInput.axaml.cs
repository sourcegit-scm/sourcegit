using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class TextInput : ChromelessWindow
    {
        public string Value { get; private set; }

        public TextInput()
        {
            InitializeComponent();
        }

        public void SetData(string title, string defaultValue = "")
        {
            TxtTitle.Text = title;
            TxtInput.Text = defaultValue;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            TxtInput.Focus(NavigationMethod.Directional);
        }

        private void OnOk(object _1, RoutedEventArgs _2)
        {
            Value = TxtInput.Text;
            Close(true);
        }

        private void OnCancel(object _1, RoutedEventArgs _2)
        {
            Value = null;
            Close(false);
        }
    }
}
