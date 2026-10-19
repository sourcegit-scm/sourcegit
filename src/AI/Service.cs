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
        // Stable handle used as the account name when the API key is kept in the OS
        // secret store. Persisted so the mapping survives restarts.
        public string Id
        {
            get;
            set;
        } = string.Empty;

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

        // The effective API key used at runtime and bound by the UI. On platforms with a
        // secret store it is loaded from / saved to that store and never written to disk;
        // see PersistedApiKey, ResolveSecret and FlushSecret.
        [JsonIgnore]
        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        // The value written to preference.json under "ApiKey". On a platform with a secret
        // store this stays empty (unless the key is only an environment variable name, which
        // is not itself a secret), so the API key is never persisted in plaintext.
        [JsonPropertyName("ApiKey")]
        public string PersistedApiKey
        {
            get => (Native.OS.SupportsSecretStorage() && !ReadApiKeyFromEnv) ? string.Empty : _apiKey;
            set => _apiKey = value ?? string.Empty;
        }

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

        /// <summary>
        /// Loads the API key from the OS secret store into memory. If a plaintext key is
        /// still present (a legacy preference.json, or a config from a platform without a
        /// secret store), it is moved into the store; the method then returns true so the
        /// caller can save and scrub the plaintext copy from disk.
        /// </summary>
        public bool ResolveSecret()
        {
            if (!Native.OS.SupportsSecretStorage() || ReadApiKeyFromEnv)
                return false;

            EnsureId();

            if (!string.IsNullOrEmpty(_apiKey))
            {
                Native.OS.WriteSecret(Id, _apiKey);
                return true;
            }

            _apiKey = Native.OS.ReadSecret(Id) ?? string.Empty;
            return false;
        }

        /// <summary>
        /// Writes the current API key to the OS secret store (or removes it) so that the
        /// serialized PersistedApiKey can stay empty. A no-op where there is no secret store.
        /// </summary>
        public void FlushSecret()
        {
            if (!Native.OS.SupportsSecretStorage())
                return;

            EnsureId();

            if (ReadApiKeyFromEnv || string.IsNullOrEmpty(_apiKey))
                Native.OS.DeleteSecret(Id);
            else
                Native.OS.WriteSecret(Id, _apiKey);
        }

        /// <summary>
        /// Removes this service's secret from the OS secret store, if any. Call when the
        /// service is deleted.
        /// </summary>
        public void ClearSecret()
        {
            if (Native.OS.SupportsSecretStorage() && !string.IsNullOrEmpty(Id))
                Native.OS.DeleteSecret(Id);
        }

        private void EnsureId()
        {
            if (string.IsNullOrEmpty(Id))
                Id = Guid.NewGuid().ToString("N");
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
        private string _apiKey = string.Empty;
        private string _model = string.Empty;
        private string _reasoningEffortLevel = Options.IgnoredReasoningEffortLevel;
        private bool _autoFetchAvailableModels = true;
    }
}
