using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

using DevBoard.AI.Routing;

namespace DevBoard.ViewModels
{
    public sealed class DevSpaceAIRouter : ObservableObject
    {
        public AvaloniaList<AIRouterProviderSettings> Providers { get; } = [];

        public AIRouterProviderSettings SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (SetProperty(ref _selectedProvider, value))
                    StatusText = value == null ? string.Empty : value.IsActive ? "Ready" : "Disabled";
            }
        }

        public string Endpoint => AIRouterSettings.Instance.ListenUrl.TrimEnd('/') + "/v1";

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public DevSpaceAIRouter()
        {
            foreach (var provider in AIRouterSettings.Instance.Providers)
                Providers.Add(provider.Clone());

            SelectedProvider = Providers.FirstOrDefault();
        }

        public AIRouterProviderSettings AddProvider()
        {
            var provider = new AIRouterProviderSettings
            {
                Id = $"provider-{Providers.Count + 1}",
                Name = "New Provider",
                BaseUrl = "http://127.0.0.1:5032/v1",
            };
            EnsureUniqueId(provider);
            Providers.Add(provider);
            SelectedProvider = provider;
            return provider;
        }

        public AIRouterProviderSettings DuplicateSelected()
        {
            if (SelectedProvider == null)
                return null;

            var copy = SelectedProvider.Clone(createNewId: true);
            copy.Name = $"{SelectedProvider.Name} Copy";
            copy.Id = $"{SelectedProvider.Id}-copy";
            EnsureUniqueId(copy);
            Providers.Add(copy);
            SelectedProvider = copy;
            Save();
            return copy;
        }

        public void DeleteSelected()
        {
            if (SelectedProvider == null)
                return;

            var index = Providers.IndexOf(SelectedProvider);
            Providers.Remove(SelectedProvider);
            SelectedProvider = Providers.Count == 0 ? null : Providers[Math.Min(index, Providers.Count - 1)];
            Save();
        }

        public void ToggleSelected()
        {
            if (SelectedProvider == null)
                return;

            SelectedProvider.IsActive = !SelectedProvider.IsActive;
            StatusText = SelectedProvider.IsActive ? "Ready" : "Disabled";
            Save();
            OnPropertyChanged(nameof(SelectedProvider));
        }

        public void Save()
        {
            foreach (var provider in Providers)
                provider.Validate();

            var ids = Providers.Select(x => x.Id).ToArray();
            if (ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length)
                throw new ArgumentException("AI Router provider IDs must be unique.");

            var settings = AIRouterSettings.Instance;
            settings.Providers = Providers.Select(x => x.Clone()).ToList();
            settings.Save();
            StatusText = SelectedProvider?.IsActive == false ? "Disabled" : "Saved";
        }

        public async Task ExportAsync(Stream stream, bool includeSecrets = false)
        {
            Save();
            await AIRouterProviderExchange.ExportAsync(Providers, stream, includeSecrets);
            StatusText = "Exported";
        }

        public async Task<int> ImportAsync(Stream stream)
        {
            var imported = await AIRouterProviderExchange.ImportAsync(stream);
            foreach (var provider in imported)
            {
                var existing = Providers.FirstOrDefault(x => string.Equals(x.Id, provider.Id, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    Providers[Providers.IndexOf(existing)] = provider;
                else
                    Providers.Add(provider);
            }

            SelectedProvider = imported.Count > 0
                ? Providers.FirstOrDefault(x => string.Equals(x.Id, imported[^1].Id, StringComparison.OrdinalIgnoreCase))
                : SelectedProvider;
            Save();
            StatusText = $"Imported {imported.Count} provider(s)";
            return imported.Count;
        }

        public async Task TestSelectedAsync()
        {
            var provider = SelectedProvider;
            if (provider == null)
                return;

            provider.Validate();
            StatusText = "Testing…";

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Min(provider.TimeoutSeconds, 30)) };
                var baseUrl = provider.BaseUrl.TrimEnd('/');
                var url = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                    ? baseUrl + "/models"
                    : baseUrl + "/v1/models";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                var apiKey = provider.ApiKey;
                if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(provider.ApiKeyEnvironment))
                    apiKey = Environment.GetEnvironmentVariable(provider.ApiKeyEnvironment);
                if (!string.IsNullOrWhiteSpace(apiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                foreach (var header in provider.ExtraHeaders)
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);

                using var response = await client.SendAsync(request);
                StatusText = response.IsSuccessStatusCode
                    ? "Healthy"
                    : $"Unavailable ({(int)response.StatusCode})";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                StatusText = "Unavailable";
            }
        }

        private void EnsureUniqueId(AIRouterProviderSettings provider)
        {
            var seed = string.IsNullOrWhiteSpace(provider.Id) ? "provider" : provider.Id.Trim();
            var candidate = seed;
            var suffix = 2;
            while (Providers.Any(x => !ReferenceEquals(x, provider) && string.Equals(x.Id, candidate, StringComparison.OrdinalIgnoreCase)))
                candidate = $"{seed}-{suffix++}";
            provider.Id = candidate;
        }

        private AIRouterProviderSettings _selectedProvider;
        private string _statusText = string.Empty;
    }
}
