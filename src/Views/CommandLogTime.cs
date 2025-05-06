using System;
using System.Threading;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public class CommandLogTime : TextBlock
    {
        public static readonly StyledProperty<ViewModels.CommandLog> LogProperty =
            AvaloniaProperty.Register<CommandLogTime, ViewModels.CommandLog>(nameof(Log), null);

        public ViewModels.CommandLog Log
        {
            get => GetValue(LogProperty);
            set => SetValue(LogProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextBlock);

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            StopTimer();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == LogProperty)
            {
                StopTimer();

                if (change.NewValue is ViewModels.CommandLog log)
                    SetupCommandLog(log);
                else
                    Text = string.Empty;
            }
        }

        private void SetupCommandLog(ViewModels.CommandLog log)
        {
            Text = GetDisplayText(log);
            if (log.IsComplete)
                return;

            _refreshTimer = new Timer(_ =>
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    Text = GetDisplayText(log);
                    if (log.IsComplete)
                        StopTimer();
                });
            }, null, 0, 100);
        }

        private void StopTimer()
        {
            if (_refreshTimer is { })
            {
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
        }

        private string GetDisplayText(ViewModels.CommandLog log)
        {
            var endTime = log.IsComplete ? log.EndTime : DateTime.Now;
            var duration = (endTime - log.StartTime).ToString(@"hh\:mm\:ss\.fff");
            return $"{log.StartTime:T} ({duration})";
        }

        private Timer _refreshTimer = null;
    }
}
