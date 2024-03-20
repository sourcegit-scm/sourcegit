using System;
using System.Diagnostics;

using Avalonia;

namespace SourceGit.Native
{
    public static class OS
    {
        public interface IBackend
        {
            void SetupApp(AppBuilder builder);

            string FindGitExecutable();
            string FindVSCode();

            void OpenTerminal(string workdir);
            void OpenInFileManager(string path, bool select);
            void OpenBrowser(string url);
            void OpenWithDefaultEditor(string file);
        }

        public static string GitInstallPath
        {
            get;
            set;
        }

        public static string VSCodeExecutableFile
        {
            get;
            set;
        }

        static OS()
        {
            if (OperatingSystem.IsMacOS())
            {
                _backend = new MacOS();
                VSCodeExecutableFile = _backend.FindVSCode();
            }
            else if (OperatingSystem.IsWindows())
            {
                _backend = new Windows();
                VSCodeExecutableFile = _backend.FindVSCode();
            }
            else if (OperatingSystem.IsLinux())
            {
                _backend = new Linux();
                VSCodeExecutableFile = _backend.FindVSCode();
            }
            else
            {
                throw new Exception("Platform unsupported!!!");
            }
        }

        public static void SetupApp(AppBuilder builder)
        {
            _backend?.SetupApp(builder);
        }

        public static string FindGitExecutable()
        {
            return _backend?.FindGitExecutable();
        }

        public static void OpenInFileManager(string path, bool select = false)
        {
            _backend?.OpenInFileManager(path, select);
        }

        public static void OpenBrowser(string url)
        {
            _backend?.OpenBrowser(url);
        }

        public static void OpenTerminal(string workdir)
        {
            _backend?.OpenTerminal(workdir);
        }

        public static void OpenWithDefaultEditor(string file)
        {
            _backend?.OpenWithDefaultEditor(file);
        }

        public static void OpenInVSCode(string repo)
        {
            if (string.IsNullOrEmpty(VSCodeExecutableFile))
            {
                App.RaiseException(repo, "Visual Studio Code can NOT be found in your system!!!");
                return;
            }

            Process.Start(new ProcessStartInfo()
            {
                WorkingDirectory = repo,
                FileName = VSCodeExecutableFile,
                Arguments = $"\"{repo}\"",
                UseShellExecute = false,
            });
        }

        private static readonly IBackend _backend = null;
    }
}