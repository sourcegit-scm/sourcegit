using System.IO;

namespace SourceGit.Commands
{
    public static class Tag
    {
        public static bool Add(string repo, string name, string basedOn, Models.ICommandLog log)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"tag --no-sign {name} {basedOn}";
            cmd.Log = log;
            return cmd.Exec();
        }

        public static bool Add(string repo, string name, string basedOn, string message, bool sign, Models.ICommandLog log)
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
                File.WriteAllText(tmp, message);
                cmd.Args += $"-F \"{tmp}\"";
            }
            else
            {
                cmd.Args += $"-m {name}";
            }

            return cmd.Exec();
        }

        public static bool Delete(string repo, string name, Models.ICommandLog log)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"tag --delete {name}";
            cmd.Log = log;
            return cmd.Exec();
        }
    }
}
