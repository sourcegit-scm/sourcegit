namespace SourceGit.Commands
{
    public class AddSafeDirectory : Command
    {
        public AddSafeDirectory(string ctx, string value)
        {
            Context = ctx;
            Args = $"config --global --add safe.directory {value.Quoted()}";
        }
    }
}
