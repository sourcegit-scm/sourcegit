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
            Args = $"submodule add {url} \"{relativePath}\"";
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

        public bool Update(string module, bool init, bool recursive, bool useRemote)
        {
            Args = "submodule update";

            if (init)
                Args += " --init";
            if (recursive)
                Args += " --recursive";
            if (useRemote)
                Args += " --remote";
            if (!string.IsNullOrEmpty(module))
                Args += $" -- \"{module}\"";

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
