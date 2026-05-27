using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class MergeBase : Command
    {
        public MergeBase(string repo, string source, string dest)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;
            Args = $"merge-base {source} {dest}";
        }

        public async Task<string> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (rs.IsSuccess)
            {
                var trimmed = rs.StdOut.Trim();
                if (trimmed.Length == 40 || trimmed.Length == 64)
                    return trimmed;
            }

            return string.Empty;
        }
    }
}
