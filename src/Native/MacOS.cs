using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
            window.BorderThickness = new Thickness(0);
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

    [SupportedOSPlatform("macOS")]
    public static class MacOSUtilities
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct CGPoint
        {
            public double X;
            public double Y;

            public CGPoint(double x, double y)
            {
                X = x;
                Y = y;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(X, Y);
            }

            public override bool Equals([NotNullWhen(true)] object obj)
            {
                if (obj is not CGPoint point)
                    return false;

                return X == point.X && Y == point.Y;
            }

            public static bool operator ==(CGPoint left, CGPoint right) => left.Equals(right);
            public static bool operator !=(CGPoint left, CGPoint right) => !left.Equals(right);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CGSize
        {
            public double Width;
            public double Height;

            public override int GetHashCode()
            {
                return HashCode.Combine(Width, Height);
            }

            public override bool Equals([NotNullWhen(true)] object obj)
            {
                if (obj is not CGSize size)
                    return false;

                return Width == size.Width && Height == size.Height;
            }

            public static bool operator ==(CGSize left, CGSize right) => left.Equals(right);
            public static bool operator !=(CGSize left, CGSize right) => !left.Equals(right);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CGRect
        {
            public CGPoint Origin;
            public CGSize Size;

            public override int GetHashCode()
            {
                return HashCode.Combine(Origin, Size);
            }

            public override bool Equals([NotNullWhen(true)] object obj)
            {
                if (obj is not CGRect rect)
                    return false;

                return Origin == rect.Origin && Size == rect.Size;
            }

            public static bool operator ==(CGRect left, CGRect right) => left.Equals(right);
            public static bool operator !=(CGRect left, CGRect right) => !left.Equals(right);
        }

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
        public static extern IntPtr objc_getClass(string name);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
        public static extern IntPtr sel_registerName(string name);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr objc_msgSend_IntPtr_Int(IntPtr receiver, IntPtr selector, int arg);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern void objc_msgSend_Void_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern void objc_msgSend_Void_Point(IntPtr receiver, IntPtr selector, CGPoint arg);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend_stret")]
        public static extern void objc_msgSendStrect_Rect(out CGRect rect, IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern CGRect objc_msgSend_Rect(IntPtr receiver, IntPtr selector);

        private static readonly IntPtr s_selStandardWindowButton = sel_registerName("standardWindowButton:");
        private static readonly IntPtr s_selFrame = sel_registerName("frame");
        private static readonly IntPtr s_selSetFrameOrigin = sel_registerName("setFrameOrigin:");

        public static void AdjustTrafficLightsForThickTitleBar(Window window)
        {
            IntPtr nsWindow = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (nsWindow == IntPtr.Zero)
                return;

            IntPtr nsCloseBtn = objc_msgSend_IntPtr_Int(nsWindow, s_selStandardWindowButton, 0);
            IntPtr nsMinBtn = objc_msgSend_IntPtr_Int(nsWindow, s_selStandardWindowButton, 1);
            IntPtr nsZoomBtn = objc_msgSend_IntPtr_Int(nsWindow, s_selStandardWindowButton, 2);
            if (nsCloseBtn == IntPtr.Zero || nsMinBtn == IntPtr.Zero || nsZoomBtn == IntPtr.Zero)
                return;

            // For Intel CPU, we need to use `objc_msgSend_stret` to get the `CGRect` struct, while for Apple Silicon, we can directly use `objc_msgSend`.
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                objc_msgSendStrect_Rect(out var frame, nsCloseBtn, s_selFrame);
                if (Math.Abs(frame.Origin.X - 14) <= 0.5 || Math.Abs(frame.Origin.Y - 2) <= 0.5)
                    return;
            }
            else
            {
                CGRect frame = objc_msgSend_Rect(nsCloseBtn, s_selFrame);
                if (Math.Abs(frame.Origin.X - 14) <= 0.5 || Math.Abs(frame.Origin.Y - 2) <= 0.5)
                    return;
            }

            objc_msgSend_Void_Point(nsCloseBtn, s_selSetFrameOrigin, new(14, 2));
            objc_msgSend_Void_Point(nsMinBtn, s_selSetFrameOrigin, new(14 + 20, 2));
            objc_msgSend_Void_Point(nsZoomBtn, s_selSetFrameOrigin, new(14 + 40, 2));
        }

        public static void HideSelf()
        {
            IntPtr nsApplicationClass = objc_getClass("NSApplication");
            IntPtr nsSharedApplicationSelector = sel_registerName("sharedApplication");
            IntPtr nsApp = objc_msgSend_IntPtr(nsApplicationClass, nsSharedApplicationSelector);
            IntPtr nsMethodSelector = sel_registerName("hide:");
            IntPtr nsDelegateSelector = sel_registerName("delegate");
            IntPtr nsDelegate = objc_msgSend_IntPtr(nsApp, nsDelegateSelector);
            objc_msgSend_Void_IntPtr(nsApp, nsMethodSelector, nsDelegate);
        }

        public static void HideOtherApplications()
        {
            IntPtr nsApplicationClass = objc_getClass("NSApplication");
            IntPtr nsSharedApplicationSelector = sel_registerName("sharedApplication");
            IntPtr nsApp = objc_msgSend_IntPtr(nsApplicationClass, nsSharedApplicationSelector);
            IntPtr nsMethodSelector = sel_registerName("hideOtherApplications:");
            IntPtr nsDelegateSelector = sel_registerName("delegate");
            IntPtr nsDelegate = objc_msgSend_IntPtr(nsApp, nsDelegateSelector);
            objc_msgSend_Void_IntPtr(nsApp, nsMethodSelector, nsDelegate);
        }

        public static void ShowAllApplications()
        {
            IntPtr nsApplicationClass = objc_getClass("NSApplication");
            IntPtr nsSharedApplicationSelector = sel_registerName("sharedApplication");
            IntPtr nsApp = objc_msgSend_IntPtr(nsApplicationClass, nsSharedApplicationSelector);
            IntPtr nsMethodSelector = sel_registerName("unhideAllApplications:");
            IntPtr nsDelegateSelector = sel_registerName("delegate");
            IntPtr nsDelegate = objc_msgSend_IntPtr(nsApp, nsDelegateSelector);
            objc_msgSend_Void_IntPtr(nsApp, nsMethodSelector, nsDelegate);
        }
    }
}
