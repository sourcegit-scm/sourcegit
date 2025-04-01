using System.Collections.Generic;
using System.IO;

namespace SourceGit.Commands
{
    public static class Tag
    {
        public static bool Add(string repo, string name, string basedOn)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"tag {name} {basedOn}";
            return cmd.Exec();
        }

        public static bool Add(string repo, string name, string basedOn, string message, bool sign)
        {
            var param = sign ? "--sign -a" : "--no-sign -a";
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"tag {param} {name} {basedOn} ";

            if (!string.IsNullOrEmpty(message))
            {
                string tmp = Path.GetTempFileName();
                File.WriteAllText(tmp, message);
                cmd.Args += $"-F \"{tmp}\"";
            }
            else
            {
                cmd.Args += $"-m {name}";
            }

            return cmd.Exec();
        }

        public static bool Delete(string repo, string name, List<Models.Remote> remotes)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"tag --delete {name}";
            if (!cmd.Exec())
                return false;

            if (remotes != null)
            {
                foreach (var r in remotes)
                    new Push(repo, r.Name, $"refs/tags/{name}", true).Exec();
            }

            return true;
        }
    }
}
