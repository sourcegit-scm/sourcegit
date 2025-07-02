using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryRevisionByRefName : Command
    {
        public QueryRevisionByRefName(string repo, string refname)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"rev-parse {refname}";
        }

        public string Result()
        {
            var rs = ReadToEnd();
            if (rs.IsSuccess && !string.IsNullOrEmpty(rs.StdOut))
                return rs.StdOut.Trim();

            return null;
        }

        public async Task<string> ResultAsync()
        {
            var rs = await ReadToEndAsync();
            if (rs.IsSuccess && !string.IsNullOrEmpty(rs.StdOut))
                return rs.StdOut.Trim();

            return null;
        }
    }
}
