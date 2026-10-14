using System;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryRemoteHead : Command
    {
        public QueryRemoteHead(string repo, string remote)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"symbolic-ref --short refs/remotes/{remote}/HEAD";

            _remote = remote;
        }

        /// <summary>
        ///     Returns the name of the remote's default branch (without the remote prefix), or null if unknown.
        /// </summary>
        public async Task<string> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return null;

            var output = rs.StdOut.Trim();
            var prefix = $"{_remote}/";
            if (output.StartsWith(prefix, StringComparison.Ordinal))
                return output.Substring(prefix.Length);

            return null;
        }

        private readonly string _remote;
    }
}
