using System;

using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Askpass : ChromelessWindow
    {
        public string KeyName
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

        public Askpass(string keyname)
        {
            KeyName = keyname;
            DataContext = this;
            InitializeComponent();
        }

        private void BeginMoveWindow(object sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Console.Out.WriteLine("No passphrase entered.");
            Environment.Exit(-1);
        }

        private void EnterPassword(object sender, RoutedEventArgs e)
        {
            Console.Out.Write($"{Passphrase}\n");
            Environment.Exit(0);
        }
    }
}
