using System;
using System.Runtime.CompilerServices;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Mcp
{
    internal static class SourceGitMcpBootstrap
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            Control.LoadedEvent.AddClassHandler<Views.Launcher>(OnLauncherLoaded);
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        private static void OnLauncherLoaded(Views.Launcher view, RoutedEventArgs e)
        {
            SourceGitMcpService.Initialize(SourceGitMcpSettings.Instance);
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            try
            {
                SourceGitMcpService.ShutdownAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // MCP is optional and must never block SourceGit process shutdown.
            }
        }
    }
}
