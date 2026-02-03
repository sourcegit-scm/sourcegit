using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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

        public static RepositorySettings Get(string repo, string gitCommonDir)
        {
            var fileInfo = new FileInfo(Path.Combine(gitCommonDir, "sourcegit.settings"));
            var fullpath = fileInfo.FullName;
            if (_cache.TryGetValue(fullpath, out var setting))
            {
                setting._usedBy.Add(repo);
                return setting;
            }

            if (!File.Exists(fullpath))
            {
                setting = new RepositorySettings();
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
                    setting = new RepositorySettings();
                }
            }

            setting._file = fullpath;
            setting._usedBy.Add(repo);
            _cache.Add(fullpath, setting);
            return setting;
        }

        public void TryUnload(string repo)
        {
            _usedBy.Remove(repo);

            if (_usedBy.Count == 0)
            {
                try
                {
                    using var stream = File.Create(_file);
                    JsonSerializer.Serialize(stream, this, JsonCodeGen.Default.RepositorySettings);
                }
                catch
                {
                    // Ignore save errors
                }

                _cache.Remove(_file);
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

        private static Dictionary<string, RepositorySettings> _cache = new();
        private string _file = string.Empty;
        private HashSet<string> _usedBy = new();
    }
}
