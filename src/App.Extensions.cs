using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia.Media;

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

        public static string FormatFontNames(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var parts = input.Split(',');
            var trimmed = new List<string>();

            foreach (var part in parts)
            {
                var t = part.Trim();
                if (string.IsNullOrEmpty(t))
                    continue;

                var sb = new StringBuilder();
                var prevChar = '\0';

                foreach (var c in t)
                {
                    if (c == ' ' && prevChar == ' ')
                        continue;
                    sb.Append(c);
                    prevChar = c;
                }

                var name = sb.ToString();
                try
                {
                    var fontFamily = FontFamily.Parse(name);
                    if (fontFamily.FamilyTypefaces.Count > 0)
                        trimmed.Add(name);
                }
                catch
                {
                    // Ignore exceptions.
                }
            }

            return trimmed.Count > 0 ? string.Join(',', trimmed) : string.Empty;
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
    }
}
