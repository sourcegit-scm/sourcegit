using Avalonia;
        public Vector SyncScrollOffset { get; set; } = Vector.Zero;
        public long OldFileSize { get; set; } = 0;
        public long NewFileSize { get; set; } = 0;

        public string OldImageSize => Old != null ? $"{Old.PixelSize.Width} x {Old.PixelSize.Height}" : "0 x 0";
        public string NewImageSize => New != null ? $"{New.PixelSize.Width} x {New.PixelSize.Height}" : "0 x 0";
    public class SubmoduleRevision
    {
        public Commit Commit { get; set; } = null;
        public string FullMessage { get; set; } = string.Empty;
    }

        public SubmoduleRevision Old { get; set; } = null;
        public SubmoduleRevision New { get; set; } = null;