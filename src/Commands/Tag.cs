using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Tag : Command
    {
        public Tag(string repo, string name)
        {
            WorkingDirectory = repo;
            Context = repo;
            _name = name;
        }

        public async Task<bool> AddAsync(string basedOn)
        {
            Args = $"tag --no-sign {_name} {basedOn}";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> AddAsync(string basedOn, string message, bool sign)
        {
            var builder = new StringBuilder();
            builder
                .Append("tag ")
                .Append(sign ? "--sign -a " : "--no-sign -a ")
                .Append(_name)
                .Append(' ')
                .Append(basedOn);

            if (!string.IsNullOrEmpty(message))
            {
                string tmp = Path.GetTempFileName();
                await File.WriteAllTextAsync(tmp, message);
                builder.Append(" -F ").Append(tmp.Quoted());

                Args = builder.ToString();
                var succ = await ExecAsync().ConfigureAwait(false);
                File.Delete(tmp);
                return succ;
            }

            builder.Append(" -m ");
            builder.Append(_name);

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> DeleteAsync()
        {
            Args = $"tag --delete {_name}";
            return await ExecAsync().ConfigureAwait(false);
        }

        private readonly string _name;
    }
}
