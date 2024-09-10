using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

using Avalonia;

namespace SourceGit.Native
{
    [SupportedOSPlatform("macOS")]
    internal class MacOS : OS.IBackend
    {
        public void SetupApp(AppBuilder builder)
        {
            builder.With(new MacOSPlatformOptions()
            {
                DisableDefaultApplicationMenuItems = true,
            });
        }

        public string FindGitExecutable()
        {
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
                Process.Start("open", $"\"{path}\"");
            else if (File.Exists(path))
                Process.Start("open", $"\"{path}\" -R");
        }

        public void OpenTerminal(string workdir)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = string.IsNullOrEmpty(workdir) ? home : workdir;
            var terminal = DetectTerminal();
            Process.Start("open", $"-a {terminal} \"{dir}\"");
        }

        public void OpenWithDefaultEditor(string file)
        {
            Process.Start("open", $"\"{file}\"");
        }

        private static string DetectTerminal()
        {
            if (Directory.Exists("/Applications/iTerm.app"))
                return "iTerm";

            return "Terminal";
        }
    }
}
