using Avalonia.Media;

namespace SourceGit.Models
{
    public class CommitSignInfo
    {
        public char VerifyResult { get; init; } = 'N';
        public string Signer { get; init; } = string.Empty;
        public string Key { get; init; } = string.Empty;

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
                        return $"Good signature.\n\nSigner: {Signer}\n\nKey: {Key}";
                    case 'B':
                        return $"Bad signature.\n\nSigner: {Signer}\n\nKey: {Key}";
                    case 'U':
                        return $"Good signature with unknown validity.\n\nSigner: {Signer}\n\nKey: {Key}";
                    case 'X':
                        return $"Good signature but has expired.\n\nSigner: {Signer}\n\nKey: {Key}";
                    case 'Y':
                        return $"Good signature made by expired key.\n\nSigner: {Signer}\n\nKey: {Key}";
                    case 'R':
                        return $"Good signature made by a revoked key.\n\nSigner: {Signer}\n\nKey: {Key}";
                    case 'E':
                        return $"Signature cannot be checked.\n\nSigner: {Signer}\n\nKey: {Key}";
                    default:
                        return "No signature.";
                }
            }
        }
    }
}
