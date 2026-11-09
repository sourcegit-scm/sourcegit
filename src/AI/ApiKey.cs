using System;
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
        SystemKeychain,
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
                if (SetProperty(ref _storage, value) && value == ApiKeyStorage.SystemKeychain)
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
            get
            {
                if (_storage == ApiKeyStorage.SystemKeychain)
                    return Native.OS.Keychain != null;
                return true;
            }
        }

        public void PrepareForSaving()
        {
            if (Storage == ApiKeyStorage.SystemKeychain)
            {
                Native.OS.Keychain?.Store(Service, "SourceGit", KeyToAccess);
                KeyToAccess = string.Empty;
            }
        }

        public void CleanupForRemove()
        {
            if (Storage == ApiKeyStorage.SystemKeychain)
                Native.OS.Keychain?.Erase(Service, "SourceGit");
        }

        public string GetRawApiKey()
        {
            if (_storage == ApiKeyStorage.PlainText)
                return _keyToAccess;

            if (_storage == ApiKeyStorage.EnvironmentVariable)
                return Environment.GetEnvironmentVariable(_keyToAccess);

            return Native.OS.Keychain?.Read(Service, "SourceGit") ?? string.Empty;
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
            key.PrepareForSaving();

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

            // Save first because `ApiKey.CleanupForRemove` may raise exception.
            _keys.Remove(apiKey);
            Save();

            apiKey.CleanupForRemove();
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
