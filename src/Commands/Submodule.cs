using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Submodule : Command
    {
        public Submodule(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public bool Add(string url, string relativePath, bool recursive)
        {
            Args = $"-c protocol.file.allow=always submodule add \"{url}\" \"{relativePath}\"";
            if (!Exec())
                return false;

            if (recursive)
            {
                Args = $"submodule update --init --recursive -- \"{relativePath}\"";
                return Exec();
            }
            else
            {
                Args = $"submodule update --init -- \"{relativePath}\"";
                return true;
            }
        }

        public bool Update(List<string> modules, bool init, bool recursive, bool useRemote = false)
        {
            var builder = new StringBuilder();
            builder.Append("submodule update");

            if (init)
                builder.Append(" --init");
            if (recursive)
                builder.Append(" --recursive");
            if (useRemote)
                builder.Append(" --remote");
            if (modules.Count > 0)
            {
                builder.Append(" --");
                foreach (var module in modules)
                    builder.Append($" \"{module}\"");
            }

            Args = builder.ToString();
            return Exec();
        }

        public bool Deinit(string module, bool force)
        {
            Args = force ? $"submodule deinit -f -- \"{module}\"" : $"submodule deinit -- \"{module}\"";
            return Exec();
        }

        public bool Delete(string module)
        {
            Args = $"rm -rf \"{module}\"";
            return Exec();
        }

        public async Task<bool> AddAsync(string url, string relativePath, bool recursive)
        {
            Args = $"-c protocol.file.allow=always submodule add \"{url}\" \"{relativePath}\"";
            if (!await ExecAsync())
                return false;

            if (recursive)
            {
                Args = $"submodule update --init --recursive -- \"{relativePath}\"";
                return await ExecAsync();
            }
            else
            {
                Args = $"submodule update --init -- \"{relativePath}\"";
                return true;
            }
        }

        public async Task<bool> UpdateAsync(List<string> modules, bool init, bool recursive, bool useRemote = false)
        {
            var builder = new StringBuilder();
            builder.Append("submodule update");

            if (init)
                builder.Append(" --init");
            if (recursive)
                builder.Append(" --recursive");
            if (useRemote)
                builder.Append(" --remote");
            if (modules.Count > 0)
            {
                builder.Append(" --");
                foreach (var module in modules)
                    builder.Append($" \"{module}\"");
            }

            Args = builder.ToString();
            return await ExecAsync();
        }

        public async Task<bool> DeinitAsync(string module, bool force)
        {
            Args = force ? $"submodule deinit -f -- \"{module}\"" : $"submodule deinit -- \"{module}\"";
            return await ExecAsync();
        }

        public async Task<bool> DeleteAsync(string module)
        {
            Args = $"rm -rf \"{module}\"";
            return await ExecAsync();
        }
    }
}
