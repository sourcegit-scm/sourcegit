using System;
using System.IO;
using System.Text.Json;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class CommitMessageEditor : ChromelessWindow
    {
        public string ConventionalTypesOverride
        {
            get;
            private set;
        } = string.Empty;

        public CommitMessageEditor()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        public void AsStandalone(string file)
        {
            var gitDir = new Commands.QueryGitDir(Path.GetDirectoryName(file)).GetResult();
            if (!string.IsNullOrEmpty(gitDir))
            {
                var settingsFile = Path.Combine(gitDir, "sourcegit.settings");
                if (File.Exists(settingsFile))
                {
                    try
                    {
                        using var stream = File.OpenRead(settingsFile);
                        var settings = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.RepositorySettings);
                        ConventionalTypesOverride = settings.ConventionalTypesOverride;
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }
            }

            _onSave = msg => File.WriteAllText(file, msg);
            _shouldExitApp = true;

            Editor.CommitMessage = File.ReadAllText(file).ReplaceLineEndings("\n").Trim();
        }

        public void AsBuiltin(string conventionalTypesOverride, string msg, Action<string> onSave)
        {
            ConventionalTypesOverride = conventionalTypesOverride;

            _onSave = onSave;
            _shouldExitApp = false;

            Editor.CommitMessage = msg;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_shouldExitApp)
                App.Quit(_exitCode);
        }

        private void SaveAndClose(object _1, RoutedEventArgs _2)
        {
            _onSave?.Invoke(Editor.CommitMessage);
            _exitCode = 0;
            Close();
        }

        private Action<string> _onSave = null;
        private bool _shouldExitApp = true;
        private int _exitCode = -1;
    }
}
