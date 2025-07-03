using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class Tag
    {
        public static async Task<bool> AddAsync(string repo, string name, string basedOn, Models.ICommandLog log)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"tag --no-sign {name} {basedOn}";
            cmd.Log = log;
            return await cmd.ExecAsync().ConfigureAwait(false);
        }

        public static async Task<bool> AddAsync(string repo, string name, string basedOn, string message, bool sign, Models.ICommandLog log)
        {
            var param = sign ? "--sign -a" : "--no-sign -a";
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"tag {param} {name} {basedOn} ";
            cmd.Log = log;

            if (!string.IsNullOrEmpty(message))
            {
                string tmp = Path.GetTempFileName();
                await File.WriteAllTextAsync(tmp, message);
                cmd.Args += $"-F \"{tmp}\"";

                var succ = await cmd.ExecAsync().ConfigureAwait(false);
                File.Delete(tmp);
                return succ;
            }

            cmd.Args += $"-m {name}";
            return await cmd.ExecAsync().ConfigureAwait(false);
        }

        public static async Task<bool> DeleteAsync(string repo, string name, Models.ICommandLog log)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"tag --delete {name}";
            cmd.Log = log;
            return await cmd.ExecAsync().ConfigureAwait(false);
        }
    }
}
