using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SourceGit.Models;

namespace SourceGit.Views
{
    public partial class Reset : UserControl
    {
        public Reset()
        {
            InitializeComponent();
        }

        private void InputElement_OnKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key.ToString().ToLower();
            foreach (var item in ResetMode.ItemsSource)
            {
                if (item.GetType() == typeof(ResetMode))
                {
                    var resetMode = (ResetMode)item;
                    if (resetMode.Key.ToString().ToLower() == key)
                    {
                        ResetMode.SelectedValue = resetMode;
                        return;
                    }
                        
                }
            }
        }

        private void Control_OnLoaded(object sender, RoutedEventArgs e)
        {
            ResetMode.Focus();
        }
    }
}
