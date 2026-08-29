using System;
using System.Linq;

using Avalonia.Controls;
using Avalonia.VisualTree;

namespace DevBoard.Views
{
    public partial class Preferences
    {
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            if (_devSpacesTabAdded)
                return;

            var tabs = this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
            if (tabs == null)
                return;

            var item = new TabItem
            {
                Header = App.Text("DevSpaces"),
                Content = new DevSpacesPreferences(),
            };

            tabs.Items.Add(item);
            _devSpacesTabAdded = true;
        }

        private bool _devSpacesTabAdded = false;
    }
}
