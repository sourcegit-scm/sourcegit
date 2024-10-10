namespace SourceGit.Commands
{
    public class Remote : Command
    {
        public Remote(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public bool Add(string name, string url)
        {
            Args = $"remote add {name} {url}";
            return Exec();
        }

        public bool Delete(string name)
        {
            Args = $"remote remove {name}";
            return Exec();
        }

        public bool Rename(string name, string to)
        {
            Args = $"remote rename {name} {to}";
            return Exec();
        }

        public bool Prune(string name)
        {
            Args = $"remote prune {name}";
            return Exec();
        }

        public bool SetURL(string name, string url)
        {
            Args = $"remote set-url {name} {url}";
            var first = Exec();
            Args = $"remote set-url {name} {url} --push";
            return Exec() && first;
        }

        public bool SetPushURL(string name, string oldUrl, string newUrl)
        {
            if (oldUrl == newUrl)
                return true;

            if (string.IsNullOrEmpty(newUrl) && !string.IsNullOrEmpty(oldUrl))
            {
                Args = $"remote set-url --push --delete {name} {oldUrl}";
            }
            else
            {
                Args = $"remote set-url --push {name} {newUrl}";
            }
            return Exec();
        }
    }
}
