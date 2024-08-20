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
        enum TerminalType
        {
            Terminal,
            iTerm2,
        }

        public MacOS()
        {
            _terminal = Directory.Exists("/Applications/iTerm.app") ? TerminalType.iTerm2 : TerminalType.Terminal;
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

            var cmdBuilder = new StringBuilder();
            switch (_terminal)
            {
                case TerminalType.iTerm2:
                    cmdBuilder.AppendLine("on run argv");
                    cmdBuilder.AppendLine("    tell application \"iTerm2\"");
                    cmdBuilder.AppendLine("        create window with default profile");
                    cmdBuilder.AppendLine("        tell the current session of the current window");
                    cmdBuilder.AppendLine($"            write text \"cd {dir}\"");
                    cmdBuilder.AppendLine("        end tell");
                    cmdBuilder.AppendLine("    end tell");
                    cmdBuilder.AppendLine("end run");
                    break;
                default:
                    cmdBuilder.AppendLine("on run argv");
                    cmdBuilder.AppendLine("    tell application \"Terminal\"");
                    cmdBuilder.AppendLine($"        do script \"cd {dir}\"");
                    cmdBuilder.AppendLine("        activate");
                    cmdBuilder.AppendLine("    end tell");
                    cmdBuilder.AppendLine("end run");
                    break;
            }

            var tmp = Path.GetTempFileName();
            File.WriteAllText(tmp, cmdBuilder.ToString());

            var proc = Process.Start("osascript", $"\"{tmp}\"");
            if (proc != null)
                proc.Exited += (_, _) => File.Delete(tmp);
            else
                File.Delete(tmp);
        }

        public void OpenWithDefaultEditor(string file)
        {
            Process.Start("open", $"\"{file}\"");
        }

        private readonly TerminalType _terminal;
    }
}
