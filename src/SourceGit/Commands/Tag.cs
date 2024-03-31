using System.Collections.Generic;
using System.IO;

namespace SourceGit.Commands
{
    public static class Tag
    {
        public static bool Add(string repo, string name, string basedOn, string message)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"tag -a {name} {basedOn} ";

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
                {
                    new Push(repo, r.Name, name, true).Exec();
                }
            }

            return true;
        }
    }
}
