namespace SourceGit.Models {
    /// <summary>
    ///     LFS对象变更
    /// </summary>
    public class LFSChange {
        public LFSObject Old;
        public LFSObject New;
        public bool IsValid => Old != null || New != null;
    }
}
