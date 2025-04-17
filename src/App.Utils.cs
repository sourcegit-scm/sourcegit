namespace SourceGit
{
    public static class CommandExtensions
    {
        public static T Use<T>(this T cmd, Models.ICommandLog log) where T : Commands.Command
        {
            cmd.Log = log;
            return cmd;
        }
    }
}
