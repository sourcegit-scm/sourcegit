using System;
using System.IO;
using System.Text.Json;
using SourceGit.AI;
using Xunit;

namespace SourceGit.Tests;

public class LocalLlmServiceSettingsTests
{
    [Fact]
    public void LocalLlmSettings_HaveApprovedDefaults()
    {
        using var service = new Service();

        Assert.Equal(ProviderType.OpenAI, service.Provider);
        Assert.Equal(0.2f, service.Temperature);
        Assert.Equal((uint)10000, service.ContextWindow);
        Assert.True(service.AutoLoadModel);
        Assert.Equal(string.Empty, service.LocalModelPath);
    }

    [Fact]
    public void LocalLlmRuntimeSettings_HaveAiStudioCompatibleDefaults()
    {
        using var service = new Service();

        Assert.Equal(LocalLlmBackend.Auto, service.LocalBackend);
        Assert.Equal(-1, service.GpuLayerCount);
        Assert.True(service.LocalThreads >= 1);
        Assert.Equal((uint)512, service.LocalBatchSize);
    }

    [Fact]
    public void LocalLlmSettings_RoundTripThroughJson()
    {
        using var service = new Service
        {
            Provider = ProviderType.LocalLlm,
            LocalModelPath = "/models/qwen.gguf",
            Temperature = 0.35f,
            ContextWindow = 16384,
            AutoLoadModel = false,
        };

        var json = JsonSerializer.Serialize(service);
        using var restored = JsonSerializer.Deserialize<Service>(json);

        Assert.NotNull(restored);
        Assert.Equal(ProviderType.LocalLlm, restored.Provider);
        Assert.Equal("/models/qwen.gguf", restored.LocalModelPath);
        Assert.Equal(0.35f, restored.Temperature);
        Assert.Equal((uint)16384, restored.ContextWindow);
        Assert.False(restored.AutoLoadModel);
    }

    [Fact]
    public void LocalLlmRuntimeSettings_RoundTripThroughJson()
    {
        using var service = new Service
        {
            Provider = ProviderType.LocalLlm,
            LocalBackend = LocalLlmBackend.Cuda,
            GpuLayerCount = 24,
            LocalThreads = 6,
            LocalBatchSize = 256,
        };

        var json = JsonSerializer.Serialize(service);
        using var restored = JsonSerializer.Deserialize<Service>(json);

        Assert.NotNull(restored);
        Assert.Equal(LocalLlmBackend.Cuda, restored.LocalBackend);
        Assert.Equal(24, restored.GpuLayerCount);
        Assert.Equal(6, restored.LocalThreads);
        Assert.Equal((uint)256, restored.LocalBatchSize);
    }

    [Fact]
    public void Temperature_IsClampedToSupportedRange()
    {
        using var service = new Service { Temperature = 3.5f };
        Assert.Equal(2.0f, service.Temperature);

        service.Temperature = -1.0f;
        Assert.Equal(0.0f, service.Temperature);
    }

    [Fact]
    public void ContextWindow_HasSafeMinimum()
    {
        using var service = new Service { ContextWindow = 1 };
        Assert.Equal((uint)512, service.ContextWindow);
    }

    [Fact]
    public void MissingLocalModel_ReportsModelNotFoundWithoutLoading()
    {
        using var service = new Service
        {
            Provider = ProviderType.LocalLlm,
            LocalModelPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.gguf"),
            AutoLoadModel = true,
        };

        service.FetchAvailableModels();

        Assert.Equal("Model not found", service.LocalModelStatus);
        Assert.Equal("Model not found", service.GetLocalModelValidationError());
    }

    [Fact]
    public void LocalModelList_UsesConfiguredDefaultModel()
    {
        using var service = new Service
        {
            Provider = ProviderType.LocalLlm,
            LocalModelPath = Path.Combine(Path.GetTempPath(), "sourcegit-default.gguf"),
            AutoLoadModel = false,
        };

        service.FetchAvailableModels();

        Assert.Equal(["sourcegit-default.gguf"], service.AvailableModels);
    }
}
