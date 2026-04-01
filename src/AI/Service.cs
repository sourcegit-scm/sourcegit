using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenAI;
using OpenAI.Chat;

namespace SourceGit.AI
{
    public class Service : ObservableObject
    {
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Server
        {
            get;
            set;
        } = string.Empty;

        public string ApiKey
        {
            get;
            set;
        } = string.Empty;

        public bool ReadApiKeyFromEnv
        {
            get;
            set;
        } = false;

        public string AdditionalPrompt
        {
            get;
            set;
        } = string.Empty;

        [JsonIgnore]
        public List<string> AvailableModels
        {
            get;
            private set;
        } = [];

        public string Model
        {
            get;
            set;
        } = string.Empty;

        public async Task<List<string>> FetchAvailableModelsAsync()
        {
            var credential = new ApiKeyCredential(ReadApiKeyFromEnv ? Environment.GetEnvironmentVariable(ApiKey) : ApiKey);
            var client = Server.Contains("openai.azure.com/", StringComparison.Ordinal)
                ? new AzureOpenAIClient(new Uri(Server), credential)
                : new OpenAIClient(credential, new() { Endpoint = new Uri(Server) });

            var allModels = client.GetOpenAIModelClient().GetModels();
            AvailableModels = new List<string>();
            foreach (var model in allModels.Value)
                AvailableModels.Add(model.Id);

            if (AvailableModels.Count > 0)
            {
                if (string.IsNullOrEmpty(Model) || !AvailableModels.Contains(Model))
                    Model = AvailableModels[0];
            }
            else
            {
                Model = null;
            }

            return AvailableModels;
        }

        public ChatClient GetChatClient()
        {
            if (string.IsNullOrEmpty(Model))
                return null;

            var credential = new ApiKeyCredential(ReadApiKeyFromEnv ? Environment.GetEnvironmentVariable(ApiKey) : ApiKey);
            var client = Server.Contains("openai.azure.com/", StringComparison.Ordinal)
                ? new AzureOpenAIClient(new Uri(Server), credential)
                : new OpenAIClient(credential, new() { Endpoint = new Uri(Server) });

            return client.GetChatClient(Model);
        }

        private string _name = string.Empty;
    }
}
