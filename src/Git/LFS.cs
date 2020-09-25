namespace SourceGit.Git {
    
    /// <summary>
    ///    Object filtered by LFS 
    /// </summary>
    public class LFSObject {

        /// <summary>
        ///     Object id
        /// </summary>
        public string OID { get; set; }

        /// <summary>
        ///     Object size.
        /// </summary>
        public long Size { get; set; }
    }
}
