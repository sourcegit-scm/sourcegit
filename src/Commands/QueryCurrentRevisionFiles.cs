namespace SourceGit.Commands
{
    public class QueryCurrentRevisionFiles : Command
    {
        public QueryCurrentRevisionFiles(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = ["ls-tree", "-r", "--name-only", "HEAD"];
        }

        public string[] Result()
        {
            var rs = ReadToEnd();
            if (rs.IsSuccess)
                return rs.StdOut.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);

            return [];
        }
    }
}
