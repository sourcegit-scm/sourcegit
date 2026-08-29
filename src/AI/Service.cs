using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenAI;
using OpenAI.Chat;

namespace SourceGit.AI
{
    public enum ProviderType
    {
        OpenAI = 0,
        LocalLlm = 1,
    }

    public enum LocalLlmBackend
    {
        Auto = 0,
        Cpu = 1,
        Cuda = 2,
        Vulkan = 3,
    }

    public class Service : ObservableObject, IDisposable
    {
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public ProviderType Provider
        {
            get => _provider;
            set
            {
                if (SetProperty(ref _provider, value))
                {
                    OnPropertyChanged(nameof(ProviderIndex));
                    OnPropertyChanged(nameof(IsOpenAI));
                    OnPropertyChanged(nameof(IsLocalLlm));
                    DisposeLocalLlmRuntime();
                }
            }
        }

        [JsonIgnore]
        public int ProviderIndex
        {
            get => (int)_provider;
            set => Provider = value == (int)ProviderType.LocalLlm ? ProviderType.LocalLlm : ProviderType.OpenAI;
        }

        [JsonIgnore]
        public bool IsOpenAI => Provider == ProviderType.OpenAI;

        [JsonIgnore]
        public bool IsLocalLlm => Provider == ProviderType.LocalLlm;

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

        public string LocalModelPath
        {
            get => _localModelPath;
            set
            {
                value ??= string.Empty;
                if (SetProperty(ref _localModelPath, value))
                {
                    DisposeLocalLlmRuntime();
                    UpdateLocalModelStatus();
                }
            }
        }

        public LocalLlmBackend LocalBackend
        {
            get => _localBackend;
            set
            {
                if (SetProperty(ref _localBackend, value))
                {
                    OnPropertyChanged(nameof(LocalBackendIndex));
                    DisposeLocalLlmRuntime();
                }
            }
        }

        [JsonIgnore]
        public int LocalBackendIndex
        {
            get => (int)_localBackend;
            set => LocalBackend = value switch
            {
                (int)LocalLlmBackend.Cpu => LocalLlmBackend.Cpu,
                (int)LocalLlmBackend.Cuda => LocalLlmBackend.Cuda,
                (int)LocalLlmBackend.Vulkan => LocalLlmBackend.Vulkan,
                _ => LocalLlmBackend.Auto,
            };
        }

        public int GpuLayerCount
        {
            get => _gpuLayerCount;
            set
            {
                var normalized = Math.Max(-1, value);
                if (SetProperty(ref _gpuLayerCount, normalized))
                    DisposeLocalLlmRuntime();
            }
        }

        public int LocalThreads
        {
            get => _localThreads;
            set
            {
                var normalized = Math.Max(1, value);
                if (SetProperty(ref _localThreads, normalized))
                    DisposeLocalLlmRuntime();
            }
        }

        public uint LocalBatchSize
        {
            get => _localBatchSize;
            set
            {
                var normalized = Math.Max(1u, value);
                if (SetProperty(ref _localBatchSize, normalized))
                    DisposeLocalLlmRuntime();
            }
        }

