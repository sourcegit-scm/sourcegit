namespace SourceGit.Models
{
    public enum ObjectType
    {
        None,
        Blob,
        Tree,
        Tag,
        Commit,
    }

    public class Object
    {
        public string SHA { get; set; }
        public ObjectType Type { get; set; }
        public string Path { get; set; }
    }
}
