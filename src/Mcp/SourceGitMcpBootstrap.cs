using System;
using System.Runtime.CompilerServices;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DevBoard.Mcp
{
    internal static class DevBoardMcpBootstrap
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            Control.LoadedEvent.AddClassHandler<Views.Launcher>(OnLauncherLoaded);
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        private static void OnLauncherLoaded(Views.Launcher view, RoutedEventArgs e)
        {
            DevBoardMcpService.Initialize(DevBoardMcpSettings.Instance);
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            try
            {
                DevBoardMcpService.ShutdownAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // MCP is optional and must never block DevBoard process shutdown.
            }
        }
    }
}
