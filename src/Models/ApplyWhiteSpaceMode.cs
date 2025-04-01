namespace SourceGit.Models
{
    public class ApplyWhiteSpaceMode
    {
        public static readonly ApplyWhiteSpaceMode[] Supported =
        [
            new ApplyWhiteSpaceMode("No Warn", "Turns off the trailing whitespace warning", "nowarn"),
            new ApplyWhiteSpaceMode("Warn", "Outputs warnings for a few such errors, but applies", "warn"),
            new ApplyWhiteSpaceMode("Error", "Raise errors and refuses to apply the patch", "error"),
            new ApplyWhiteSpaceMode("Error All", "Similar to 'error', but shows more", "error-all"),
        ];

        public string Name { get; set; }
        public string Desc { get; set; }
        public string Arg { get; set; }

        public ApplyWhiteSpaceMode(string n, string d, string a)
        {
            Name = n;
            Desc = d;
            Arg = a;
        }
    }
}
