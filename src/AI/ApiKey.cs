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

        public ApiKeyCredential ToCredential()
        {
            if (_storage == ApiKeyStorage.PlainText)
                return new ApiKeyCredential(_keyToAccess);

            if (_storage == ApiKeyStorage.EnvironmentVariable)
                return new ApiKeyCredential(Environment.GetEnvironmentVariable(_keyToAccess));

            return new ApiKeyCredential(Native.OS.CredentialStorage?.Read(Service, "SourceGit") ?? string.Empty);
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

        private ApiKeyStorage _storage = ApiKeyStorage.EnvironmentVariable;
        private string _keyToAccess = string.Empty;
    }

    public class ApiKeyCollections
    {
        public List<ApiKey> Keys
        {
            get;
            set;
        } = new List<ApiKey>();

        public static ApiKey Get(string service)
        {
            return GetOrLoadAll().Keys.Find(k => k.Service.Equals(service, StringComparison.Ordinal));
        }

        public static void AddOrUpdate(ApiKey key)
        {
            var collection = GetOrLoadAll();
            var changed = true;
            var existed = false;

            if (key.Storage == ApiKeyStorage.SystemCredentialManager)
            {
                Native.OS.CredentialStorage?.Store(key.Service, "SourceGit", key.KeyToAccess);
                key.KeyToAccess = string.Empty;
            }

            foreach (var k in collection.Keys)
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
                collection.Keys.Add(key);

            if (changed)
            {
                var saveTo = GetSavedFilePath();
                var content = JsonSerializer.Serialize(collection, JsonCodeGen.Default.ApiKeyCollections);
                var tmpfile = $"{saveTo}.tmp";
                File.WriteAllText(tmpfile, content);
                File.Move(tmpfile, saveTo, true);
            }
        }

        public static void Remove(string service)
        {
            var collection = GetOrLoadAll();
            var apiKey = collection.Keys.Find(k => k.Service.Equals(service, StringComparison.Ordinal));
            if (apiKey == null)
                return;

            apiKey.Cleanup();
            collection.Keys.Remove(apiKey);

            var saveTo = GetSavedFilePath();
            var content = JsonSerializer.Serialize(collection, JsonCodeGen.Default.ApiKeyCollections);
            var tmpfile = $"{saveTo}.tmp";
            File.WriteAllText(tmpfile, content);
            File.Move(tmpfile, saveTo, true);
        }

        private static ApiKeyCollections GetOrLoadAll()
        {
            if (_instance != null)
                return _instance;

            var savedFile = GetSavedFilePath();
            if (File.Exists(savedFile))
            {
                try
                {
                    var content = File.ReadAllText(savedFile);
                    _instance = JsonSerializer.Deserialize(content, JsonCodeGen.Default.ApiKeyCollections);
                }
                catch
                {
                    _instance = new ApiKeyCollections();
                }
            }
            else
            {
                _instance = new ApiKeyCollections();
            }

            return _instance;
        }

        private static string GetSavedFilePath()
        {
            return Path.Combine(
                Native.OS.BasicDirectories.ConfigDir,
                "api_keys.json");
        }

        private static ApiKeyCollections _instance = null;
    }
}
