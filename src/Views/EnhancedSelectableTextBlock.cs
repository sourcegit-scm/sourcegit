using System;

using Avalonia;
using Avalonia.Controls;

namespace SourceGit.Views
{
    public class EnhancedSelectableTextBlock : SelectableTextBlock
    {
        protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty)
                UpdateLayout();
        }
    }
}
