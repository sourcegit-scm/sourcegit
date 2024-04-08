using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;

using Avalonia;
using Avalonia.Media;

namespace SourceGit.Native
{
    [SupportedOSPlatform("macOS")]
    internal class MacOS : OS.IBackend
    {
        public void SetupApp(AppBuilder builder)
        {
            builder.With(new FontManagerOptions()
            {
                DefaultFamilyName = "PingFang SC",
            });
        }

        public string FindGitExecutable()
        {
            if (File.Exists("/usr/bin/git"))
                return "/usr/bin/git";
            return string.Empty;
        }

        public IReadOnlyList<Models.ExternalTerminal> FindExternalTerminals()
        {
            var finder = new Models.ExternalTerminalFinder();
            finder.AppleScript(new AppleScriptTerminal());
            return finder.Terminals;
        }

        public List<Models.ExternalEditor> FindExternalEditors()
        {
            var finder = new Models.ExternalEditorFinder();
            finder.VSCode(() => "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code");
            finder.VSCodeInsiders(() => "/Applications/Visual Studio Code - Insiders.app/Contents/Resources/app/bin/code");
            finder.Fleet(() => $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Applications/Fleet.app/Contents/MacOS/Fleet");
            finder.SublimeText(() => "/Applications/Sublime Text.app/Contents/SharedSupport/bin");
            return finder.Editors;
        }

        public void OpenBrowser(string url)
        {
            Process.Start("open", url);
        }

        public void OpenInFileManager(string path, bool select)
        {
            if (Directory.Exists(path))
            {
                Process.Start("open", path);
            }
            else if (File.Exists(path))
            {
                Process.Start("open", $"\"{path}\" -R");
            }
        }

        public void OpenTerminal(string workdir)
        {
            new AppleScriptTerminal().Open(workdir);
        }

        public void OpenWithDefaultEditor(string file)
        {
            Process.Start("open", file);
        }
        
        private sealed record AppleScriptTerminal : Models.ExternalTerminal
        {
            public AppleScriptTerminal()
            {
                Executable = "/usr/bin/osascript";
                OpenCmdArgs = "";
            }
            
            public override void Open(string repo)
            {
                var dir = string.IsNullOrEmpty(repo) ? "~" : repo;
                var tmp = Path.GetTempFileName();
                File.WriteAllText(tmp,
                    $"""
                    on run argv
                       tell application "Terminal"
                            do script "cd '{dir}'"
                            activate
                        end tell
                    end run
                    """);

                var proc = Process.Start("/usr/bin/osascript", $"\"{tmp}\"");
                proc.Exited += (o, e) => File.Delete(tmp);
            }
        }
    }
}
