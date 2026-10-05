using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace SourceGit.Views
{
    public class DateTimePresenter : TextBlock
    {
        public static readonly DirectProperty<DateTimePresenter, bool> ShowDateOnlyProperty =
            AvaloniaProperty.RegisterDirect<DateTimePresenter, bool>(
                nameof(ShowDateOnly),
                static o => o.ShowDateOnly,
                static (o, v) => o.ShowDateOnly = v);

        public bool ShowDateOnly
        {
            get => _showDateOnly;
            set => SetAndRaise(ShowDateOnlyProperty, ref _showDateOnly, value);
        }

        public static readonly DirectProperty<DateTimePresenter, bool> Use24HoursProperty =
            AvaloniaProperty.RegisterDirect<DateTimePresenter, bool>(
                nameof(Use24Hours),
                static o => o.Use24Hours,
                static (o, v) => o.Use24Hours = v);

        public bool Use24Hours
        {
            get => _use24Hours;
            set => SetAndRaise(Use24HoursProperty, ref _use24Hours, value);
        }

        public static readonly DirectProperty<DateTimePresenter, int> DateTimeFormatProperty =
            AvaloniaProperty.RegisterDirect<DateTimePresenter, int>(
                nameof(DateTimeFormat),
                static o => o.DateTimeFormat,
                static (o, v) => o.DateTimeFormat = v);

        public int DateTimeFormat
        {
            get => _dateTimeFormat;
            set => SetAndRaise(DateTimeFormatProperty, ref _dateTimeFormat, value);
        }

        public static readonly DirectProperty<DateTimePresenter, ulong> TimestampProperty =
            AvaloniaProperty.RegisterDirect<DateTimePresenter, ulong>(
                nameof(Timestamp),
                static o => o.Timestamp,
                static (o, v) => o.Timestamp = v);

        public ulong Timestamp
        {
            get => _timestamp;
            set => SetAndRaise(TimestampProperty, ref _timestamp, value);
        }

        protected override Type StyleKeyOverride => typeof(TextBlock);

        public DateTimePresenter()
        {
            Bind(Use24HoursProperty, CompiledBinding.Create((ViewModels.Preferences pref) => pref.Use24Hours,
                source: ViewModels.Preferences.Instance, mode: BindingMode.OneWay));

            Bind(DateTimeFormatProperty, CompiledBinding.Create((ViewModels.Preferences pref) => pref.DateTimeFormat,
                source: ViewModels.Preferences.Instance, mode: BindingMode.OneWay));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ShowDateOnlyProperty ||
                change.Property == Use24HoursProperty ||
                change.Property == DateTimeFormatProperty ||
                change.Property == TimestampProperty)
            {
                var text = Models.DateTimeFormat.Format(Timestamp, ShowDateOnly);
                SetCurrentValue(TextProperty, text);
            }
        }

        private bool _showDateOnly = false;
        private bool _use24Hours = true;
        private int _dateTimeFormat = 0;
        private ulong _timestamp = 0;
    }
}
