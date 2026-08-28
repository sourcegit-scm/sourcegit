using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

using Avalonia.Controls;
using Avalonia.Platform;

internal sealed class ProbeNativeHost : NativeControlHost
{
    public ProbeNativeHost(string helperPath)
    {
        _helperPath = helperPath;
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException();
        if (!File.Exists(_helperPath))
            throw new FileNotFoundException("Probe helper executable not found.", _helperPath);

        var psi = new ProcessStartInfo(_helperPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(parent.Handle.ToInt64().ToString(CultureInfo.InvariantCulture));

        _process = new Process
        {
            StartInfo = psi,
        };

        try
        {
            if (!_process.Start())
                throw new InvalidOperationException("Failed to start probe helper.");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var line = _process.StandardOutput.ReadLineAsync(cts.Token).AsTask().GetAwaiter().GetResult();
            const string prefix = "SOURCEGIT_TERMINAL_READY ";
            if (line == null ||
                !line.StartsWith(prefix, StringComparison.Ordinal) ||
                !long.TryParse(
                    line.AsSpan(prefix.Length),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var childValue) ||
                childValue == 0)
            {
                var stderr = _process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"Invalid helper handshake: '{line}'. {stderr}");
            }

            return new PlatformHandle(new IntPtr(childValue), "HWND");
        }
        catch
        {
            StopHelper();
            throw;
        }
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        // The HWND belongs to the helper process. Never call DestroyWindow on it here.
        StopHelper();
    }

    private void StopHelper()
    {
        var process = _process;
        _process = null;
        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Probe cleanup is best-effort.
        }
        finally
        {
            process.Dispose();
        }
    }

    private readonly string _helperPath;
    private Process _process;
}
