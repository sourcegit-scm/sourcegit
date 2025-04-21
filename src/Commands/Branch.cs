namespace SourceGit.Commands
{
    public static class Branch
    {
        public static string ShowCurrent(string repo)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"branch --show-current";
            return cmd.ReadToEnd().StdOut.Trim();
        }

        public static bool Create(string repo, string name, string basedOn, Models.ICommandLog log)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"branch {name} {basedOn}";
            cmd.Log = log;
            return cmd.Exec();
        }

        public static bool Rename(string repo, string name, string to, Models.ICommandLog log)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"branch -M {name} {to}";
            cmd.Log = log;
            return cmd.Exec();
        }

        public static bool SetUpstream(string repo, string name, string upstream, Models.ICommandLog log)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Log = log;

            if (string.IsNullOrEmpty(upstream))
                cmd.Args = $"branch {name} --unset-upstream";
            else
                cmd.Args = $"branch {name} -u {upstream}";

            return cmd.Exec();
        }

        public static bool DeleteLocal(string repo, string name, Models.ICommandLog log)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"branch -D {name}";
            cmd.Log = log;
            return cmd.Exec();
        }

        public static bool DeleteRemote(string repo, string remote, string name, Models.ICommandLog log)
        {
            bool exists = new Remote(repo).HasBranch(remote, name);
            if (exists)
                return new Push(repo, remote, $"refs/heads/{name}", true) { Log = log }.Exec();

            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"branch -D -r {remote}/{name}";
            cmd.Log = log;
            return cmd.Exec();
        }
    }
}
