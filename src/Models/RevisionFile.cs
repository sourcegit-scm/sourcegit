using System.Globalization;
using System.IO;
using Avalonia.Media.Imaging;

namespace DevBoard.Models
{
    public class RevisionBinaryFile
    {
        public string Repository { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public long Size { get; set; } = 0;

        public RevisionBinaryFile(string repo, string file, string revision, long size)
        {
            Repository = repo;
            File = file;
            Revision = revision;
            Size = size;
        }
    }

    public class RevisionImageFile
    {
        public Bitmap Image { get; }
        public long FileSize { get; }
        public string ImageType { get; }
        public string ImageSize => Image != null ? $"{Image.PixelSize.Width} x {Image.PixelSize.Height}" : "0 x 0";

        public RevisionImageFile(string file, Bitmap img, long size)
        {
            Image = img;
            FileSize = size;
            ImageType = Path.GetExtension(file)!.Substring(1).ToUpper(CultureInfo.CurrentCulture);
        }
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
        public int UncommittedChanges { get; set; } = 0;
    }
}
