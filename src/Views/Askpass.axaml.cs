using System;
using System.Security.Cryptography;
using System.Text;

using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Askpass : ChromelessWindow
    {
        public Askpass()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            TxtPassphrase.Focus(NavigationMethod.Directional);
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Console.Out.WriteLine("No passphrase entered.");
            App.Quit(-1);
        }

        private void EnterPassword(object _1, RoutedEventArgs _2)
        {
            var passphrase = TxtPassphrase.Text ?? string.Empty;
            byte[] passBytes = Encoding.UTF8.GetBytes(passphrase);
            try
            {
                var outStream = Console.OpenStandardOutput();
                outStream.Write(passBytes, 0, passBytes.Length);
                outStream.WriteByte((byte)'\n');
                outStream.Flush();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passBytes);
                TxtPassphrase.Text = string.Empty;
            }
            App.Quit(0);
        }
    }
}
