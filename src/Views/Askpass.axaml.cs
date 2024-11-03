using System;

using Avalonia;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Askpass : ChromelessWindow
    {
        public static readonly StyledProperty<bool> ShowPasswordProperty =
            AvaloniaProperty.Register<Askpass, bool>(nameof(ShowPassword));

        public bool ShowPassword
        {
            get => GetValue(ShowPasswordProperty);
            set => SetValue(ShowPasswordProperty, value);
        }

        public string Description
        {
            get;
            private set;
        } = string.Empty;

        public string Passphrase
        {
            get;
            set;
        } = string.Empty;

        public Askpass()
        {
            DataContext = this;
            InitializeComponent();
        }

        public Askpass(string description)
        {
            Description = description;
            DataContext = this;
            InitializeComponent();
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Console.Out.WriteLine("No passphrase entered.");
            App.Quit(-1);
        }

        private void EnterPassword(object _1, RoutedEventArgs _2)
        {
            Console.Out.Write($"{Passphrase}\n");
            App.Quit(0);
        }

        protected override void OnOpened(EventArgs e)
        {
            PassphraseTextBox.Focus();
            base.OnOpened(e);
        }
    }
}
