using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using Avalonia;

namespace SourceGit.Native
{
    public static partial class OS
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

            bool EnsureSingleInstance();
        }

        public static string DataDir {
            get;
            private set;
        } = string.Empty;

        public static string CustomPathEnv
        {
            get;
            set;
        } = string.Empty;

        public static string GitExecutable
        {
            get => _gitExecutable;
            set
            {
                if (_gitExecutable != value)
                {
                    _gitExecutable = value;
                    UpdateGitVersion();
                }
            }
        }

        public static string GitVersionString
        {
            get;
            private set;
        } = string.Empty;

        public static Version GitVersion
        {
            get;
            private set;
        } = new Version(0, 0, 0);

        public static string ShellOrTerminal {
            get;
            set;
        } = string.Empty;

        public static List<Models.ExternalTool> ExternalTools {
            get;
            set;
        } = [];

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
            if (OperatingSystem.IsWindows())
            {
                var execFile = Process.GetCurrentProcess().MainModule!.FileName;
                var portableDir = Path.Combine(Path.GetDirectoryName(execFile), "data");
                if (Directory.Exists(portableDir))
                {
                    DataDir = portableDir;
                    return;
                }
            }

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

        private static void UpdateGitVersion()
        {
            if (string.IsNullOrEmpty(_gitExecutable) || !File.Exists(_gitExecutable))
            {
                GitVersionString = string.Empty;
                GitVersion = new Version(0, 0, 0);
                return;
            }

            var start = new ProcessStartInfo();
            start.FileName = _gitExecutable;
            start.Arguments = "--version";
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;

            var proc = new Process() { StartInfo = start };
            try
            {
                proc.Start();

                var rs = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(rs))
                {
                    GitVersionString = rs.Trim();

                    var match = REG_GIT_VERSION().Match(GitVersionString);
                    if (match.Success)
                    {
                        var major = int.Parse(match.Groups[1].Value);
                        var minor = int.Parse(match.Groups[2].Value);
                        var build = int.Parse(match.Groups[3].Value);
                        GitVersion = new Version(major, minor, build);
                        GitVersionString = GitVersionString.Substring(11).Trim();
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            proc.Close();
        }

        [GeneratedRegex(@"^git version[\s\w]*(\d+)\.(\d+)[\.\-](\d+).*$")]
        private static partial Regex REG_GIT_VERSION();

        public static bool EnsureSingleInstance()
        {
            return _backend.EnsureSingleInstance();
        }

        private static IBackend _backend = null;
        private static string _gitExecutable = string.Empty;
    }
}
