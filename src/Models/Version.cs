using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SourceGit.Models
{
    public partial class Version
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("tag_name")]
        public string TagName { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        [GeneratedRegex(@"^v(\d+)\.(\d+)$")]
        private static partial Regex REG_VERSION_TAG();

        public bool IsNewVersion
        {
            get
            {
                var match = REG_VERSION_TAG().Match(TagName);
                if (!match.Success)
                    return false;

                var major = int.Parse(match.Groups[1].Value);
                var minor = int.Parse(match.Groups[2].Value);
                var ver = Assembly.GetExecutingAssembly().GetName().Version!;
                return ver.Major < major || (ver.Major == major && ver.Minor < minor);
            }
        }
    }

    public class AlreadyUpToDate { }
}
