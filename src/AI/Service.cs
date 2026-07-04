using System;
using System.ClientModel;
using System.Collections.Generic;
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

        public List<string> AvailableModels
        {
            get => _availableModels;
            set => SetProperty(ref _availableModels, value);
        }

        public string Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        public string AdditionalPrompt
        {
            get;
            set;
        } = string.Empty;

        public void AddModel(string model)
        {
            if (!_availableModels.Contains(model))
            {
                var newList = new List<string>(_availableModels) { model };
                AvailableModels = newList;
            }
        }

        public void RemoveModel(string model)
        {
            if (_availableModels.Contains(model))
            {
                var newList = new List<string>(_availableModels);
                newList.Remove(model);
                AvailableModels = newList;
            }
        }

        public List<string> FetchModelsFromServer()
        {
            var allModels = GetOpenAIClient().GetOpenAIModelClient().GetModels();
            var result = new List<string>();
            foreach (var model in allModels.Value)
                result.Add(model.Id);
            return result;
        }

        public ChatClient GetChatClient()
        {
            return !string.IsNullOrEmpty(Model) ? GetOpenAIClient().GetChatClient(Model) : null;
        }

        public Service Clone()
        {
            return new Service
            {
                Name = Name,
                Server = Server,
                ApiKey = ApiKey,
                ReadApiKeyFromEnv = ReadApiKeyFromEnv,
                Model = Model,
                AdditionalPrompt = AdditionalPrompt,
                AvailableModels = new List<string>(AvailableModels),
            };
        }

        private OpenAIClient GetOpenAIClient()
        {
            var credential = new ApiKeyCredential(ReadApiKeyFromEnv ? Environment.GetEnvironmentVariable(ApiKey) : ApiKey);
            return Server.Contains("openai.azure.com/", StringComparison.Ordinal)
                ? new AzureOpenAIClient(new Uri(Server), credential)
                : new OpenAIClient(credential, new() { Endpoint = new Uri(Server) });
        }

        private string _name = string.Empty;
        private string _model = string.Empty;
        private List<string> _availableModels = [];
    }
}
