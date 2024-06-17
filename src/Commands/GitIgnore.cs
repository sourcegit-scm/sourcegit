using System.IO;

namespace SourceGit.Commands
{
    public static class GitIgnore
    {
        public static void Add(string repo, string pattern)
        {
            var file = Path.Combine(repo, ".gitignore");
            if (!File.Exists(file))
                File.WriteAllLines(file, [ pattern ]);
            else
                File.AppendAllLines(file, [ pattern ]);
        }
    }
}
