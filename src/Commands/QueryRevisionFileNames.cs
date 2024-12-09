namespace SourceGit.Commands
{
    public class QueryRevisionFileNames : Command
    {
        public QueryRevisionFileNames(string repo, string revision)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"ls-tree -r -z --name-only {revision}";
        }

        public string[] Result()
        {
            var rs = ReadToEnd();
            if (rs.IsSuccess)
                return rs.StdOut.Split('\0', System.StringSplitOptions.RemoveEmptyEntries);

            return [];
        }
    }
}
