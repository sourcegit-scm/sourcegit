using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace SourceGit.Native
{
    internal static partial class WindowsTerminal
    {
        internal const string LibraryName = "Microsoft.Terminal.Control";

        [StructLayout(LayoutKind.Sequential)]
        internal struct TilSize
        {
            public int X;
            public int Y;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void WriteCallback(nint text);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void ScrollCallback(int viewTop, int viewHeight, int bufferSize);

        internal static bool IsSupported
        {
            get
            {
                if (!OperatingSystem.IsWindows())
                    return false;

                var architecture = RuntimeInformation.ProcessArchitecture;
                return (architecture == Architecture.X64 || architecture == Architecture.Arm64) &&
                       File.Exists(GetLibraryPath());
            }
        }

        internal static string GetLibraryPath()
        {
            var rid = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.Arm64 => "win-arm64",
                _ => string.Empty,
            };

            if (string.IsNullOrEmpty(rid))
                return string.Empty;

            return Path.Combine(
                AppContext.BaseDirectory,
                "native-terminal",
                rid,
                "Microsoft.Terminal.Control.dll");
        }

        internal static void EnsureResolver()
        {
            if (Interlocked.Exchange(ref _resolverRegistered, 1) != 0)
                return;

            NativeLibrary.SetDllImportResolver(
                typeof(WindowsTerminal).Assembly,
                static (name, _, _) =>
                {
                    if (!string.Equals(name, LibraryName, StringComparison.Ordinal))
                        return IntPtr.Zero;

                    var file = GetLibraryPath();
                    if (string.IsNullOrEmpty(file) || !File.Exists(file))
                        return IntPtr.Zero;

                    return NativeLibrary.TryLoad(file, out var handle) ? handle : IntPtr.Zero;
                });
        }

        internal static void AvoidBuggyTSFConsoleFlagsOnce()
        {
            if (Interlocked.Exchange(ref _tsfInitialized, 1) != 0)
                return;

            EnsureResolver();
            AvoidBuggyTSFConsoleFlags();
        }

        internal static void CreateTerminalChecked(nint parent, out nint hwnd, out nint terminal)
        {
            EnsureResolver();
            var hr = CreateTerminal(parent, out hwnd, out terminal);
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);
        }

        internal static TilSize TriggerResizeChecked(nint terminal, int width, int height)
        {
            var hr = TerminalTriggerResize(terminal, width, height, out var dimensions);
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            return dimensions;
        }

        [LibraryImport(LibraryName, EntryPoint = "AvoidBuggyTSFConsoleFlags")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial void AvoidBuggyTSFConsoleFlags();

        [LibraryImport(LibraryName, EntryPoint = "CreateTerminal")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        private static partial int CreateTerminal(nint parent, out nint hwnd, out nint terminal);

        [LibraryImport(LibraryName, EntryPoint = "DestroyTerminal")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial void DestroyTerminal(nint terminal);

        [LibraryImport(LibraryName, EntryPoint = "TerminalSendOutput", StringMarshalling = StringMarshalling.Utf16)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial void TerminalSendOutput(nint terminal, string data);

        [LibraryImport(LibraryName, EntryPoint = "TerminalTriggerResize")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        private static partial int TerminalTriggerResize(nint terminal, int width, int height, out TilSize dimensions);

        [LibraryImport(LibraryName, EntryPoint = "TerminalDpiChanged")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial void TerminalDpiChanged(nint terminal, int dpi);

        [LibraryImport(LibraryName, EntryPoint = "TerminalRegisterScrollCallback")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial void TerminalRegisterScrollCallback(nint terminal, ScrollCallback callback);

        [LibraryImport(LibraryName, EntryPoint = "TerminalRegisterWriteCallback")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial void TerminalRegisterWriteCallback(nint terminal, WriteCallback callback);

        [LibraryImport(LibraryName, EntryPoint = "TerminalUserScroll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial void TerminalUserScroll(nint terminal, int viewTop);

        [LibraryImport(LibraryName, EntryPoint = "TerminalGetSelection")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial nint TerminalGetSelection(nint terminal);

        [LibraryImport(LibraryName, EntryPoint = "TerminalIsSelectionActive")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial byte TerminalIsSelectionActive(nint terminal);

        [LibraryImport(LibraryName, EntryPoint = "TerminalSendKeyEvent")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial void TerminalSendKeyEvent(
            nint terminal,
            ushort vkey,
            ushort scanCode,
            ushort flags,
            byte keyDown);

        [LibraryImport(LibraryName, EntryPoint = "TerminalSendCharEvent")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial void TerminalSendCharEvent(
            nint terminal,
            ushort ch,
            ushort scanCode,
            ushort flags);

        [LibraryImport(LibraryName, EntryPoint = "TerminalSetFocused")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial void TerminalSetFocused(nint terminal, byte focused);

        private static int _resolverRegistered;
        private static int _tsfInitialized;
    }
}
