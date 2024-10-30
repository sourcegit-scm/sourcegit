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
                switch (VerifyResult)
                {
                    case 'G':
                    case 'U':
                        return Brushes.Green;
                    case 'X':
                    case 'Y':
                    case 'R':
                        return Brushes.DarkOrange;
                    case 'B':
                    case 'E':
                        return Brushes.Red;
                    default:
                        return Brushes.Transparent;
                }
            }
        }

        public string ToolTip
        {
            get
            {
                switch (VerifyResult)
                {
                    case 'G':
                        return "Good signature.";
                    case 'U':
                        return "Good signature with unknown validity.";
                    case 'X':
                        return "Good signature but has expired.";
                    case 'Y':
                        return "Good signature made by expired key.";
                    case 'R':
                        return "Good signature made by a revoked key.";
                    case 'B':
                        return "Bad signature.";
                    case 'E':
                        return "Signature cannot be checked.";
                    default:
                        return "No signature.";
                }
            }
        }
    }
}
