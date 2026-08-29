namespace DevBoard.Native
{
    public static partial class OS
    {
        public static string FindTerminal(Models.ShellOrTerminal shell)
        {
            return shell != null ? _backend.FindTerminal(shell) : string.Empty;
        }
    }
}
