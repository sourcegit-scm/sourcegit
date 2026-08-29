using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform;

namespace SourceGit.Views
{
    internal sealed class WindowsTerminalNativeHost : NativeControlHost
    {
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
            _viewTop = viewTop;
            _viewHeight = viewHeight;
            _bufferSize = bufferSize;
        }

        private nint? HandleWindowMessage(uint message, nuint wParam, nint lParam)
        {
            // Task 4 adds the Windows Terminal key/focus/resize/scroll behavior here.
            return null;
        }

        private void ApplyDpi()
        {
            if (_terminal == 0)
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            var scaling = topLevel?.RenderScaling ?? 1.0;
            var dpi = Math.Max(96, (int)Math.Round(scaling * 96.0));
            Native.WindowsTerminal.TerminalDpiChanged(_terminal, dpi);
        }

        private void CleanupNativeTerminal()
        {
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

        private readonly TaskCompletionSource _nativeCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private nint _terminal;
        private nint _hwnd;
        private Native.WindowsTerminal.WriteCallback _writeCallback;
        private Native.WindowsTerminal.ScrollCallback _scrollCallback;
        private Native.WindowsTerminalSubclass _subclass;
        private int _viewTop;
        private int _viewHeight;
        private int _bufferSize;
    }
}
