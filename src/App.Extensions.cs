using System;
using System.IO;

namespace SourceGit
{
    public static class StringExtensions
    {
        public static string Quoted(this string value)
        {
            return $"\"{Escaped(value)}\"";
        }

        public static string Escaped(this string value)
        {
            return value.Replace("\"", "\\\"", StringComparison.Ordinal);
        }
    }

    public static class CommandExtensions
    {
        public static T Use<T>(this T cmd, Models.ICommandLog log) where T : Commands.Command
        {
            cmd.Log = log;
            return cmd;
        }
    }

    public static class DirectoryInfoExtension
    {
        public static void WalkFiles(this DirectoryInfo dir, Action<string> onFile, int maxDepth = 4)
        {
            try
            {
                var options = new EnumerationOptions()
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                };

                foreach (var file in dir.GetFiles("*", options))
                    onFile(file.FullName);

                if (maxDepth > 0)
                {
                    foreach (var subDir in dir.GetDirectories("*", options))
                    {
                        if (subDir.Name.StartsWith(".", StringComparison.Ordinal) ||
                            subDir.Name.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
                            continue;

                        WalkFiles(subDir, onFile, maxDepth - 1);
                    }
                }
            }
            catch
            {
                // Ignore exceptions.
            }
        }

        public static string GetRelativePath(this DirectoryInfo dir, string fullpath)
        {
            return fullpath.Substring(dir.FullName.Length).TrimStart(Path.DirectorySeparatorChar);
        }
    }
}
