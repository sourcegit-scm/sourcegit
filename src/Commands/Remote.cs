using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Remote : Command
    {
        public Remote(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public async Task<bool> AddAsync(string name, string url)
        {
            Args = $"remote add {name} {url}";
            return await ExecAsync();
        }

        public async Task<bool> DeleteAsync(string name)
        {
            Args = $"remote remove {name}";
            return await ExecAsync();
        }

        public async Task<bool> RenameAsync(string name, string to)
        {
            Args = $"remote rename {name} {to}";
            return await ExecAsync();
        }

        public async Task<bool> PruneAsync(string name)
        {
            Args = $"remote prune {name}";
            return await ExecAsync();
        }

        public async Task<bool> SetURLAsync(string name, string url, bool isDelete, bool isPush)
        {
            var builder = new StringBuilder();
            builder.Append("remote set-url ");
            if (isDelete)
                builder.Append("--delete ");
            if (isPush)
                builder.Append("--push ");
            builder.Append(name).Append(' ').Append(url);

            Args = builder.ToString();
            return await ExecAsync();
        }
    }
}
