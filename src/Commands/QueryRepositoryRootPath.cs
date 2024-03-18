namespace SourceGit.Commands
{
    public class QueryRepositoryRootPath : Command
    {
        public QueryRepositoryRootPath(string path)
        {
            WorkingDirectory = path;
            Args = "rev-parse --show-toplevel";
            RaiseError = false;
        }

        public string Result()
        {
            var rs = ReadToEnd().StdOut;
            if (string.IsNullOrEmpty(rs)) return null;
            return rs.Trim();
        }
    }
}