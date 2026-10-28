using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using Azure.AI.OpenAI;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenAI;

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
            get => _server;
            set
            {
                if (SetProperty(ref _server, value))
                    OnPropertyChanged(nameof(HasValidApiKey));
            }
        }

        [JsonIgnore]
        public bool HasValidApiKey
        {
            get => ApiKeyManager.Get(Server) != null;
        }

        [JsonIgnore]
        public string CachedApiKey
        {
            get;
            set;
        } = string.Empty;

        [JsonIgnore]
        public List<string> CachedAvailableModels
        {
            get;
            set;
        } = [];

        public string Model
        {
            get => _model;
            set
            {
                if (SetProperty(ref _model, value))
                {
                    if (!_autoFetchAvailableModels)
                    {
                        if (string.IsNullOrEmpty(value))
                            CachedAvailableModels = [];
                        else
                            CachedAvailableModels = [_model];
                    }
                }
            }
        }

        public bool AutoFetchAvailableModels
        {
            get => _autoFetchAvailableModels;
            set
            {
                if (SetProperty(ref _autoFetchAvailableModels, value))
                {
                    if (!value && !string.IsNullOrEmpty(_model))
                        CachedAvailableModels = [_model];
                    else
                        CachedAvailableModels = [];
                }
            }
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

        public OpenAIClient GetOpenAIClient()
        {
            if (string.IsNullOrEmpty(CachedApiKey))
                CachedApiKey = ApiKeyManager.Get(Server)?.GetRawApiKey() ?? string.Empty;

            var credential = new ApiKeyCredential(CachedApiKey);
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
        private string _server = string.Empty;
        private string _model = string.Empty;
        private string _reasoningEffortLevel = Options.IgnoredReasoningEffortLevel;
        private bool _autoFetchAvailableModels = true;
    }
}
