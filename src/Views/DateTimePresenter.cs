using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace SourceGit.Views
{
    public class DateTimePresenter : TextBlock
    {
        public static readonly StyledProperty<bool> ShowDateOnlyProperty =
            AvaloniaProperty.Register<DateTimePresenter, bool>(nameof(ShowDateOnly), false);

        public bool ShowDateOnly
        {
            get => GetValue(ShowDateOnlyProperty);
            set => SetValue(ShowDateOnlyProperty, value);
        }

        public static readonly StyledProperty<bool> Use24HoursProperty =
            AvaloniaProperty.Register<DateTimePresenter, bool>(nameof(Use24Hours), true);

        public bool Use24Hours
        {
            get => GetValue(Use24HoursProperty);
            set => SetValue(Use24HoursProperty, value);
        }

        public static readonly StyledProperty<int> DateTimeFormatProperty =
            AvaloniaProperty.Register<DateTimePresenter, int>(nameof(DateTimeFormat));

        public int DateTimeFormat
        {
            get => GetValue(DateTimeFormatProperty);
            set => SetValue(DateTimeFormatProperty, value);
        }

        public static readonly StyledProperty<ulong> TimestampProperty =
            AvaloniaProperty.Register<DateTimePresenter, ulong>(nameof(Timestamp), 0);

        public ulong Timestamp
        {
            get => GetValue(TimestampProperty);
            set => SetValue(TimestampProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextBlock);

        public DateTimePresenter()
        {
            Bind(Use24HoursProperty, new Binding()
            {
                Mode = BindingMode.OneWay,
                Source = ViewModels.Preferences.Instance,
                Path = "Use24Hours"
            });

            Bind(DateTimeFormatProperty, new Binding()
            {
                Mode = BindingMode.OneWay,
                Source = ViewModels.Preferences.Instance,
                Path = "DateTimeFormat"
            });
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ShowDateOnlyProperty ||
                change.Property == Use24HoursProperty ||
                change.Property == DateTimeFormatProperty ||
                change.Property == TimestampProperty)
            {
                var active = Models.DateTimeFormat.Active;
                var format = ShowDateOnly ? active.DateOnly : active.DateTime;
                var text = DateTime.UnixEpoch.AddSeconds(Timestamp).ToLocalTime().ToString(format);
                SetCurrentValue(TextProperty, text);
            }
        }
    }
}
