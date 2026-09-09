using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Custom HTTP headers sent with every request to this service.
        /// Needed by some OpenAI-compatible providers (e.g. OpenCode Go requires `x-opencode-session`).
        /// </summary>
        public Dictionary<string, string> ExtraHeaders
        {
            get;
            set;
        } = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Text representation of <see cref="ExtraHeaders"/> for UI editing. One `Name: Value` pair per line.
        /// Lines without a colon or with an empty/invalid name are ignored.
        /// </summary>
        [JsonIgnore]
        public string ExtraHeadersText
        {
            get => string.Join('\n', ExtraHeaders.Select(kv => $"{kv.Key}: {kv.Value}"));
            set
            {
                var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in value.Split('\n'))
                {
                    var idx = line.IndexOf(':');
                    if (idx <= 0)
                        continue;

                    var name = line.Substring(0, idx).Trim();
                    var headerValue = line.Substring(idx + 1).Trim();
                    if (name.Length == 0 || headerValue.Length == 0 || name.Any(char.IsWhiteSpace))
                        continue;

                    parsed[name] = headerValue;
                }

                ExtraHeaders = parsed;
            }
        }

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
                var azureOptions = new AzureOpenAIClientOptions();
                ApplyExtraHeaders(azureOptions);
                return new AzureOpenAIClient(new Uri(Server), credential, azureOptions);
            }

            var options = new OpenAIClientOptions() { Endpoint = new Uri(Server) };
            ApplyExtraHeaders(options);
            return new OpenAIClient(credential, options);
        }

        private void ApplyExtraHeaders(ClientPipelineOptions options)
        {
            if (ExtraHeaders.Count > 0)
                options.AddPolicy(new ExtraHeadersPolicy(ExtraHeaders), PipelinePosition.PerCall);
        }

        private sealed class ExtraHeadersPolicy : PipelinePolicy
        {
            public ExtraHeadersPolicy(Dictionary<string, string> headers)
            {
                _headers = headers;
            }

            public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
            {
                Apply(message);
                ProcessNext(message, pipeline, currentIndex);
            }

            public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
            {
                Apply(message);
                await ProcessNextAsync(message, pipeline, currentIndex);
            }

            private void Apply(PipelineMessage message)
            {
                foreach (var header in _headers)
                    message.Request.Headers.Set(header.Key, header.Value);
            }

            private readonly Dictionary<string, string> _headers;
        }

        private string _name = string.Empty;
        private string _model = string.Empty;
        private string _reasoningEffortLevel = Options.IgnoredReasoningEffortLevel;
        private bool _autoFetchAvailableModels = true;
    }
}
