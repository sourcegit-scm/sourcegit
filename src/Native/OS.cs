using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Controls;

namespace SourceGit.Native
{
    public static partial class OS
    {
        public interface IBackend
        {
            void SetupApp(AppBuilder builder);
            void SetupWindow(Window window);

            string FindGitExecutable();
            string FindTerminal(Models.ShellOrTerminal shell);
            List<Models.ExternalTool> FindExternalTools();

            void OpenTerminal(string workdir);
            void OpenInFileManager(string path, bool select);
            void OpenBrowser(string url);
            void OpenWithDefaultEditor(string file);
        }

        public static string DataDir
        {
            get;
            private set;
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

        public static string CredentialHelper
        {
            get;
            set;
        } = "manager";

        public static string ShellOrTerminal
        {
            get;
            set;
        } = string.Empty;

        public static List<Models.ExternalTool> ExternalTools
        {
            get;
            set;
        } = [];

        public static int ExternalMergerType
        {
            get;
            set;
        } = 0;

        public static string ExternalMergerExecFile
        {
            get;
            set;
        } = string.Empty;

        public static bool UseSystemWindowFrame
        {
            get => OperatingSystem.IsLinux() && _enableSystemWindowFrame;
            set => _enableSystemWindowFrame = value;
        }

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
                throw new PlatformNotSupportedException();
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
                var portableDir = Path.Combine(Path.GetDirectoryName(execFile)!, "data");
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

        public static void SetupExternalTools()
        {
            ExternalTools = _backend.FindExternalTools();
        }

        public static void SetupForWindow(Window window)
        {
            _backend.SetupWindow(window);
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

        public static Models.DiffMergeTool GetDiffMergeTool(bool onlyDiff)
        {
            if (ExternalMergerType < 0 || ExternalMergerType >= Models.ExternalMerger.Supported.Count)
                return null;

            if (ExternalMergerType != 0 && (string.IsNullOrEmpty(ExternalMergerExecFile) || !File.Exists(ExternalMergerExecFile)))
                return null;

            var tool = Models.ExternalMerger.Supported[ExternalMergerType];
            return new Models.DiffMergeTool(ExternalMergerExecFile, onlyDiff ? tool.DiffCmd : tool.MergeCmd);
        }

        public static void AutoSelectExternalMergeToolExecFile()
        {
            if (ExternalMergerType >= 0 && ExternalMergerType < Models.ExternalMerger.Supported.Count)
            {
                var merger = Models.ExternalMerger.Supported[ExternalMergerType];
                var externalTool = ExternalTools.Find(x => x.Name.Equals(merger.Name, StringComparison.Ordinal));
                if (externalTool != null)
                    ExternalMergerExecFile = externalTool.ExecFile;
                else if (!OperatingSystem.IsWindows() && File.Exists(merger.Finder))
                    ExternalMergerExecFile = merger.Finder;
                else
                    ExternalMergerExecFile = string.Empty;
            }
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
                App.RaiseException(workdir, "Terminal is not specified! Please confirm that the correct shell/terminal has been configured.");
            else
                _backend.OpenTerminal(workdir);
        }

        public static void OpenWithDefaultEditor(string file)
        {
            _backend.OpenWithDefaultEditor(file);
        }

        public static string GetAbsPath(string root, string sub)
        {
            var fullpath = Path.Combine(root, sub);
            if (OperatingSystem.IsWindows())
                return fullpath.Replace('/', '\\');

            return fullpath;
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

            try
            {
                using var proc = Process.Start(start)!;
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
        }

        [GeneratedRegex(@"^git version[\s\w]*(\d+)\.(\d+)[\.\-](\d+).*$")]
        private static partial Regex REG_GIT_VERSION();

        private static IBackend _backend = null;
        private static string _gitExecutable = string.Empty;
        private static bool _enableSystemWindowFrame = false;
    }
}
