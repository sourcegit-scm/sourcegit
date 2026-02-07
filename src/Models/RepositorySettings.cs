using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Avalonia.Collections;

namespace SourceGit.Models
{
    public class RepositorySettings
    {
        public string DefaultRemote
        {
            get;
            set;
        } = string.Empty;

        public int PreferredMergeMode
        {
            get;
            set;
        } = 0;

        public string ConventionalTypesOverride
        {
            get;
            set;
        } = string.Empty;

        public bool EnableAutoFetch
        {
            get;
            set;
        } = false;

        public int AutoFetchInterval
        {
            get;
            set;
        } = 10;

        public bool AskBeforeAutoUpdatingSubmodules
        {
            get;
            set;
        } = false;

        public string PreferredOpenAIService
        {
            get;
            set;
        } = "---";

        public AvaloniaList<CommitTemplate> CommitTemplates
        {
            get;
            set;
        } = [];

        public AvaloniaList<string> CommitMessages
        {
            get;
            set;
        } = [];

        public AvaloniaList<CustomAction> CustomActions
        {
            get;
            set;
        } = [];

        public static RepositorySettings Get(string gitCommonDir)
        {
            var fileInfo = new FileInfo(Path.Combine(gitCommonDir, "sourcegit.settings"));
            var fullpath = fileInfo.FullName;
            if (_cache.TryGetValue(fullpath, out var setting))
                return setting;

            if (!File.Exists(fullpath))
            {
                setting = new();
            }
            else
            {
                try
                {
                    using var stream = File.OpenRead(fullpath);
                    setting = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.RepositorySettings);
                }
                catch
                {
                    setting = new();
                }
            }

            // Serialize setting again to make sure there are no unnecessary whitespaces.
            Task.Run(() =>
            {
                var formatted = JsonSerializer.Serialize(setting, JsonCodeGen.Default.RepositorySettings);
                setting._orgHash = HashContent(formatted);
            });

            setting._file = fullpath;
            _cache.Add(fullpath, setting);
            return setting;
        }

        public async Task SaveAsync()
        {
            try
            {
                var content = JsonSerializer.Serialize(this, JsonCodeGen.Default.RepositorySettings);
                var hash = HashContent(content);
                if (!hash.Equals(_orgHash, StringComparison.Ordinal))
                {
                    await File.WriteAllTextAsync(_file, content);
                    _orgHash = hash;
                }
            }
            catch
            {
                // Ignore save errors
            }
        }

        public void PushCommitMessage(string message)
        {
            message = message.Trim().ReplaceLineEndings("\n");
            var existIdx = CommitMessages.IndexOf(message);
            if (existIdx == 0)
                return;

            if (existIdx > 0)
            {
                CommitMessages.Move(existIdx, 0);
                return;
            }

            if (CommitMessages.Count > 9)
                CommitMessages.RemoveRange(9, CommitMessages.Count - 9);

            CommitMessages.Insert(0, message);
        }

        public CustomAction AddNewCustomAction()
        {
            var act = new CustomAction() { Name = "Unnamed Action" };
            CustomActions.Add(act);
            return act;
        }

        public void RemoveCustomAction(CustomAction act)
        {
            if (act != null)
                CustomActions.Remove(act);
        }

        public void MoveCustomActionUp(CustomAction act)
        {
            var idx = CustomActions.IndexOf(act);
            if (idx > 0)
                CustomActions.Move(idx - 1, idx);
        }

        public void MoveCustomActionDown(CustomAction act)
        {
            var idx = CustomActions.IndexOf(act);
            if (idx < CustomActions.Count - 1)
                CustomActions.Move(idx + 1, idx);
        }

        private static string HashContent(string source)
        {
            var hash = MD5.HashData(Encoding.Default.GetBytes(source));
            var builder = new StringBuilder(hash.Length * 2);
            foreach (var c in hash)
                builder.Append(c.ToString("x2"));
            return builder.ToString();
        }

        private static Dictionary<string, RepositorySettings> _cache = new();
        private string _file = string.Empty;
        private string _orgHash = string.Empty;
    }
}
