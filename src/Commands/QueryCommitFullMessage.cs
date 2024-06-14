namespace SourceGit.Commands
{
    public class QueryCommitFullMessage : Command
    {
        public QueryCommitFullMessage(string repo, string sha)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"show --no-show-signature --pretty=format:%B -s {sha}";
        }

        public string Result()
        {
            var rs = ReadToEnd();
            if (rs.IsSuccess)
                return rs.StdOut.TrimEnd();
            return string.Empty;
        }
    }
}
