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
        public string SHA { get; set; }
    }
}
