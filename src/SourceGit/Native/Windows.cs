using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Native
{
    [SupportedOSPlatform("windows")]
    internal class Windows : OS.IBackend
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct RTL_OSVERSIONINFOEX
        {
            internal uint dwOSVersionInfoSize;
            internal uint dwMajorVersion;
            internal uint dwMinorVersion;
            internal uint dwBuildNumber;
            internal uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            internal string szCSDVersion;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern bool PathFindOnPath([In, Out] StringBuilder pszFile, [In] string[] ppszOtherDirs);

        [DllImport("ntdll")]
        private static extern int RtlGetVersion(ref RTL_OSVERSIONINFOEX lpVersionInformation);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern IntPtr ILCreateFromPathW(string pszPath);

        [DllImport("shell32.dll", SetLastError = false)]
        private static extern void ILFree(IntPtr pidl);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, int cild, IntPtr apidl, int dwFlags);

        public void SetupApp(AppBuilder builder)
        {
            builder.With(new FontManagerOptions()
            {
                DefaultFamilyName = "Microsoft YaHei UI",
                FontFallbacks = [new FontFallback { FontFamily = "Microsoft YaHei" }],
            });

            // Fix drop shadow issue on Windows 10
            RTL_OSVERSIONINFOEX v = new RTL_OSVERSIONINFOEX();
            v.dwOSVersionInfoSize = (uint)Marshal.SizeOf<RTL_OSVERSIONINFOEX>();
            if (RtlGetVersion(ref v) == 0 && (v.dwMajorVersion < 10 || v.dwBuildNumber < 22000))
            {
                Window.WindowStateProperty.Changed.AddClassHandler<Window>((w, e) =>
                {
                    if (w.WindowState != WindowState.Maximized)
                    {
                        var margins = new MARGINS { cxLeftWidth = 1, cxRightWidth = 1, cyTopHeight = 1, cyBottomHeight = 1 };
                        DwmExtendFrameIntoClientArea(w.TryGetPlatformHandle().Handle, ref margins);
                    }
                });

                Window.LoadedEvent.AddClassHandler<Window>((w, e) =>
                {
                    if (w.WindowState != WindowState.Maximized)
                    {
                        var margins = new MARGINS { cxLeftWidth = 1, cxRightWidth = 1, cyTopHeight = 1, cyBottomHeight = 1 };
                        DwmExtendFrameIntoClientArea(w.TryGetPlatformHandle().Handle, ref margins);
                    }
                });
            }
        }

        public string FindGitExecutable()
        {
            var reg = Microsoft.Win32.RegistryKey.OpenBaseKey(
                Microsoft.Win32.RegistryHive.LocalMachine,
                Microsoft.Win32.RegistryView.Registry64);

            var git = reg.OpenSubKey("SOFTWARE\\GitForWindows");
            if (git != null)
            {
                return Path.Combine(git.GetValue("InstallPath") as string, "bin", "git.exe");
            }

            var builder = new StringBuilder("git.exe", 259);
            if (!PathFindOnPath(builder, null))
            {
                return null;
            }

            var exePath = builder.ToString();
            if (string.IsNullOrEmpty(exePath)) return null;

            return exePath;
        }

        public string FindVSCode()
        {
            var root = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine,
                    Environment.Is64BitOperatingSystem ? Microsoft.Win32.RegistryView.Registry64 : Microsoft.Win32.RegistryView.Registry32);

            var vscode = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{EA457B21-F73E-494C-ACAB-524FDE069978}_is1");
            if (vscode != null)
            {
                return vscode.GetValue("DisplayIcon") as string;
            }

            var toolPath = Environment.ExpandEnvironmentVariables($"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe");
            if (File.Exists(toolPath)) return toolPath;
            return string.Empty;
        }

        public string FindFleet()
        {
            var toolPath = Environment.ExpandEnvironmentVariables($"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\\AppData\\Local\\Programs\\Fleet\\Fleet.exe");
            if (File.Exists(toolPath)) return toolPath;
            return string.Empty;
        }

        public void OpenBrowser(string url)
        {
            var info = new ProcessStartInfo("cmd", $"/c start {url}");
            info.CreateNoWindow = true;
            Process.Start(info);
        }

        public void OpenTerminal(string workdir)
        {
            var bash = Path.Combine(Path.GetDirectoryName(OS.GitInstallPath), "bash.exe");
            if (!File.Exists(bash))
            {
                App.RaiseException(string.IsNullOrEmpty(workdir) ? "" : workdir, $"Can NOT found bash.exe under '{Path.GetDirectoryName(OS.GitInstallPath)}'");
                return;
            }

            var startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.FileName = bash;
            if (!string.IsNullOrEmpty(workdir) && Path.Exists(workdir)) startInfo.WorkingDirectory = workdir;
            Process.Start(startInfo);
        }

        public void OpenInFileManager(string path, bool select)
        {
            var fullpath = string.Empty;
            if (File.Exists(path))
            {
                fullpath = new FileInfo(path).FullName;

                // For security reason, we never execute a file.
                // Instead, we open the folder and select it.
                select = true;
            }
            else
            {
                fullpath = new DirectoryInfo(path).FullName;
            }

            if (select)
            {
                // The fullpath here may be a file or a folder.
                OpenFolderAndSelectFile(fullpath);
            }
            else
            {
                // The fullpath here is always a folder.
                Process.Start(new ProcessStartInfo(fullpath)
                {
                    UseShellExecute = true,
                    CreateNoWindow = true,
                });
            }
        }

        private static void OpenFolderAndSelectFile(string folderPath)
        {
            var pidl = ILCreateFromPathW(folderPath);

            try
            {
                SHOpenFolderAndSelectItems(pidl, 0, 0, 0);
            }
            finally
            {
                ILFree(pidl);
            }
        }

        public void OpenWithDefaultEditor(string file)
        {
            var info = new FileInfo(file);
            var start = new ProcessStartInfo("cmd", $"/c start {info.FullName}");
            start.CreateNoWindow = true;
            Process.Start(start);
        }
    }
}