using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace SourceGit.Native
{
    [SupportedOSPlatform("windows")]
    internal class Windows : OS.IBackend
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern bool PathFindOnPath([In, Out] StringBuilder pszFile, [In] string[] ppszOtherDirs);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern IntPtr ILCreateFromPathW(string pszPath);

        [DllImport("shell32.dll", SetLastError = false)]
        private static extern void ILFree(IntPtr pidl);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, int cild, IntPtr apidl, int dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        public void SetupApp(AppBuilder builder)
        {
            // Fix drop shadow issue on Windows 10
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                Window.WindowStateProperty.Changed.AddClassHandler<Window>((w, _) => FixWindowFrameOnWin10(w));
                Control.LoadedEvent.AddClassHandler<Window>((w, _) => FixWindowFrameOnWin10(w));
            }
        }

        public void SetupWindow(Window window)
        {
            window.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
            window.ExtendClientAreaToDecorationsHint = true;
            window.Classes.Add("fix_maximized_padding");

            Win32Properties.AddWndProcHookCallback(window, (IntPtr hWnd, uint msg, IntPtr _, IntPtr lParam, ref bool handled) =>
            {
                // Custom WM_NCHITTEST
                if (msg == 0x0084)
                {
                    handled = true;

                    if (window.WindowState == WindowState.FullScreen || window.WindowState == WindowState.Maximized)
                        return 1; // HTCLIENT

                    var p = IntPtrToPixelPoint(lParam);
                    GetWindowRect(hWnd, out var rcWindow);

                    var borderThickness = (int)(4 * window.RenderScaling);
                    int y = 1;
                    int x = 1;
                    if (p.X >= rcWindow.left && p.X < rcWindow.left + borderThickness)
                        x = 0;
                    else if (p.X < rcWindow.right && p.X >= rcWindow.right - borderThickness)
                        x = 2;

                    if (p.Y >= rcWindow.top && p.Y < rcWindow.top + borderThickness)
                        y = 0;
                    else if (p.Y < rcWindow.bottom && p.Y >= rcWindow.bottom - borderThickness)
                        y = 2;

                    var zone = y * 3 + x;
                    return zone switch
                    {
                        0 => 13, // HTTOPLEFT
                        1 => 12, // HTTOP
                        2 => 14, // HTTOPRIGHT
                        3 => 10, // HTLEFT
                        4 => 1, // HTCLIENT
                        5 => 11, // HTRIGHT
                        6 => 16, // HTBOTTOMLEFT
                        7 => 15, // HTBOTTOM
                        _ => 17,
                    };
                }

                return IntPtr.Zero;
            });
        }

        public string FindGitExecutable()
        {
            var reg = Microsoft.Win32.RegistryKey.OpenBaseKey(
                Microsoft.Win32.RegistryHive.LocalMachine,
                Microsoft.Win32.RegistryView.Registry64);

            var git = reg.OpenSubKey(@"SOFTWARE\GitForWindows");
            if (git?.GetValue("InstallPath") is string installPath)
                return Path.Combine(installPath, "bin", "git.exe");

            var builder = new StringBuilder("git.exe", 259);
            if (!PathFindOnPath(builder, null))
                return null;

            var exePath = builder.ToString();
            if (!string.IsNullOrEmpty(exePath))
                return exePath;

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
                    var bash = Path.GetFullPath(Path.Combine(binDir, "..", "git-bash.exe"));
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
                    return @"C:\Windows\System32\cmd.exe";
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
            var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var finder = new Models.ExternalToolsFinder();
            finder.VSCode(FindVSCode);
            finder.VSCodeInsiders(FindVSCodeInsiders);
            finder.VSCodium(FindVSCodium);
            finder.Cursor(() => Path.Combine(localAppDataDir, @"Programs\Cursor\Cursor.exe"));
            finder.Fleet(() => Path.Combine(localAppDataDir, @"Programs\Fleet\Fleet.exe"));
            finder.FindJetBrainsFromToolbox(() => Path.Combine(localAppDataDir, @"JetBrains\Toolbox"));
            finder.SublimeText(FindSublimeText);
            finder.Zed(FindZed);
            FindVisualStudio(finder);
            return finder.Tools;
        }

        public void OpenBrowser(string url)
        {
            var info = new ProcessStartInfo("cmd", $"""/c start "" {url.Quoted()}""");
            info.CreateNoWindow = true;
            Process.Start(info);
        }

        public void OpenTerminal(string workdir)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var cwd = string.IsNullOrEmpty(workdir) ? home : workdir;
            var terminal = OS.ShellOrTerminal;

            if (!File.Exists(terminal))
            {
                App.RaiseException(workdir, "Terminal is not specified! Please confirm that the correct shell/terminal has been configured.");
                return;
            }

            var startInfo = new ProcessStartInfo();
            startInfo.WorkingDirectory = cwd;
            startInfo.FileName = terminal;

            // Directly launching `Windows Terminal` need to specify the `-d` parameter
            if (terminal.EndsWith("wt.exe", StringComparison.OrdinalIgnoreCase))
                startInfo.Arguments = $"-d {cwd.Quoted()}";

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
            var start = new ProcessStartInfo("cmd", $"""/c start "" {info.FullName.Quoted()}""");
            start.CreateNoWindow = true;
            Process.Start(start);
        }

        private void FixWindowFrameOnWin10(Window w)
        {
            // Schedule the DWM frame extension to run in the next render frame
            // to ensure proper timing with the window initialization sequence
            Dispatcher.UIThread.Post(() =>
            {
                var platformHandle = w.TryGetPlatformHandle();
                if (platformHandle == null)
                    return;

                var margins = new MARGINS { cxLeftWidth = 1, cxRightWidth = 1, cyTopHeight = 1, cyBottomHeight = 1 };
                DwmExtendFrameIntoClientArea(platformHandle.Handle, ref margins);
            }, DispatcherPriority.Render);
        }

        private PixelPoint IntPtrToPixelPoint(IntPtr param)
        {
            var v = IntPtr.Size == 4 ? param.ToInt32() : (int)(param.ToInt64() & 0xFFFFFFFF);
            return new PixelPoint((short)(v & 0xffff), (short)(v >> 16));
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
                return systemVScode.GetValue("DisplayIcon") as string;

            var currentUser = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.CurrentUser,
                    Microsoft.Win32.RegistryView.Registry64);

            // VSCode (user)
            var vscode = currentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{771FD6B0-FA20-440A-A002-3B3BAC16DC50}_is1");
            if (vscode != null)
                return vscode.GetValue("DisplayIcon") as string;

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
                return systemVScodeInsiders.GetValue("DisplayIcon") as string;

            var currentUser = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.CurrentUser,
                    Microsoft.Win32.RegistryView.Registry64);

            // VSCode - Insiders (user)
            var vscodeInsiders = currentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{217B4C08-948D-4276-BFBB-BEE930AE5A2C}_is1");
            if (vscodeInsiders != null)
                return vscodeInsiders.GetValue("DisplayIcon") as string;

            return string.Empty;
        }

        private string FindVSCodium()
        {
            var localMachine = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine,
                    Microsoft.Win32.RegistryView.Registry64);

            // VSCodium (system)
            var systemVSCodium = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{88DA3577-054F-4CA1-8122-7D820494CFFB}_is1");
            if (systemVSCodium != null)
                return systemVSCodium.GetValue("DisplayIcon") as string;

            var currentUser = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.CurrentUser,
                    Microsoft.Win32.RegistryView.Registry64);

            // VSCodium (user)
            var vscodium = currentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{2E1F05D1-C245-4562-81EE-28188DB6FD17}_is1");
            if (vscodium != null)
                return vscodium.GetValue("DisplayIcon") as string;

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

        private void FindVisualStudio(Models.ExternalToolsFinder finder)
        {
            var vswhere = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe");
            if (!File.Exists(vswhere))
                return;

            var startInfo = new ProcessStartInfo();
            startInfo.FileName = vswhere;
            startInfo.Arguments = "-format json -prerelease -utf8";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardOutput = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;

            try
            {
                using var proc = Process.Start(startInfo)!;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                if (proc.ExitCode == 0)
                {
                    var instances = JsonSerializer.Deserialize(output, JsonCodeGen.Default.ListVisualStudioInstance);
                    foreach (var instance in instances)
                    {
                        var exec = instance.ProductPath;
                        var icon = instance.IsPrerelease ? "vs-preview" : "vs";
                        finder.TryAdd(instance.DisplayName, icon, () => exec, GenerateCommandlineArgsForVisualStudio);
                    }
                }
            }
            catch
            {
                // Just ignore.
            }
        }

        private string FindZed()
        {
            var currentUser = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.CurrentUser,
                    Microsoft.Win32.RegistryView.Registry64);

            // NOTE: this is the official Zed Preview reg data.
            var preview = currentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{F70E4811-D0E2-4D88-AC99-D63752799F95}_is1");
            if (preview != null)
                return preview.GetValue("DisplayIcon") as string;

            var findInPath = new StringBuilder("zed.exe", 512);
            if (PathFindOnPath(findInPath, null))
                return findInPath.ToString();

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

        private string GenerateCommandlineArgsForVisualStudio(string repo)
        {
            var sln = FindVSSolutionFile(new DirectoryInfo(repo), 4);
            return string.IsNullOrEmpty(sln) ? repo.Quoted() : sln.Quoted();
        }

        private string FindVSSolutionFile(DirectoryInfo dir, int leftDepth)
        {
            var files = dir.GetFiles();
            foreach (var f in files)
            {
                if (f.Name.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                    f.Name.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                    return f.FullName;
            }

            if (leftDepth <= 0)
                return null;

            var subDirs = dir.GetDirectories();
            foreach (var subDir in subDirs)
            {
                var first = FindVSSolutionFile(subDir, leftDepth - 1);
                if (!string.IsNullOrEmpty(first))
                    return first;
            }

            return null;
        }
    }
}
