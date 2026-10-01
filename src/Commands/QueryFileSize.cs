using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryFileSize : Command
    {
        public QueryFileSize(string repo, string file, string revision)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"cat-file -s {revision}:{file.Quoted()}";
        }

        public async Task<long> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (rs.IsSuccess && long.TryParse(rs.StdOut.Trim(), out var size))
                return size;

            return 0;
        }
    }
}
