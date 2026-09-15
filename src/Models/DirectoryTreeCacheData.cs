using System.Collections.Generic;

namespace SourceGit.Models
{
    public class DirectoryTreeCacheData
    {
        public List<string> SourceDirectories { get; set; } = [];
        public int ScanDepth { get; set; } = 5;
        public List<string> ExpandedPaths { get; set; } = [];
        public List<DirectoryTreeCacheNode> Nodes { get; set; } = [];
    }

    public class DirectoryTreeCacheNode
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public bool IsRepository { get; set; }
        public List<DirectoryTreeCacheNode> Children { get; set; } = [];
    }
}
