using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.AI
{
    public enum ApiKeyStorage
    {
        EnvironmentVariable = 0,
        SystemCredentialManager,
        PlainText,
    }

    public class ApiKey : ObservableObject
    {
        public string Service
        {
            get;
            set;
        } = string.Empty;

        public ApiKeyStorage Storage
        {
            get => _storage;
            set
            {
                if (SetProperty(ref _storage, value) && value == ApiKeyStorage.SystemCredentialManager)
                    OnPropertyChanged(nameof(IsValidStorage));
            }
        }

        public string KeyToAccess
        {
            get => _keyToAccess;
            set => SetProperty(ref _keyToAccess, value);
        }

        [JsonIgnore]
        public bool IsValidStorage
        {
            get => Storage != ApiKeyStorage.SystemCredentialManager || !OperatingSystem.IsLinux();
        }

        public void PrepareForSaving()
        {
            if (Storage == ApiKeyStorage.SystemCredentialManager)
            {
                Native.OS.CredentialStorage?.Store(Service, "SourceGit", KeyToAccess);
                KeyToAccess = string.Empty;
            }
        }

        public void Cleanup()
        {
            if (Storage == ApiKeyStorage.SystemCredentialManager)
                Native.OS.CredentialStorage?.Erase(Service, "SourceGit");
        }

        public ApiKey CreateEditObject()
        {
            var cloned = new ApiKey()
            {
                Service = Service,
                Storage = _storage,
            };

            if (_storage == ApiKeyStorage.SystemCredentialManager)
                cloned.KeyToAccess = Native.OS.CredentialStorage?.Read(Service, "SourceGit") ?? string.Empty;
            else
                cloned.KeyToAccess = _keyToAccess;

            return cloned;
        }

        public ApiKeyCredential ToCredential()
        {
            if (_storage == ApiKeyStorage.PlainText)
                return new ApiKeyCredential(_keyToAccess);

            if (_storage == ApiKeyStorage.EnvironmentVariable)
                return new ApiKeyCredential(Environment.GetEnvironmentVariable(_keyToAccess));

            var rawApiKey = Native.OS.CredentialStorage?.Read(Service, "SourceGit");
            return new ApiKeyCredential(rawApiKey ?? string.Empty);
        }

        private ApiKeyStorage _storage = ApiKeyStorage.EnvironmentVariable;
        private string _keyToAccess = string.Empty;
    }

    public static class ApiKeyManager
    {
        static ApiKeyManager()
        {
            _saveTo = Path.Combine(
                Native.OS.BasicDirectories.ConfigDir,
                "api_keys.json");

            if (File.Exists(_saveTo))
            {
                try
                {
                    _keys = JsonSerializer.Deserialize(File.ReadAllText(_saveTo), JsonCodeGen.Default.ListApiKey);
                }
                catch
                {
                    _keys = new List<ApiKey>();
                }
            }
            else
            {
                _keys = new List<ApiKey>();
            }
        }

        public static ApiKey Get(string service)
        {
            return _keys.Find(k => k.Service.Equals(service, StringComparison.Ordinal));
        }

        public static void AddOrUpdate(ApiKey key)
        {
            var changed = true;
            var existed = false;
            foreach (var k in _keys)
            {
                if (k.Service.Equals(key.Service, StringComparison.Ordinal))
                {
                    if (k.Storage == key.Storage && k.KeyToAccess.Equals(key.KeyToAccess, StringComparison.Ordinal))
                    {
                        changed = false;
                    }
                    else
                    {
                        k.Storage = key.Storage;
                        k.KeyToAccess = key.KeyToAccess;
                    }

                    existed = true;
                    break;
                }
            }

            if (!existed)
                _keys.Add(key);

            if (changed)
                Save();
        }

        public static void Remove(string service)
        {
            var apiKey = _keys.Find(k => k.Service.Equals(service, StringComparison.Ordinal));
            if (apiKey == null)
                return;

            // Save first because `ApiKey.Cleanup` may raise exception.
            _keys.Remove(apiKey);
            Save();

            apiKey.Cleanup();
        }

        private static void Save()
        {
            var content = JsonSerializer.Serialize(_keys, JsonCodeGen.Default.ListApiKey);
            var tmpfile = $"{_saveTo}.tmp";
            File.WriteAllText(tmpfile, content);
            File.Move(tmpfile, _saveTo, true);
        }

        private static List<ApiKey> _keys = null;
        private static string _saveTo = string.Empty;
    }
}
