using System.Text.RegularExpressions;

namespace SourceGit.Models
{
    public partial class LFSObject
    {
        [GeneratedRegex(@"^version https://git-lfs.github.com/spec/v\d+\r?\noid sha256:([0-9a-f]+)\r?\nsize (\d+)[\r\n]*$")]
        private static partial Regex REG_FORMAT();

        public string Oid { get; set; } = string.Empty;
        public long Size { get; set; }

        public static LFSObject Parse(string content)
        {
            var match = REG_FORMAT().Match(content);
            if (match.Success)
                return new() { Oid = match.Groups[1].Value, Size = long.Parse(match.Groups[2].Value) };

            return null;
        }
    }
}
