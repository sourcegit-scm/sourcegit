namespace SourceGit.Models
{
    public class AIProvider
    {
        public string Name { get; set; }
        public string Server { get; set; }
        public string Model { get; set; }
        public string ApiKey { get; set; }
        public bool ReadApiKeyFromEnv { get; set; }
        public string AdditionalPrompt { get; set; }
    }
}
