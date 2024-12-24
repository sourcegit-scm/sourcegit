using System;
using System.Collections.Generic;
#if ENABLE_PORTABLE
using System.Diagnostics;
#endif
using System.IO;

using Avalonia;

namespace SourceGit.Native
{
    public static class OS
    {
        public interface IBackend
        {
            void SetupApp(AppBuilder builder);

            string FindGitExecutable();
            string FindTerminal(Models.ShellOrTerminal shell);
            List<Models.ExternalTool> FindExternalTools();

            void OpenTerminal(string workdir);
            void OpenInFileManager(string path, bool select);
            void OpenBrowser(string url);
            void OpenWithDefaultEditor(string file);
        }

        public static string DataDir { get; private set; } = string.Empty;
        public static string GitExecutable { get; set; } = string.Empty;
        public static string ShellOrTerminal { get; set; } = string.Empty;
        public static List<Models.ExternalTool> ExternalTools { get; set; } = [];
        public static string CustomPathEnv { get; set; } = string.Empty;

        static OS()
        {
            if (OperatingSystem.IsWindows())
            {
                _backend = new Windows();
            }
            else if (OperatingSystem.IsMacOS())
            {
                _backend = new MacOS();
            }
            else if (OperatingSystem.IsLinux())
            {
                _backend = new Linux();
            }
            else
            {
                throw new Exception("Platform unsupported!!!");
            }
        }

        public static void SetupApp(AppBuilder builder)
        {
            _backend.SetupApp(builder);
        }

        public static void SetupDataDir()
        {
#if ENABLE_PORTABLE
            if (OperatingSystem.IsWindows())
            {
                var execFile = Process.GetCurrentProcess().MainModule!.FileName;
                DataDir = Path.Combine(Path.GetDirectoryName(execFile), "data");
                if (!Directory.Exists(DataDir))
                    Directory.CreateDirectory(DataDir);
                return;
            }
#endif
            var osAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(osAppDataDir))
                DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sourcegit");
            else
                DataDir = Path.Combine(osAppDataDir, "SourceGit");

            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);
        }

        public static void SetupEnternalTools()
        {
            ExternalTools = _backend.FindExternalTools();
        }

        public static string FindGitExecutable()
        {
            return _backend.FindGitExecutable();
        }

        public static bool TestShellOrTerminal(Models.ShellOrTerminal shell)
        {
            return !string.IsNullOrEmpty(_backend.FindTerminal(shell));
        }

        public static void SetShellOrTerminal(Models.ShellOrTerminal shell)
        {
            if (shell == null)
                ShellOrTerminal = string.Empty;
            else
                ShellOrTerminal = _backend.FindTerminal(shell);
        }

        public static void OpenInFileManager(string path, bool select = false)
        {
            _backend.OpenInFileManager(path, select);
        }

        public static void OpenBrowser(string url)
        {
            _backend.OpenBrowser(url);
        }

        public static void OpenTerminal(string workdir)
        {
            if (string.IsNullOrEmpty(ShellOrTerminal))
                App.RaiseException(workdir, $"Terminal is not specified! Please confirm that the correct shell/terminal has been configured.");
            else
                _backend.OpenTerminal(workdir);
        }

        public static void OpenWithDefaultEditor(string file)
        {
            _backend.OpenWithDefaultEditor(file);
        }

        private static IBackend _backend = null;
    }
}
