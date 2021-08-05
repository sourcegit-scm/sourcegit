using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SourceGit.Views.Controls {

    [SuppressUnmanagedCodeSecurity]
    internal delegate Int32 BrowseCallbackProc(IntPtr hwnd, Int32 msg, IntPtr lParam, IntPtr lpData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    [SuppressUnmanagedCodeSecurity]
    internal class BrowseInfo {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public IntPtr pszDisplayName;
        public String lpszTitle;
        public Int32 ulFlags;
        public BrowseCallbackProc lpfn;
        public IntPtr lParam;
        public Int32 iImage;
    }

    /// <summary>
    ///     Win32 API封装（user32.dll)
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    internal static class User32 {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(HandleRef hWnd, Int32 msg, Int32 wParam, String lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(HandleRef hWnd, Int32 msg, Int32 wParam, Int32 lParam);
    }

    /// <summary>
    ///     Win32 API封装（ole32.dll)
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    internal static class Ole32 {
        [DllImport("ole32.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true)]
        internal static extern void CoTaskMemFree(IntPtr pv);
    }

    /// <summary>
    ///     Win32 API封装（shell32.dll)
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    internal static class Shell32 {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern Boolean SHGetPathFromIDList(IntPtr pidl, IntPtr pszPath);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHBrowseForFolder([In] BrowseInfo lpbi);
    }

    /// <summary>
    ///     调用WindowsAPI打开对话目录对话框
    /// </summary>
    public class FolderDialog : Microsoft.Win32.CommonDialog {
        /// <summary>
        ///     选中的目录
        /// </summary>
        public string SelectedPath { get; private set; } = string.Empty;

        public override void Reset() {
            SelectedPath = string.Empty;
        }

        protected override bool RunDialog(IntPtr hwndOwner) {
            BrowseCallbackProc callback = new BrowseCallbackProc(BrowseCallbackHandler);
            bool ok = false;
            try {
                var info = new BrowseInfo();
                info.pidlRoot = IntPtr.Zero;
                info.hwndOwner = hwndOwner;
                info.pszDisplayName = IntPtr.Zero;
                info.lpszTitle = null;
                info.ulFlags = 0x0153;
                info.lpfn = callback;
                info.lParam = IntPtr.Zero;
                info.iImage = 0;

                IntPtr result = Shell32.SHBrowseForFolder(info);
                if (result != IntPtr.Zero) {
                    IntPtr pathPtr = Marshal.AllocHGlobal(260 * Marshal.SystemDefaultCharSize);
                    Shell32.SHGetPathFromIDList(result, pathPtr);

                    if (pathPtr != IntPtr.Zero) {
                        SelectedPath = Marshal.PtrToStringAuto(pathPtr);
                        ok = true;
                        Marshal.FreeHGlobal(pathPtr);
                    }

                    Ole32.CoTaskMemFree(result);
                }
            } finally {
                callback = null;
            }

            return ok;
        }

        private Int32 BrowseCallbackHandler(IntPtr hwnd, Int32 msg, IntPtr lParam, IntPtr lpData) {
            switch (msg) {
            case 1:
                if (!string.IsNullOrEmpty(SelectedPath)) {
                    Int32 flag = Marshal.SystemDefaultCharSize == 1 ? 1126 : 1127;
                    User32.SendMessage(new HandleRef(null, hwnd), flag, 1, SelectedPath);
                }
                break;
            case 2:
                if (lParam != IntPtr.Zero) {
                    IntPtr pathPtr = Marshal.AllocHGlobal(260 * Marshal.SystemDefaultCharSize);
                    bool flag = Shell32.SHGetPathFromIDList(lParam, pathPtr);
                    Marshal.FreeHGlobal(pathPtr);
                    User32.SendMessage(new HandleRef(null, hwnd), 1125, 0, flag ? 1 : 0);
                }
                break;
            }

            return 0;
        }
    }
}
