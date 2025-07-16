namespace SourceGit.Commands
{
    public class Clean : Command
    {
        public Clean(string repo, Models.CleanMode mode)
        {
            WorkingDirectory = repo;
            Context = repo;

            Args = mode switch
            {
                Models.CleanMode.OnlyUntrackedFiles => "clean -qfd",
                Models.CleanMode.OnlyIgnoredFiles => "clean -qfdX",
                _ => "clean -qfdx",
            };
        }
    }
}
