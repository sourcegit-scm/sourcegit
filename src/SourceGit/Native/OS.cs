using System;
using System.Diagnostics;

using Avalonia;

// ReSharper disable InconsistentNaming

namespace SourceGit.Native
{
    public static class OS
    {
        public interface IBackend
        {
            void SetupApp(AppBuilder builder);

            string FindGitExecutable();
            string FindVSCode();
            string FindFleet();

            void OpenTerminal(string workdir);
            void OpenInFileManager(string path, bool select);
            void OpenBrowser(string url);
            void OpenWithDefaultEditor(string file);
        }

        public static string GitInstallPath { get; set; }

        public static string VSCodeExecutableFile { get; set; }

        public static string FleetExecutableFile { get; set; }

        public enum Platforms
        {
            Unknown = 0,
            Windows = 1,
            MacOS = 2,
            Linux
        }

        public static Platforms Platform => OperatingSystem.IsWindows() ? Platforms.Windows : OperatingSystem.IsMacOS() ? Platforms.MacOS : OperatingSystem.IsLinux() ? Platforms.Linux : Platforms.Unknown;

        static OS()
        {
            _backend = Platform switch
            {
#pragma warning disable CA1416
                Platforms.Windows => new Windows(),
                Platforms.MacOS => new MacOS(),
                Platforms.Linux => new Linux(),
#pragma warning restore CA1416
                _ => throw new Exception("Platform unsupported!!!")
            };

            VSCodeExecutableFile = _backend.FindVSCode();
            FleetExecutableFile = _backend.FindFleet();
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
                WorkingDirectory = repo, FileName = VSCodeExecutableFile, Arguments = $"\"{repo}\"", UseShellExecute = false,
            });
        }

        private static readonly IBackend _backend = null;

        public static void OpenInFleet(string repo)
        {
            if (string.IsNullOrEmpty(FleetExecutableFile))
            {
                App.RaiseException(repo, "Fleet can NOT be found in your system!!!");
                return;
            }

            Process.Start(new ProcessStartInfo()
            {
                WorkingDirectory = repo, FileName = FleetExecutableFile, Arguments = $"\"{repo}\"", UseShellExecute = false,
            });
        }
    }
}