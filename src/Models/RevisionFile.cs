namespace SourceGit.Models {
    public class RevisionBinaryFile {
    }

    public class RevisionTextFile {
        public string FileName { get; set; }
        public string Content { get; set; }
    }

    public class RevisionLFSObject {
        public LFSObject Object { get; set; }
    }

    public class RevisionSubmodule {
        public string SHA { get; set; }
    }
}
