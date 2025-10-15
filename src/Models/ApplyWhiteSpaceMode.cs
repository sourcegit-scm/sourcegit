namespace SourceGit.Models
{
    public class ApplyWhiteSpaceMode(string n, string d, string a)
    {
        public static readonly ApplyWhiteSpaceMode[] Supported =
        [
            new ApplyWhiteSpaceMode("No Warn", "Turns off the trailing whitespace warning", "nowarn"),
            new ApplyWhiteSpaceMode("Warn", "Outputs warnings for a few such errors, but applies", "warn"),
            new ApplyWhiteSpaceMode("Error", "Raise errors and refuses to apply the patch", "error"),
            new ApplyWhiteSpaceMode("Error All", "Similar to 'error', but shows more", "error-all"),
        ];

        public string Name { get; set; } = n;
        public string Desc { get; set; } = d;
        public string Arg { get; set; } = a;
    }
}
