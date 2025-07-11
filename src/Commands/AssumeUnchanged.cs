namespace SourceGit.Commands
{
    public class AssumeUnchanged : Command
    {
        public AssumeUnchanged(string repo, string file, bool bAdd)
        {
            var mode = bAdd ? "--assume-unchanged" : "--no-assume-unchanged";

            WorkingDirectory = repo;
            Context = repo;
            Args = $"update-index {mode} -- {file.Quoted()}";
        }
    }
}
