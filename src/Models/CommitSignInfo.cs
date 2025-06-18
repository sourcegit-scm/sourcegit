using Avalonia.Media;

namespace SourceGit.Models
{
    public class CommitSignInfo
    {
        public char VerifyResult { get; init; } = 'N';
        public string Signer { get; init; } = string.Empty;
        public string Key { get; init; } = string.Empty;
        public bool HasSigner => !string.IsNullOrEmpty(Signer);

        public IBrush Brush
        {
            get
            {
                return VerifyResult switch
                {
                    'G' or 'U' => Brushes.Green,
                    'X' or 'Y' or 'R' => Brushes.DarkOrange,
                    'B' or 'E' => Brushes.Red,
                    _ => Brushes.Transparent,
                };
            }
        }

        public string ToolTip
        {
            get
            {
                return VerifyResult switch
                {
                    'G' => "Good signature.",
                    'U' => "Good signature with unknown validity.",
                    'X' => "Good signature but has expired.",
                    'Y' => "Good signature made by expired key.",
                    'R' => "Good signature made by a revoked key.",
                    'B' => "Bad signature.",
                    'E' => "Signature cannot be checked.",
                    _ => "No signature.",
                };
            }
        }
    }
}
