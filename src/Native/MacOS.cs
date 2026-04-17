using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace SourceGit.Native
{
    [SupportedOSPlatform("macOS")]
    internal class MacOS : OS.IBackend
    {
        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
        public static extern IntPtr objc_getClass(string name);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
        public static extern IntPtr sel_registerName(string name);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr objc_msgSendWithArg(IntPtr receiver, IntPtr selector, IntPtr arg);

        public void SetupApp(AppBuilder builder)
        {
            builder.With(new MacOSPlatformOptions()
            {
                DisableDefaultApplicationMenuItems = true,
            });

            // Fix `PATH` env on macOS.
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                path = "/opt/homebrew/bin:/opt/homebrew/sbin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin";
            else if (!path.Contains("/opt/homebrew/", StringComparison.Ordinal))
                path = "/opt/homebrew/bin:/opt/homebrew/sbin:" + path;

            var customPathFile = Path.Combine(OS.DataDir, "PATH");
            if (File.Exists(customPathFile))
            {
                var env = File.ReadAllText(customPathFile).Trim();
                if (!string.IsNullOrEmpty(env))
                    path = env;
            }

            Environment.SetEnvironmentVariable("PATH", path);
        }

        public void SetupWindow(Window window)
        {
            window.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.SystemChrome;
            window.ExtendClientAreaToDecorationsHint = true;
        }

        public void HideSelf()
        {
            IntPtr nsApplicationClass = objc_getClass("NSApplication");
            IntPtr nsSharedApplicationSelector = sel_registerName("sharedApplication");
            IntPtr nsApp = objc_msgSend(nsApplicationClass, nsSharedApplicationSelector);
            IntPtr nsMethodSelector = sel_registerName("hide:");
            IntPtr nsDelegateSelector = sel_registerName("delegate");
            IntPtr nsDelegate = objc_msgSend(nsApp, nsDelegateSelector);
            objc_msgSendWithArg(nsApp, nsMethodSelector, nsDelegate);
        }

        public void HideOtherApplications()
        {
            IntPtr nsApplicationClass = objc_getClass("NSApplication");
            IntPtr nsSharedApplicationSelector = sel_registerName("sharedApplication");
            IntPtr nsApp = objc_msgSend(nsApplicationClass, nsSharedApplicationSelector);
            IntPtr nsMethodSelector = sel_registerName("hideOtherApplications:");
            IntPtr nsDelegateSelector = sel_registerName("delegate");
            IntPtr nsDelegate = objc_msgSend(nsApp, nsDelegateSelector);
            objc_msgSendWithArg(nsApp, nsMethodSelector, nsDelegate);
        }

        public void ShowAllApplications()
        {
            IntPtr nsApplicationClass = objc_getClass("NSApplication");
            IntPtr nsSharedApplicationSelector = sel_registerName("sharedApplication");
            IntPtr nsApp = objc_msgSend(nsApplicationClass, nsSharedApplicationSelector);
            IntPtr nsMethodSelector = sel_registerName("unhideAllApplications:");
            IntPtr nsDelegateSelector = sel_registerName("delegate");
            IntPtr nsDelegate = objc_msgSend(nsApp, nsDelegateSelector);
            objc_msgSendWithArg(nsApp, nsMethodSelector, nsDelegate);
        }

        public string GetDataDir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SourceGit");
        }

        public string FindGitExecutable()
        {
            var gitPathVariants = new List<string>() {
                "/usr/bin/git",
                "/usr/local/bin/git",
                "/opt/homebrew/bin/git",
                "/opt/homebrew/opt/git/bin/git"
            };

            foreach (var path in gitPathVariants)
                if (File.Exists(path))
                    return path;

            return string.Empty;
        }

        public string FindTerminal(Models.ShellOrTerminal shell)
        {
            return shell.Exec;
        }

        public List<Models.ExternalTool> FindExternalTools()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var finder = new Models.ExternalToolsFinder();
            finder.VSCode(() => "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code");
            finder.VSCodeInsiders(() => "/Applications/Visual Studio Code - Insiders.app/Contents/Resources/app/bin/code");
            finder.VSCodium(() => "/Applications/VSCodium.app/Contents/Resources/app/bin/codium");
            finder.Cursor(() => "/Applications/Cursor.app/Contents/Resources/app/bin/cursor");
            finder.FindJetBrainsFromToolbox(() => Path.Combine(home, "Library/Application Support/JetBrains/Toolbox"));
            finder.SublimeText(() => "/Applications/Sublime Text.app/Contents/SharedSupport/bin/subl");
            finder.Zed(() => File.Exists("/usr/local/bin/zed") ? "/usr/local/bin/zed" : "/Applications/Zed.app/Contents/MacOS/cli");
            return finder.Tools;
        }

        public void OpenBrowser(string url)
        {
            Process.Start("open", url);
        }

        public void OpenInFileManager(string path)
        {
            if (Directory.Exists(path))
                Process.Start("open", path.Quoted());
            else if (File.Exists(path))
                Process.Start("open", $"{path.Quoted()} -R");
        }

        public void OpenTerminal(string workdir, string _)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = string.IsNullOrEmpty(workdir) ? home : workdir;
            Process.Start("open", $"-a {OS.ShellOrTerminal} {dir.Quoted()}");
        }

        public void OpenWithDefaultEditor(string file)
        {
            Process.Start("open", file.Quoted());
        }
    }
}
