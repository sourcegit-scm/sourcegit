using System.Collections.Generic;
using System.Text;

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

        public bool Delete(string relativePath)
        {
            Args = $"submodule deinit -f \"{relativePath}\"";
            if (!Exec())
                return false;

            Args = $"rm -rf \"{relativePath}\"";
            return Exec();
        }
    }
}
