using System;
using System.Threading;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public class CommandLogTime : TextBlock
    {
        protected override Type StyleKeyOverride => typeof(TextBlock);

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            StopTimer();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            StopTimer();

            if (DataContext is ViewModels.CommandLog log)
                SetupCommandLog(log);
            else
                Text = string.Empty;
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
            if (_refreshTimer is not null)
            {
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
        }

        private static string GetDisplayText(ViewModels.CommandLog log)
        {
            var endTime = log.IsComplete ? log.EndTime : DateTime.Now;
            var duration = endTime - log.StartTime;

            if (duration.TotalMinutes >= 1)
                return $"{duration.TotalMinutes:G3} min";

            if (duration.TotalSeconds >= 1)
                return $"{duration.TotalSeconds:G3} s";

            return $"{duration.TotalMilliseconds:G3} ms";
        }

        private Timer _refreshTimer = null;
    }
}
