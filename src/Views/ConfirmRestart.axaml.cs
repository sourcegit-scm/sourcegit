using System;
using System.Diagnostics;

using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ConfirmRestart : ChromelessWindow
    {
        public ConfirmRestart()
        {
            InitializeComponent();
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Console.Out.WriteLine("No passphrase entered.");
            App.Quit(-1);
        }

        private void Restart(object _1, RoutedEventArgs _2)
        {
            var selfExecFile = Process.GetCurrentProcess().MainModule!.FileName;
            Process.Start(selfExecFile);
            App.Quit(-1);
        }
    }
}
