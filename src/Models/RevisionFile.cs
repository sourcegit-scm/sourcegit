using Avalonia.Media.Imaging;

namespace SourceGit.Models
{
    public class RevisionBinaryFile
    {
        public long Size { get; set; } = 0;
    }

    public class RevisionImageFile
    {
        public Bitmap Image { get; set; } = null;
        public long FileSize { get; set; } = 0;
        public string ImageType { get; set; } = string.Empty;
        public string ImageSize => Image != null ? $"{Image.PixelSize.Width} x {Image.PixelSize.Height}" : "0 x 0";
    }

    public class RevisionTextFile
    {
        public string FileName { get; set; }
        public string Content { get; set; }
    }

    public class RevisionLFSObject
    {
        public LFSObject Object { get; set; }
    }

    public class RevisionSubmodule
    {
        public Commit Commit { get; set; } = null;
        public CommitFullMessage FullMessage { get; set; } = null;
    }
}
