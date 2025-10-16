namespace SourceGit.Models
{
    public class LFSLock
    {
        public string File
        {
            get => _file;
            set => _file = value.Trim();
        }

        public string User
        {
            get => _user;
            set => _user = value.Trim();
        }

        public long ID { get; set; } = 0;
        
        private string _file = string.Empty;
        private string _user = string.Empty;
    }
}
