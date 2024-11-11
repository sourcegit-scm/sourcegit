namespace SourceGit.Commands
{
    public class QueryCommitChildren : Command
    {
        public QueryCommitChildren(string repo, string sha)
        {
            WorkingDirectory = repo;
            Context = repo;
            _sha = sha;
            Args = $"rev-list --children --all {sha}^..";
        }

        public string[] Result()
        {
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return [];

            int start = rs.StdOut.IndexOf($"\n{_sha}");
            if (start != -1)
            {
                int end = rs.StdOut.IndexOf('\n', start + 1);
                if (end == -1)
                    end = rs.StdOut.Length;
                start = rs.StdOut.IndexOf(' ', start);
                if (start != -1 && start < end)
                    return rs.StdOut.Substring(start + 1, end - start - 1).Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            }
            return [];
        }

        private string _sha;
    }
}
