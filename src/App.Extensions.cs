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
            foreach (var file in dir.GetFiles())
                onFile(file.FullName);

            if (maxDepth > 0)
            {
                foreach (var subDir in dir.GetDirectories())
                    WalkFiles(subDir, onFile, maxDepth - 1);
            }
        }

        public static string GetRelativePath(this DirectoryInfo dir, string fullpath)
        {
            return fullpath.Substring(dir.FullName.Length).TrimStart(Path.DirectorySeparatorChar);
        }
    }
}
