namespace SourceGit.Commands
{
    public class Remove : Command
    {
        public Remove(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public Remove File(string file)
        {
            Args = $"rm --force --ignore-unmatch -- {file.Quoted()}";
            return this;
        }

        public Remove Files(string pathspecFromFile)
        {
            Args = $"rm --force --ignore-unmatch --pathspec-from-file={pathspecFromFile.Quoted()}";
            return this;
        }
    }
}
