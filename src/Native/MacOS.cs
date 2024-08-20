using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;

using Avalonia;

namespace SourceGit.Native
{
    [SupportedOSPlatform("macOS")]
    internal class MacOS : OS.IBackend
    {
        enum SupportedTerminals
        {
            NativeTerminal,
            iTerm2,
        }

        class Terminal
        {
            public SupportedTerminals Application { get; set; }

            public Terminal(SupportedTerminals application)
            {
                Application = application;
            }

            public void Open(string dir)
            {
                string command = GetTerminalCommand(dir);
                if (command == null)
                    return;

                var tmp = Path.GetTempFileName();
                File.WriteAllText(tmp, command);

                var proc = Process.Start("osascript", $"\"{tmp}\"");
                if (proc != null)
                    proc.Exited += (_, _) => File.Delete(tmp);
                else
                    File.Delete(tmp);
            }

            private string GetTerminalCommand(string dir)
            {
                switch(Application)
                {
                    case SupportedTerminals.NativeTerminal:
                        return GetNativeTerminalCommand(dir);
                    case SupportedTerminals.iTerm2:
                        return GetITerm2Command(dir);
                    default:
                        App.RaiseException(dir, $"Only supports native Terminal and iTerm2!");
                        return null;
                }
            }

            private string GetNativeTerminalCommand(string dir)
            {
                var builder = new StringBuilder();
                builder.AppendLine("on run argv");
                builder.AppendLine("    tell application \"Terminal\"");
                builder.AppendLine($"        do script \"cd {dir}\"");
                builder.AppendLine("        activate");
                builder.AppendLine("    end tell");
                builder.AppendLine("end run");
                return builder.ToString();
            }

            private string GetITerm2Command(string dir)
            {
                var builder = new StringBuilder();
                builder.AppendLine("on run argv");
                builder.AppendLine("    tell application \"iTerm2\"");
                builder.AppendLine("        create window with default profile");
                builder.AppendLine("        tell the current session of the current window");
                builder.AppendLine($"            write text \"cd {dir}\"");
                builder.AppendLine("        end tell");
                builder.AppendLine("    end tell");
                builder.AppendLine("end run");
                return builder.ToString();
            }
        }

        public MacOS()
        {
            // TODO: use config
            if (IsITermInstalled())
                _terminal = new Terminal(SupportedTerminals.iTerm2);
            else
                _terminal = new Terminal(SupportedTerminals.NativeTerminal);
        }

        public void SetupApp(AppBuilder builder)
        {
            builder.With(new MacOSPlatformOptions()
            {
                DisableDefaultApplicationMenuItems = true,
            });
        }

        public string FindGitExecutable()
        {
            // XCode built-in git
            return File.Exists("/usr/bin/git") ? "/usr/bin/git" : string.Empty;
        }

        public List<Models.ExternalTool> FindExternalTools()
        {
            var finder = new Models.ExternalToolsFinder();
            finder.VSCode(() => "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code");
            finder.VSCodeInsiders(() => "/Applications/Visual Studio Code - Insiders.app/Contents/Resources/app/bin/code");
            finder.VSCodium(() => "/Applications/VSCodium.app/Contents/Resources/app/bin/codium");
            finder.Fleet(() => $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Applications/Fleet.app/Contents/MacOS/Fleet");
            finder.FindJetBrainsFromToolbox(() => $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Library/Application Support/JetBrains/Toolbox");
            finder.SublimeText(() => "/Applications/Sublime Text.app/Contents/SharedSupport/bin/subl");
            return finder.Founded;
        }

        public void OpenBrowser(string url)
        {
            Process.Start("open", url);
        }

        public void OpenInFileManager(string path, bool select)
        {
            if (Directory.Exists(path))
            {
                Process.Start("open", $"\"{path}\"");
            }
            else if (File.Exists(path))
            {
                Process.Start("open", $"\"{path}\" -R");
            }
        }

        public void OpenTerminal(string workdir)
        {
            var dir = string.IsNullOrEmpty(workdir) ? "~" : workdir;
            dir = dir.Replace(" ", "\\ ");

            _terminal.Open(dir);
        }

        public void OpenWithDefaultEditor(string file)
        {
            Process.Start("open", file);
        }

        private bool IsITermInstalled()
        {
            return Directory. Exists("/Applications/iTerm.app");
        }

        private readonly Terminal _terminal = null;
    }
}
