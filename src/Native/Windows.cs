using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

using Avalonia;
using Avalonia.Controls;

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

        [DllImport("ntdll")]
        private static extern int RtlGetVersion(ref RTL_OSVERSIONINFOEX lpVersionInformation);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern bool PathFindOnPath([In, Out] StringBuilder pszFile, [In] string[] ppszOtherDirs);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern IntPtr ILCreateFromPathW(string pszPath);

        [DllImport("shell32.dll", SetLastError = false)]
        private static extern void ILFree(IntPtr pidl);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, int cild, IntPtr apidl, int dwFlags);

        public void SetupApp(AppBuilder builder)
        {
            // Fix drop shadow issue on Windows 10
            RTL_OSVERSIONINFOEX v = new RTL_OSVERSIONINFOEX();
            v.dwOSVersionInfoSize = (uint)Marshal.SizeOf<RTL_OSVERSIONINFOEX>();
            if (RtlGetVersion(ref v) == 0 && (v.dwMajorVersion < 10 || v.dwBuildNumber < 22000))
            {
                Window.WindowStateProperty.Changed.AddClassHandler<Window>((w, _) => FixWindowFrameOnWin10(w));
                Control.LoadedEvent.AddClassHandler<Window>((w, _) => FixWindowFrameOnWin10(w));
            }
        }

        public string FindGitExecutable()
        {
            var reg = Microsoft.Win32.RegistryKey.OpenBaseKey(
                Microsoft.Win32.RegistryHive.LocalMachine,
                Microsoft.Win32.RegistryView.Registry64);

            var git = reg.OpenSubKey("SOFTWARE\\GitForWindows");
            if (git != null && git.GetValue("InstallPath") is string installPath)
            {
                return Path.Combine(installPath, "bin", "git.exe");
            }

            var builder = new StringBuilder("git.exe", 259);
            if (!PathFindOnPath(builder, null))
            {
                return null;
            }

            var exePath = builder.ToString();
            if (!string.IsNullOrEmpty(exePath))
            {
                return exePath;
            }

            return null;
        }

        public string FindTerminal(Models.ShellOrTerminal shell)
        {
            switch (shell.Type)
            {
                case "git-bash":
                    if (string.IsNullOrEmpty(OS.GitExecutable))
                        break;

                    var binDir = Path.GetDirectoryName(OS.GitExecutable)!;
                    var bash = Path.Combine(binDir, "bash.exe");
                    if (!File.Exists(bash))
                        break;

                    return bash;
                case "pwsh":
                    var localMachine = Microsoft.Win32.RegistryKey.OpenBaseKey(
                            Microsoft.Win32.RegistryHive.LocalMachine,
                            Microsoft.Win32.RegistryView.Registry64);

                    var pwsh = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\pwsh.exe");
                    if (pwsh != null)
                    {
                        var path = pwsh.GetValue(null) as string;
                        if (File.Exists(path))
                            return path;
                    }

                    var pwshFinder = new StringBuilder("powershell.exe", 512);
                    if (PathFindOnPath(pwshFinder, null))
                        return pwshFinder.ToString();

                    break;
                case "cmd":
                    return "C:\\Windows\\System32\\cmd.exe";
                case "wt":
                    var wtFinder = new StringBuilder("wt.exe", 512);
                    if (PathFindOnPath(wtFinder, null))
                        return wtFinder.ToString();

                    break;
            }

            return string.Empty;
        }

        public List<Models.ExternalTool> FindExternalTools()
        {
            var finder = new Models.ExternalToolsFinder();
            finder.VSCode(FindVSCode);
            finder.VSCodeInsiders(FindVSCodeInsiders);
            finder.VSCodium(FindVSCodium);
            finder.Fleet(() => $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\Programs\\Fleet\\Fleet.exe");
            finder.FindJetBrainsFromToolbox(() => $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\JetBrains\\Toolbox");
            finder.SublimeText(FindSublimeText);
            return finder.Founded;
        }

        public void OpenBrowser(string url)
        {
            var info = new ProcessStartInfo("cmd", $"/c start {url}");
            info.CreateNoWindow = true;
            Process.Start(info);
        }

        public void OpenTerminal(string workdir)
        {
            if (!File.Exists(OS.ShellOrTerminal))
            {
                App.RaiseException(workdir, $"Terminal is not specified! Please confirm that the correct shell/terminal has been configured.");
                return;
            }

            var startInfo = new ProcessStartInfo();
            startInfo.WorkingDirectory = workdir;
            startInfo.FileName = OS.ShellOrTerminal;

            // Directly launching `Windows Terminal` need to specify the `-d` parameter
            if (OS.ShellOrTerminal.EndsWith("wt.exe", StringComparison.OrdinalIgnoreCase))
                startInfo.Arguments = $"-d \"{workdir}\"";

            Process.Start(startInfo);
        }

        public void OpenInFileManager(string path, bool select)
        {
            string fullpath;
            if (File.Exists(path))
            {
                fullpath = new FileInfo(path).FullName;
                select = true;
            }
            else
            {
                fullpath = new DirectoryInfo(path!).FullName;
                fullpath += Path.DirectorySeparatorChar;
            }

            if (select)
            {
                OpenFolderAndSelectFile(fullpath);
            }
            else
            {
                Process.Start(new ProcessStartInfo(fullpath)
                {
                    UseShellExecute = true,
                    CreateNoWindow = true,
                });
            }
        }

        public void OpenWithDefaultEditor(string file)
        {
            var info = new FileInfo(file);
            var start = new ProcessStartInfo("cmd", $"/c start \"\" \"{info.FullName}\"");
            start.CreateNoWindow = true;
            Process.Start(start);
        }

        private void FixWindowFrameOnWin10(Window w)
        {
            if (w.WindowState != WindowState.Normal)
                w.SystemDecorations = SystemDecorations.Full;                
            else
                w.SystemDecorations = SystemDecorations.BorderOnly;
        }

        #region EXTERNAL_EDITOR_FINDER
        private string FindVSCode()
        {
            var localMachine = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine,
                    Microsoft.Win32.RegistryView.Registry64);

            // VSCode (system)
            var systemVScode = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{EA457B21-F73E-494C-ACAB-524FDE069978}_is1");
            if (systemVScode != null)
            {
                return systemVScode.GetValue("DisplayIcon") as string;
            }

            var currentUser = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.CurrentUser,
                    Microsoft.Win32.RegistryView.Registry64);

            // VSCode (user)
            var vscode = currentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{771FD6B0-FA20-440A-A002-3B3BAC16DC50}_is1");
            if (vscode != null)
            {
                return vscode.GetValue("DisplayIcon") as string;
            }

            return string.Empty;
        }

        private string FindVSCodeInsiders()
        {
            var localMachine = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine,
                    Microsoft.Win32.RegistryView.Registry64);

            // VSCode - Insiders (system)
            var systemVScodeInsiders = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{1287CAD5-7C8D-410D-88B9-0D1EE4A83FF2}_is1");
            if (systemVScodeInsiders != null)
            {
                return systemVScodeInsiders.GetValue("DisplayIcon") as string;
            }

            var currentUser = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.CurrentUser,
                    Microsoft.Win32.RegistryView.Registry64);

            // VSCode - Insiders (user)
            var vscodeInsiders = currentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{217B4C08-948D-4276-BFBB-BEE930AE5A2C}_is1");
            if (vscodeInsiders != null)
            {
                return vscodeInsiders.GetValue("DisplayIcon") as string;
            }

            return string.Empty;
        }

        private string FindVSCodium()
        {
            var localMachine = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine,
                    Microsoft.Win32.RegistryView.Registry64);

            // VSCodium (system)
            var systemVScodium = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{88DA3577-054F-4CA1-8122-7D820494CFFB}_is1");
            if (systemVScodium != null)
            {
                return systemVScodium.GetValue("DisplayIcon") as string;
            }

            var currentUser = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.CurrentUser,
                    Microsoft.Win32.RegistryView.Registry64);

            // VSCodium (user)
            var vscodium = currentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{2E1F05D1-C245-4562-81EE-28188DB6FD17}_is1");
            if (vscodium != null)
            {
                return vscodium.GetValue("DisplayIcon") as string;
            }

            return string.Empty;
        }

        private string FindSublimeText()
        {
            var localMachine = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine,
                    Microsoft.Win32.RegistryView.Registry64);

            // Sublime Text 4
            var sublime = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Sublime Text_is1");
            if (sublime != null)
            {
                var icon = sublime.GetValue("DisplayIcon") as string;
                return Path.Combine(Path.GetDirectoryName(icon)!, "subl.exe");
            }

            // Sublime Text 3
            var sublime3 = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Sublime Text 3_is1");
            if (sublime3 != null)
            {
                var icon = sublime3.GetValue("DisplayIcon") as string;
                return Path.Combine(Path.GetDirectoryName(icon)!, "subl.exe");
            }

            return string.Empty;
        }
        #endregion

        private void OpenFolderAndSelectFile(string folderPath)
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
    }
}
