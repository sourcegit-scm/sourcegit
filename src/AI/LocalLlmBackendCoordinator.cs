using System;
using System.Diagnostics;
using LLama.Native;

namespace SourceGit.AI
{
    internal readonly record struct LocalLlmBackendCapabilities(bool CudaAvailable, bool VulkanAvailable);

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
                    LocalLlmBackend.Auto when capabilities.CudaAvailable => LocalLlmBackend.Cuda,
                    LocalLlmBackend.Auto when capabilities.VulkanAvailable => LocalLlmBackend.Vulkan,
                    LocalLlmBackend.Auto => LocalLlmBackend.Cpu,
                    LocalLlmBackend.Cuda when !capabilities.CudaAvailable => throw new InvalidOperationException("CUDA backend was explicitly requested but CUDA is not available."),
                    LocalLlmBackend.Vulkan when !capabilities.VulkanAvailable => throw new InvalidOperationException("Vulkan backend was explicitly requested but Vulkan is not available."),
                    _ => requested,
                };

                NativeLibraryConfig.All
                    .WithCuda(selected == LocalLlmBackend.Cuda)
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
            var cudaAvailable = CommandExists("nvidia-smi") ||
                                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CUDA_PATH"));
            var vulkanAvailable = CommandExists("vulkaninfo") || OperatingSystem.IsWindows();
            return new LocalLlmBackendCapabilities(cudaAvailable, vulkanAvailable);
        }

        private static bool CommandExists(string command)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = command == "vulkaninfo" ? "--summary" : "--help",
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
