using System;
using System.ClientModel;

namespace SourceGit.AI
{
    public class Service
    {
        public string Name { get; set; }
        public string Server { get; set; }
        public string Model { get; set; }
        public string ApiKey { get; set; }
        public bool ReadApiKeyFromEnv { get; set; }
        public string AdditionalPrompt { get; set; }
        public ApiKeyCredential Credential => new ApiKeyCredential(ReadApiKeyFromEnv ? Environment.GetEnvironmentVariable(ApiKey) : ApiKey);
    }
}
