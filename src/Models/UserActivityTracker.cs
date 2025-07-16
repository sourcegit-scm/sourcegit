using System;
using System.Threading;

namespace SourceGit.Models
{
    public class UserActivityTracker
    {
        private static readonly Lazy<UserActivityTracker> s_instance = new(() => new UserActivityTracker());
        private bool _isWindowActive = false;
        private DateTime _lastActivity = DateTime.MinValue;
        private readonly Lock _lockObject = new();
        private readonly int _minIdleSecondsBeforeAutoFetch = 15;

        private void OnUserActivity(object sender, EventArgs e) => UpdateLastActivity();

        private void OnWindowActivated(object sender, EventArgs e)
        {
            lock (_lockObject)
            {
                _isWindowActive = true;
                _lastActivity = DateTime.Now;
            }
        }

        private void OnWindowDeactivated(object sender, EventArgs e)
        {
            lock (_lockObject)
                _isWindowActive = false;
        }

        public void Initialize()
        {
            lock (_lockObject)
            {
                _lastActivity = DateTime.Now;
                _isWindowActive = true;
            }

            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                if (desktop.MainWindow != null)
                {
                    desktop.MainWindow.Activated += OnWindowActivated;
                    desktop.MainWindow.Deactivated += OnWindowDeactivated;
                    desktop.MainWindow.KeyDown += OnUserActivity;
                    desktop.MainWindow.PointerPressed += OnUserActivity;
                    desktop.MainWindow.PointerMoved += OnUserActivity;
                    desktop.MainWindow.PointerWheelChanged += OnUserActivity;
                }
        }

        public bool ShouldPerformAutoFetch(DateTime lastFetchTime, int intervalMinutes)
        {
            var now = DateTime.Now;

            if (now < lastFetchTime.AddMinutes(intervalMinutes))
                return false;

            lock (_lockObject)
            {
                if (!_isWindowActive)
                    return true;

                var timeSinceLastActivity = now - _lastActivity;

                if (timeSinceLastActivity.TotalSeconds >= _minIdleSecondsBeforeAutoFetch)
                    return true;

                return false;
            }
        }

        public void UpdateLastActivity()
        {
            lock (_lockObject)
                _lastActivity = DateTime.Now;
        }

        public static UserActivityTracker Instance => s_instance.Value;
    }
}
