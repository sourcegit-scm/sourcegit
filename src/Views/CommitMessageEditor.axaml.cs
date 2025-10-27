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

            var content = File.ReadAllText(file).ReplaceLineEndings("\n").Trim();
            var parts = content.Split('\n', 2);
            Editor.SubjectEditor.Text = parts[0];
            if (parts.Length > 1)
                Editor.DescriptionEditor.Text = parts[1].Trim();
        }

        public void AsBuiltin(string conventionalTypesOverride, string msg, Action<string> onSave)
        {
            ConventionalTypesOverride = conventionalTypesOverride;

            _onSave = onSave;
            _shouldExitApp = false;

            var parts = msg.Split('\n', 2);
            Editor.SubjectEditor.Text = parts[0];
            if (parts.Length > 1)
                Editor.DescriptionEditor.Text = parts[1].Trim();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_shouldExitApp)
                App.Quit(_exitCode);
        }

        private void SaveAndClose(object _1, RoutedEventArgs _2)
        {
            _onSave?.Invoke(Editor.Text);
            _exitCode = 0;
            Close();
        }

        private Action<string> _onSave = null;
        private bool _shouldExitApp = true;
        private int _exitCode = -1;
    }
}
