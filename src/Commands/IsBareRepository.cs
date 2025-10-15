using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class IsBareRepository : Command
    {
        public IsBareRepository(string path)
        {
            WorkingDirectory = path;
            Args = "rev-parse --is-bare-repository";
        }

        public bool GetResult()
        {
            if (!Directory.Exists(Path.Combine(WorkingDirectory, "refs")) ||
                !Directory.Exists(Path.Combine(WorkingDirectory, "objects")) ||
                !File.Exists(Path.Combine(WorkingDirectory, "HEAD")))
                return false;

            var rs = ReadToEnd();
            return rs.IsSuccess && rs.StdOut.Trim() == "true";
        }

        public async Task<bool> GetResultAsync()
        {
            if (!Directory.Exists(Path.Combine(WorkingDirectory, "refs")) ||
                !Directory.Exists(Path.Combine(WorkingDirectory, "objects")) ||
                !File.Exists(Path.Combine(WorkingDirectory, "HEAD")))
                return false;

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess && rs.StdOut.Trim() == "true";
        }
    }
}
