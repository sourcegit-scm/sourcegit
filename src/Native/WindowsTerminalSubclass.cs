using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace DevBoard.Native
{
    internal sealed class WindowsTerminalSubclass : IDisposable
    {
        private const uint WM_NCDESTROY = 0x0082;
        private const uint SPI_GETWHEELSCROLLLINES = 0x0068;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate nint SubclassProc(
            nint hwnd,
            uint message,
            nuint wParam,
            nint lParam,
            nuint subclassId,
            nuint refData);

        internal static WindowsTerminalSubclass Attach(
            nint hwnd,
            Func<uint, nuint, nint, nint?> handler)
        {
            var instance = new WindowsTerminalSubclass(hwnd, handler);
            instance.AttachCore();
            return instance;
        }

        internal static bool IsKeyDown(int virtualKey)
        {
            return (GetKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
        }

        internal static void Focus(nint hwnd)
        {
            if (hwnd != 0)
                SetFocus(hwnd);
        }

        internal static uint GetWheelScrollLines()
        {
            if (!SystemParametersInfoW(SPI_GETWHEELSCROLLLINES, 0, out var lines, 0))
                return 3;

            return lines == uint.MaxValue ? 3 : Math.Max(1u, lines);
        }

        public void Dispose()
        {
            DetachCore(removeSubclass: true);
        }

        private WindowsTerminalSubclass(
            nint hwnd,
            Func<uint, nuint, nint, nint?> handler)
        {
            _hwnd = hwnd;
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _subclassId = unchecked((nuint)Interlocked.Increment(ref _nextSubclassId));
        }

        private void AttachCore()
        {
            _selfHandle = GCHandle.Alloc(this);
            var refData = unchecked((nuint)GCHandle.ToIntPtr(_selfHandle));

            if (SetWindowSubclass(_hwnd, SharedThunk, _subclassId, refData))
            {
                _attached = true;
                return;
            }

            _selfHandle.Free();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowSubclass failed.");
        }

        private void DetachCore(bool removeSubclass)
        {
            if (!_attached && !_selfHandle.IsAllocated)
                return;

            if (_attached && removeSubclass && _hwnd != 0)
                RemoveWindowSubclass(_hwnd, SharedThunk, _subclassId);

            _attached = false;
            _hwnd = 0;

            if (_selfHandle.IsAllocated)
                _selfHandle.Free();
        }

        private static nint SubclassThunk(
            nint hwnd,
            uint message,
            nuint wParam,
            nint lParam,
            nuint subclassId,
            nuint refData)
        {
            WindowsTerminalSubclass instance = null;

            try
            {
                if (refData != 0)
                {
                    var handle = GCHandle.FromIntPtr(unchecked((nint)refData));
                    instance = handle.Target as WindowsTerminalSubclass;
                }

                var handled = instance?._handler(message, wParam, lParam);
                var result = handled ?? DefSubclassProc(hwnd, message, wParam, lParam);

                if (message == WM_NCDESTROY)
                    instance?.DetachCore(removeSubclass: false);

                return result;
            }
            catch
            {
                if (message == WM_NCDESTROY)
                    instance?.DetachCore(removeSubclass: false);

                return DefSubclassProc(hwnd, message, wParam, lParam);
            }
        }

        [DllImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowSubclass(
            nint hwnd,
            SubclassProc subclassProc,
            nuint subclassId,
            nuint refData);

        [DllImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveWindowSubclass(
            nint hwnd,
            SubclassProc subclassProc,
            nuint subclassId);

        [DllImport("comctl32.dll")]
        private static extern nint DefSubclassProc(
            nint hwnd,
            uint message,
            nuint wParam,
            nint lParam);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern nint SetFocus(nint hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfoW(
            uint action,
            uint parameter,
            out uint value,
            uint flags);

        private static readonly SubclassProc SharedThunk = SubclassThunk;
        private static long _nextSubclassId;

        private readonly Func<uint, nuint, nint, nint?> _handler;
        private readonly nuint _subclassId;
        private GCHandle _selfHandle;
        private nint _hwnd;
        private bool _attached;
    }
}
