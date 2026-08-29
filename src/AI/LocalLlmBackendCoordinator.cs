using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LLama.Native;

namespace SourceGit.AI
{
    internal readonly record struct LocalLlmBackendCapabilities(bool VulkanAvailable);

    internal static class LocalLlmBackendCoordinator
    {
        public static LocalLlmBackend ConfigureAndLock(LocalLlmBackend requested)
        {
            lock (_gate)
            {
                if (_isLocked)
                {
                    if (_requested != requested)
                        throw new InvalidOperationException($"Local LLM native backend is already locked to {_selected}; restart SourceGit to change it.");

                    return _selected!.Value;
                }

                var capabilities = ProbeCapabilities();
                var selected = requested switch
                {
                    LocalLlmBackend.Auto when capabilities.VulkanAvailable => LocalLlmBackend.Vulkan,
                    LocalLlmBackend.Auto => LocalLlmBackend.Cpu,
                    LocalLlmBackend.Vulkan when !capabilities.VulkanAvailable => throw new InvalidOperationException("Vulkan backend was explicitly requested but Vulkan is not available for this platform/architecture."),
                    _ => requested,
                };

                NativeLibraryConfig.All
                    .WithCuda(false)
                    .WithVulkan(selected == LocalLlmBackend.Vulkan)
                    .WithAutoFallback(false);

                _requested = requested;
                _selected = selected;
                _isLocked = true;
                return selected;
            }
        }

        private static LocalLlmBackendCapabilities ProbeCapabilities()
        {
            var isWindows = OperatingSystem.IsWindows();
            var isLinux = OperatingSystem.IsLinux();
            if (!SupportsBundledVulkan(RuntimeInformation.ProcessArchitecture, isWindows, isLinux))
                return new LocalLlmBackendCapabilities(false);

            var vulkanAvailable = CommandExists("vulkaninfo") || isWindows;
            return new LocalLlmBackendCapabilities(vulkanAvailable);
        }

        private static bool SupportsBundledVulkan(Architecture architecture, bool isWindows, bool isLinux)
        {
            return architecture == Architecture.X64 && (isWindows || isLinux);
        }

        private static bool CommandExists(string command)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "--summary",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                return process != null && process.WaitForExit(1500);
            }
            catch
            {
                return false;
            }
        }

        private static readonly object _gate = new();
        private static LocalLlmBackend? _requested;
        private static LocalLlmBackend? _selected;
        private static bool _isLocked;
    }
}
