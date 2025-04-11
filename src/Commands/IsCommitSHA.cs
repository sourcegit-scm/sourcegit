namespace SourceGit.Commands
{
    public class IsCommitSHA : Command
    {
        public IsCommitSHA(string repo, string hash)
        {
            WorkingDirectory = repo;
            Args = $"cat-file -t {hash}";
        }

        public bool Result()
        {
            var rs = ReadToEnd();
            return rs.IsSuccess && rs.StdOut.Trim().Equals("commit");
        }
    }
}
