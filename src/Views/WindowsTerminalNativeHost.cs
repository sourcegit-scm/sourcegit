using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace SourceGit.Views
{
    internal sealed class WindowsTerminalNativeHost : NativeControlHost
    {
        private const uint WM_SETFOCUS = 0x0007;
        private const uint WM_KILLFOCUS = 0x0008;
        private const uint WM_MOUSEACTIVATE = 0x0021;
        private const uint WM_WINDOWPOSCHANGED = 0x0047;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;
        private const uint WM_SYSKEYDOWN = 0x0104;
        private const uint WM_SYSKEYUP = 0x0105;
        private const uint WM_MOUSEWHEEL = 0x020A;

        private const uint SWP_NOSIZE = 0x0001;
        private const int WHEEL_DELTA = 120;

        private const ushort VK_SHIFT = 0x10;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_INSERT = 0x2D;
        private const ushort VK_C = 0x43;
        private const ushort VK_V = 0x56;

        internal event Action<string> InputGenerated;
        internal event Action<int, int> TerminalResized;

        internal bool NativeCreated => _terminal != 0 && _hwnd != 0;

        internal Task WaitForNativeCreatedAsync(CancellationToken cancellationToken)
        {
            if (NativeCreated)
                return Task.CompletedTask;

            return _nativeCreated.Task.WaitAsync(cancellationToken);
        }

        internal void SendOutput(string text)
        {
            if (_terminal == 0 || string.IsNullOrEmpty(text))
                return;

            Native.WindowsTerminal.TerminalSendOutput(_terminal, text);
        }

        internal async Task CopySelectionAsync()
        {
            if (_terminal == 0 || Native.WindowsTerminal.TerminalIsSelectionActive(_terminal) == 0)
                return;

            var pointer = Native.WindowsTerminal.TerminalGetSelection(_terminal);
            if (pointer == 0)
                return;

            string text;
            try
            {
                text = Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pointer);
            }

            if (string.IsNullOrEmpty(text))
                return;

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(text);
        }

        internal async Task PasteAsync()
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
                return;

#pragma warning disable CS0618 // Type or member is obsolete
            var text = await clipboard.GetTextAsync();
#pragma warning restore CS0618 // Type or member is obsolete
            if (!string.IsNullOrEmpty(text))
                InputGenerated?.Invoke(text);
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            try
            {
                Native.WindowsTerminal.EnsureResolver();
                Native.WindowsTerminal.AvoidBuggyTSFConsoleFlagsOnce();
                Native.WindowsTerminal.CreateTerminalChecked(parent.Handle, out _hwnd, out _terminal);

                _writeCallback = OnNativeWrite;
                _scrollCallback = OnScroll;
                Native.WindowsTerminal.TerminalRegisterWriteCallback(_terminal, _writeCallback);
                Native.WindowsTerminal.TerminalRegisterScrollCallback(_terminal, _scrollCallback);
                _subclass = Native.WindowsTerminalSubclass.Attach(_hwnd, HandleWindowMessage);

                AttachTopLevel();
                ApplyDpi();
                _nativeCreated.TrySetResult();
                return new PlatformHandle(_hwnd, "HWND");
            }
            catch (Exception ex)
            {
                CleanupNativeTerminal();
                _nativeCreated.TrySetException(ex);

                // Keep Avalonia's native-host attachment valid long enough for the surface
                // startup task to observe the failure and replace this host with the fallback.
                return base.CreateNativeControlCore(parent);
            }
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            if (_terminal != 0 || _hwnd != 0)
            {
                CleanupNativeTerminal();
                return;
            }

            base.DestroyNativeControlCore(control);
        }

        private void OnNativeWrite(nint text)
        {
            if (text == 0)
                return;

            try
            {
                var value = Marshal.PtrToStringUni(text);
                if (!string.IsNullOrEmpty(value))
                    InputGenerated?.Invoke(value);
            }
            finally
            {
                Marshal.FreeCoTaskMem(text);
            }
        }

        private void OnScroll(int viewTop, int viewHeight, int bufferSize)
        {
            Volatile.Write(ref _viewTop, viewTop);
            Volatile.Write(ref _viewHeight, viewHeight);
            Volatile.Write(ref _bufferSize, bufferSize);
        }

        private nint? HandleWindowMessage(uint message, nuint wParam, nint lParam)
        {
            if (_terminal == 0)
                return null;

            switch (message)
            {
                case WM_SETFOCUS:
                    Native.WindowsTerminal.TerminalSetFocused(_terminal, 1);
                    return null;
                case WM_KILLFOCUS:
                    Native.WindowsTerminal.TerminalSetFocused(_terminal, 0);
                    return null;
                case WM_MOUSEACTIVATE:
                    Native.WindowsTerminalSubclass.Focus(_hwnd);
                    return null;
                case WM_KEYDOWN:
                case WM_SYSKEYDOWN:
                    return HandleKeyDown(wParam, lParam);
                case WM_KEYUP:
                case WM_SYSKEYUP:
                    return HandleKeyUp(wParam, lParam);
                case WM_CHAR:
                    return HandleChar(wParam, lParam);
                case WM_WINDOWPOSCHANGED:
                    HandleWindowPositionChanged(lParam);
                    return null;
                case WM_MOUSEWHEEL:
                    HandleMouseWheel(wParam);
                    return null;
                default:
                    return null;
            }
        }

        private nint HandleKeyDown(nuint wParam, nint lParam)
        {
            UnpackKeyMessage(wParam, lParam, out var virtualKey, out var scanCode, out var flags);

            if (TryHandleShortcut(virtualKey))
                return 0;

            Native.WindowsTerminal.TerminalSendKeyEvent(_terminal, virtualKey, scanCode, flags, 1);
            return 0;
        }

        private nint HandleKeyUp(nuint wParam, nint lParam)
        {
            UnpackKeyMessage(wParam, lParam, out var virtualKey, out var scanCode, out var flags);

            if (_consumedKeys.Remove(virtualKey))
            {
                _suppressNextChar = false;
                return 0;
            }

            Native.WindowsTerminal.TerminalSendKeyEvent(_terminal, virtualKey, scanCode, flags, 0);
            return 0;
        }

        private nint HandleChar(nuint wParam, nint lParam)
        {
            if (_suppressNextChar)
            {
                _suppressNextChar = false;
                return 0;
            }

            UnpackKeyMessage(wParam, lParam, out var character, out var scanCode, out var flags);
            Native.WindowsTerminal.TerminalSendCharEvent(_terminal, character, scanCode, flags);
            return 0;
        }

        private bool TryHandleShortcut(ushort virtualKey)
        {
            if (_consumedKeys.Contains(virtualKey))
                return true;

            var control = Native.WindowsTerminalSubclass.IsKeyDown(VK_CONTROL);
            var shift = Native.WindowsTerminalSubclass.IsKeyDown(VK_SHIFT);

            if (virtualKey == VK_C && control)
            {
                var hasSelection = Native.WindowsTerminal.TerminalIsSelectionActive(_terminal) != 0;
                if (!shift && !hasSelection)
                    return false;

                if (hasSelection)
                    QueueClipboardOperation(CopySelectionAsync);

                ConsumeShortcut(virtualKey, suppressCharacter: true);
                return true;
            }

            if (virtualKey == VK_V && control && shift)
            {
                QueueClipboardOperation(PasteAsync);
                ConsumeShortcut(virtualKey, suppressCharacter: true);
                return true;
            }

            if (virtualKey == VK_INSERT && shift)
            {
                QueueClipboardOperation(PasteAsync);
                ConsumeShortcut(virtualKey, suppressCharacter: false);
                return true;
            }

            return false;
        }

        private void ConsumeShortcut(ushort virtualKey, bool suppressCharacter)
        {
            _consumedKeys.Add(virtualKey);
            _suppressNextChar = suppressCharacter;
        }

        private void QueueClipboardOperation(Func<Task> operation)
        {
            Dispatcher.UIThread.Post(() => _ = RunClipboardOperationAsync(operation));
        }

        private static async Task RunClipboardOperationAsync(Func<Task> operation)
        {
            try
            {
                await operation();
            }
            catch
            {
                // Clipboard access can fail when another process owns it. The PTY stays alive.
            }
        }

        private void HandleWindowPositionChanged(nint lParam)
        {
            if (_resizeInProgress || lParam == 0)
                return;

            var position = Marshal.PtrToStructure<WindowPos>(lParam);
            if ((position.Flags & SWP_NOSIZE) != 0 || position.Width <= 0 || position.Height <= 0)
                return;

            _resizeInProgress = true;
            try
            {
                var dimensions = Native.WindowsTerminal.TriggerResizeChecked(
                    _terminal,
                    position.Width,
                    position.Height);
                var cols = Math.Max(1, dimensions.X);
                var rows = Math.Max(1, dimensions.Y);
                TerminalResized?.Invoke(cols, rows);
            }
            finally
            {
                _resizeInProgress = false;
            }
        }

        private void HandleMouseWheel(nuint wParam)
        {
            var delta = unchecked((short)((wParam >> 16) & 0xFFFF));
            if (delta == 0)
                return;

            _wheelDelta += delta;
            var steps = _wheelDelta / WHEEL_DELTA;
            if (steps == 0)
                return;

            _wheelDelta -= steps * WHEEL_DELTA;

            var viewTop = Volatile.Read(ref _viewTop);
            var viewHeight = Volatile.Read(ref _viewHeight);
            var bufferSize = Volatile.Read(ref _bufferSize);
            if (viewHeight <= 0 || bufferSize <= viewHeight)
                return;

            var scrollLines = (int)Math.Min(
                Native.WindowsTerminalSubclass.GetWheelScrollLines(),
                int.MaxValue);
            var maxTop = Math.Max(0, bufferSize - viewHeight);
            var target = Math.Clamp(viewTop - (steps * scrollLines), 0, maxTop);
            if (target == viewTop)
                return;

            Volatile.Write(ref _viewTop, target);
            Native.WindowsTerminal.TerminalUserScroll(_terminal, target);
        }

        private void AttachTopLevel()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (ReferenceEquals(_topLevel, topLevel))
                return;

            if (_topLevel != null)
                _topLevel.PropertyChanged -= OnTopLevelPropertyChanged;

            _topLevel = topLevel;
            if (_topLevel != null)
                _topLevel.PropertyChanged += OnTopLevelPropertyChanged;
        }

        private void OnTopLevelPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "RenderScaling")
                ApplyDpi();
        }

        private void ApplyDpi()
        {
            if (_terminal == 0)
                return;

            var scaling = _topLevel?.RenderScaling ?? TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            var dpi = Math.Max(96, (int)Math.Round(scaling * 96.0));
            Native.WindowsTerminal.TerminalDpiChanged(_terminal, dpi);
        }

        private void CleanupNativeTerminal()
        {
            if (_topLevel != null)
            {
                _topLevel.PropertyChanged -= OnTopLevelPropertyChanged;
                _topLevel = null;
            }

            _consumedKeys.Clear();
            _suppressNextChar = false;
            _subclass?.Dispose();
            _subclass = null;

            if (_terminal != 0)
            {
                try
                {
                    Native.WindowsTerminal.DestroyTerminal(_terminal);
                }
                finally
                {
                    _terminal = 0;
                    _hwnd = 0;
                }
            }
            else
            {
                _hwnd = 0;
            }

            _writeCallback = null;
            _scrollCallback = null;
        }

        private static void UnpackKeyMessage(
            nuint wParam,
            nint lParam,
            out ushort virtualKey,
            out ushort scanCode,
            out ushort flags)
        {
            var raw = unchecked((ulong)(long)lParam);
            var scanCodeAndFlags = (raw >> 16) & 0xFFFF;
            scanCode = (ushort)(scanCodeAndFlags & 0x00FF);
            flags = (ushort)(scanCodeAndFlags & 0xFF00);
            virtualKey = (ushort)wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowPos
        {
            public nint Hwnd;
            public nint HwndInsertAfter;
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public uint Flags;
        }

        private readonly TaskCompletionSource _nativeCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly HashSet<ushort> _consumedKeys = [];

        private nint _terminal;
        private nint _hwnd;
        private Native.WindowsTerminal.WriteCallback _writeCallback;
        private Native.WindowsTerminal.ScrollCallback _scrollCallback;
        private Native.WindowsTerminalSubclass _subclass;
        private TopLevel _topLevel;
        private int _viewTop;
        private int _viewHeight;
        private int _bufferSize;
        private int _wheelDelta;
        private bool _resizeInProgress;
        private bool _suppressNextChar;
    }
}
