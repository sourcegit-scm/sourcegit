using System;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;

using EasyWindowsTerminalControl;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 ||
            !long.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentValue))
        {
            Console.Error.WriteLine("Expected one decimal parent HWND argument.");
            return 2;
        }

        try
        {
            var app = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown,
            };

            var parameters = new HwndSourceParameters("SourceGit Windows Terminal Probe")
            {
                ParentWindow = new IntPtr(parentValue),
                WindowStyle = unchecked((int)0x50000000), // WS_CHILD | WS_VISIBLE
                Width = 800,
                Height = 480,
            };

            using var source = new HwndSource(parameters);
            var terminal = new EasyTerminalControl
            {
                StartupCommandLine = "cmd.exe",
                WorkingDirectory = Environment.CurrentDirectory,
            };
            source.RootVisual = terminal;

            Console.Out.WriteLine($"SOURCEGIT_TERMINAL_READY {source.Handle.ToInt64()}");
            Console.Out.Flush();

            app.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
