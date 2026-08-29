using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Avalonia.Collections;

namespace SourceGit.ViewModels
{
    public sealed class GitAccountStore
    {
        public static GitAccountStore Instance { get; } = Load();

        public AvaloniaList<Models.GitAccount> Accounts { get; } = [];

        public void Save()
        {
            var data = new GitAccountStoreData
            {
                Accounts = [.. Accounts],
            };

            var tmp = Path.Combine(Native.OS.DataDir, "git_accounts_tmp.json");
            var content = JsonSerializer.Serialize(data, GitAccountJsonCodeGen.Default.GitAccountStoreData);
            File.WriteAllText(tmp, content);

            var file = Path.Combine(Native.OS.DataDir, "git_accounts.json");
            File.Move(tmp, file, true);
        }

        private static GitAccountStore Load()
        {
            var store = new GitAccountStore();
            var file = Path.Combine(Native.OS.DataDir, "git_accounts.json");
            if (!File.Exists(file))
                return store;

            try
            {
                using var stream = File.OpenRead(file);
                var data = JsonSerializer.Deserialize(stream, GitAccountJsonCodeGen.Default.GitAccountStoreData);
                if (data?.Accounts != null)
                    store.Accounts.AddRange(data.Accounts);
            }
            catch
            {
                // Keep an empty store if the persisted file is invalid.
            }

            return store;
        }
    }

    internal sealed class GitAccountStoreData
    {
        public List<Models.GitAccount> Accounts { get; set; } = [];
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(GitAccountStoreData))]
    internal partial class GitAccountJsonCodeGen : JsonSerializerContext { }
}
