using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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

        [JsonIgnore]
        public List<string> AvailableModels
        {
            get;
            private set;
        } = [];

        public string Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        public bool AutoFetchAvailableModels
        {
            get => _autoFetchAvailableModels;
            set => SetProperty(ref _autoFetchAvailableModels, value);
        }

        public string ReasoningEffortLevel
        {
            get => _reasoningEffortLevel;
            set => SetProperty(ref _reasoningEffortLevel, value);
        }

        public string AdditionalPrompt
        {
            get;
            set;
        } = string.Empty;

        public string ExtraHeaders
        {
            get;
            set;
        } = string.Empty;

        public void FetchAvailableModels()
        {
            if (!_autoFetchAvailableModels)
            {
                if (!string.IsNullOrEmpty(Model))
                    AvailableModels = [Model];
                return;
            }

            var allModels = GetOpenAIClient().GetOpenAIModelClient().GetModels();
            AvailableModels = new List<string>();
            foreach (var model in allModels.Value)
                AvailableModels.Add(model.Id);

            if (AvailableModels.Count > 0 && (string.IsNullOrEmpty(Model) || !AvailableModels.Contains(Model)))
                Model = AvailableModels[0];
        }

        public ChatClient GetChatClient()
        {
            return !string.IsNullOrEmpty(Model) ? GetOpenAIClient().GetChatClient(Model) : null;
        }

        private OpenAIClient GetOpenAIClient()
        {
            var credential = new ApiKeyCredential(ReadApiKeyFromEnv ? Environment.GetEnvironmentVariable(ApiKey) : ApiKey);

            if (Server.Contains("openai.azure.com/", StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(ExtraHeaders))
                    return new AzureOpenAIClient(new Uri(Server), credential);

                var azureOptions = new AzureOpenAIClientOptions();
                azureOptions.AddPolicy(new ExtraHeadersPolicy(ExtraHeaders), PipelinePosition.PerCall);
                return new AzureOpenAIClient(new Uri(Server), credential, azureOptions);
            }

            var options = new OpenAIClientOptions() { Endpoint = new Uri(Server) };
            if (!string.IsNullOrEmpty(ExtraHeaders))
                options.AddPolicy(new ExtraHeadersPolicy(ExtraHeaders), PipelinePosition.PerCall);
            return new OpenAIClient(credential, options);
        }

        private string _name = string.Empty;
        private string _model = string.Empty;
        private string _reasoningEffortLevel = Options.IgnoredReasoningEffortLevel;
        private bool _autoFetchAvailableModels = true;
    }
}
