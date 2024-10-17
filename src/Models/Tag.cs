namespace SourceGit.Models
{
    public class Tag
    {
        public string Name { get; set; }
        public string SHA { get; set; }
        public string Message { get; set; }
        public bool IsFiltered { get; set; }
    }
}