        public float Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, Math.Clamp(value, 0.0f, 2.0f));
        }

        public uint ContextWindow
        {
            get => _contextWindow;
            set
            {
                var normalized = Math.Max(512u, value);
                if (SetProperty(ref _contextWindow, normalized))
                    DisposeLocalLlmRuntime();
            }
        }

        public bool AutoLoadModel
        {
            get => _autoLoadModel;
            set => SetProperty(ref _autoLoadModel, value);
        }

        [JsonIgnore]
        public string LocalModelStatus
        {
            get => _localModelStatus;
            private set => SetProperty(ref _localModelStatus, value);
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

        public void FetchAvailableModels()
        {
            if (IsLocalLlm)
            {
                AvailableModels = string.IsNullOrWhiteSpace(LocalModelPath) ? [] : [Path.GetFileName(LocalModelPath)];
                UpdateLocalModelStatus();
                if (AutoLoadModel && IsLocalModelAvailable())
                    _ = PreloadLocalModelSafelyAsync();
                return;
            }

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
            if (IsLocalLlm)
                return null;

            return !string.IsNullOrEmpty(Model) ? GetOpenAIClient().GetChatClient(Model) : null;
        }

        public bool IsLocalModelAvailable()
        {
            return !string.IsNullOrWhiteSpace(LocalModelPath) && File.Exists(LocalModelPath);
        }

        public string GetLocalModelValidationError()
        {
            if (string.IsNullOrWhiteSpace(LocalModelPath) || !File.Exists(LocalModelPath))
                return "Model not found";

            return string.Empty;
        }

        internal async Task PreloadLocalModelAsync(CancellationToken cancellationToken)
        {
            if (!IsLocalLlm)
                return;

            await GetLocalLlmRuntimeAsync(cancellationToken);
        }

        internal async Task StreamLocalLlmAsync(string prompt, Action<string> onUpdate, CancellationToken cancellationToken)
        {
            var runtime = await GetLocalLlmRuntimeAsync(cancellationToken);
            await runtime.StreamAsync(prompt, Temperature, onUpdate, cancellationToken);
        }

        public void Dispose()
        {
            DisposeLocalLlmRuntime();
            GC.SuppressFinalize(this);
        }

        private OpenAIClient GetOpenAIClient()
        {
            var credential = new ApiKeyCredential(ReadApiKeyFromEnv ? Environment.GetEnvironmentVariable(ApiKey) : ApiKey);
            return Server.Contains("openai.azure.com/", StringComparison.Ordinal)
                ? new AzureOpenAIClient(new Uri(Server), credential)
                : new OpenAIClient(credential, new() { Endpoint = new Uri(Server) });
        }

        private async Task PreloadLocalModelSafelyAsync()
        {
            try
            {
                await PreloadLocalModelAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                UpdateLocalModelStatus();
            }
            catch (Exception ex)
            {
                LocalModelStatus = $"Failed: {ex.Message}";
            }
        }

        private async Task<LocalLlmRuntime> GetLocalLlmRuntimeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var error = GetLocalModelValidationError();
            if (!string.IsNullOrEmpty(error))
            {
                LocalModelStatus = error;
                throw new FileNotFoundException(error, LocalModelPath);
            }

            var current = _localLlmRuntime.Current;
            if (current != null)
                return current;

            var modelPath = LocalModelPath;
            var backend = LocalBackend;
            var contextWindow = ContextWindow;
            var gpuLayerCount = GpuLayerCount;
            var threads = LocalThreads;
            var batchSize = LocalBatchSize;

            if (!_localLlmRuntime.IsLoading)
                LocalModelStatus = "Loading model...";

            try
            {
                var runtime = await _localLlmRuntime.GetOrLoadAsync(
                    () => LocalLlmRuntime.Load(modelPath, backend, contextWindow, gpuLayerCount, threads, batchSize),
                    cancellationToken);
                LocalModelStatus = $"Loaded ({runtime.Backend})";
                return runtime;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                UpdateLocalModelStatus();
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (_localLlmRuntime.Current == null)
                    LocalModelStatus = $"Failed: {ex.Message}";
                throw;
            }
        }

        private void UpdateLocalModelStatus()
        {
            if (!IsLocalLlm)
                LocalModelStatus = string.Empty;
            else if (string.IsNullOrWhiteSpace(LocalModelPath))
                LocalModelStatus = "No model selected";
            else if (!File.Exists(LocalModelPath))
                LocalModelStatus = "Model not found";
            else if (_localLlmRuntime.Current is { } runtime)
                LocalModelStatus = $"Loaded ({runtime.Backend})";
            else if (_localLlmRuntime.IsLoading)
                LocalModelStatus = "Loading model...";
            else
                LocalModelStatus = "Ready";
        }

        private void DisposeLocalLlmRuntime()
        {
            _localLlmRuntime.Reset();
            UpdateLocalModelStatus();
        }

        private string _name = string.Empty;
        private ProviderType _provider = ProviderType.OpenAI;
        private string _model = string.Empty;
        private string _localModelPath = string.Empty;
        private LocalLlmBackend _localBackend = LocalLlmBackend.Auto;
        private int _gpuLayerCount = -1;
        private int _localThreads = Math.Max(1, Environment.ProcessorCount / 2);
        private uint _localBatchSize = 512;
        private float _temperature = 0.2f;
        private uint _contextWindow = 10000;
        private bool _autoLoadModel = true;
        private string _localModelStatus = string.Empty;
        private string _reasoningEffortLevel = Options.IgnoredReasoningEffortLevel;
        private bool _autoFetchAvailableModels = true;
        private readonly AsyncLoadCoordinator<LocalLlmRuntime> _localLlmRuntime = new();
    }
}
