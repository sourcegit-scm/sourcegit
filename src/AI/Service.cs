using System;
using System.ClientModel;
using Azure.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;

namespace SourceGit.AI
{
    public class Service
    {
        public string Name { get; set; } = string.Empty;
        public string Server { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public bool ReadApiKeyFromEnv { get; set; } = false;
        public string AdditionalPrompt { get; set; } = string.Empty;

        public ChatClient GetChatClient()
        {
            var credential = new ApiKeyCredential(ReadApiKeyFromEnv ? Environment.GetEnvironmentVariable(ApiKey) : ApiKey);
            var client = Server.Contains("openai.azure.com/", StringComparison.Ordinal)
                ? new AzureOpenAIClient(new Uri(Server), credential)
                : new OpenAIClient(credential, new() { Endpoint = new Uri(Server) });

            return client.GetChatClient(Model);
        }
    }
}
