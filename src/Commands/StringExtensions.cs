using System;

namespace SourceGit.Commands
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
}
