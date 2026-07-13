using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class MergeBase : Command
    {
        [GeneratedRegex(@"^[0-9a-f]{8,64}$")]
        private static partial Regex REG_HEX();

        public MergeBase(string repo, string rev1, string rev2)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;
            Args = $"merge-base {rev1} {rev2}";
        }

        public async Task<string> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return string.Empty;

            var trimmed = rs.StdOut.Trim();
            if (REG_HEX().IsMatch(trimmed))
                return trimmed;

            return string.Empty;
        }
    }
}
