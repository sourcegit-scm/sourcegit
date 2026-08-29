namespace SourceGit.Models
{
    public enum CloseAppDecision
    {
        No,
        Yes,
        AddToTray,
    }

    public enum LauncherCloseAction
    {
        Confirm,
        Exit,
        KeepOpen,
        HideToTray,
    }

    public sealed class LauncherCloseController
    {
        public LauncherCloseAction OnCloseRequested()
        {
            return _exitRequested ? LauncherCloseAction.Exit : LauncherCloseAction.Confirm;
        }

        public LauncherCloseAction Apply(CloseAppDecision decision)
        {
            switch (decision)
            {
                case CloseAppDecision.Yes:
                    _exitRequested = true;
                    return LauncherCloseAction.Exit;
                case CloseAppDecision.AddToTray:
                    return LauncherCloseAction.HideToTray;
                default:
                    return LauncherCloseAction.KeepOpen;
            }
        }

        private bool _exitRequested;
    }
}
