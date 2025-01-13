using System.IO;

namespace SourceGit.Commands
{
    public class IsBareRepository : Command
    {
        public IsBareRepository(string path)
        {
            WorkingDirectory = path;
            Args = "rev-parse --is-bare-repository";
        }

        public bool Result()
        {
            if (!Directory.Exists(Path.Combine(WorkingDirectory, "refs")) ||
                !Directory.Exists(Path.Combine(WorkingDirectory, "objects")) ||
                !File.Exists(Path.Combine(WorkingDirectory, "HEAD")))
                return false;

            var rs = ReadToEnd();
            return rs.IsSuccess && rs.StdOut.Trim() == "true";
        }
    }
}
