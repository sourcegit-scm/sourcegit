using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using SourceGit.AI;
using Xunit;

namespace SourceGit.Tests;

public class LocalLlmServiceSettingsTests
{
    [Fact]
    public void LocalLlmSettings_HaveApprovedDefaults()
    {
        var service = new Service();
        var type = typeof(Service);

        Assert.Equal("OpenAI", Read(type, service, "Provider")?.ToString());
        Assert.Equal(0.2f, Assert.IsType<float>(Read(type, service, "Temperature")));
        Assert.Equal((uint)10000, Assert.IsType<uint>(Read(type, service, "ContextWindow")));
        Assert.True(Assert.IsType<bool>(Read(type, service, "AutoLoadModel")));
        Assert.Equal(string.Empty, Assert.IsType<string>(Read(type, service, "LocalModelPath")));
    }

    [Fact]
    public void LocalLlmRuntimeSettings_HaveAiStudioCompatibleDefaults()
    {
        using var service = new Service();
        var type = typeof(Service);

        Assert.Equal("Auto", Read(type, service, "LocalBackend")?.ToString());
        Assert.Equal(-1, Assert.IsType<int>(Read(type, service, "GpuLayerCount")));
        Assert.True(Assert.IsType<int>(Read(type, service, "LocalThreads")) >= 1);
        Assert.Equal((uint)512, Assert.IsType<uint>(Read(type, service, "LocalBatchSize")));
    }

    [Fact]
    public void LocalLlmSettings_RoundTripThroughJson()
    {
        var service = new Service
        {
            Provider = ProviderType.LocalLlm,
            LocalModelPath = "/models/qwen.gguf",
            Temperature = 0.35f,
            ContextWindow = 16384,
            AutoLoadModel = false,
        };

        var json = JsonSerializer.Serialize(service);
        var restored = JsonSerializer.Deserialize<Service>(json);

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
        using var service = new Service { Provider = ProviderType.LocalLlm };
        var type = typeof(Service);
        Write(type, service, "LocalBackend", "Cuda");
        Write(type, service, "GpuLayerCount", 24);
        Write(type, service, "LocalThreads", 6);
        Write(type, service, "LocalBatchSize", (uint)256);

        var json = JsonSerializer.Serialize(service);
        using var restored = JsonSerializer.Deserialize<Service>(json);

        Assert.NotNull(restored);
        Assert.Equal("Cuda", Read(type, restored, "LocalBackend")?.ToString());
        Assert.Equal(24, Assert.IsType<int>(Read(type, restored, "GpuLayerCount")));
        Assert.Equal(6, Assert.IsType<int>(Read(type, restored, "LocalThreads")));
        Assert.Equal((uint)256, Assert.IsType<uint>(Read(type, restored, "LocalBatchSize")));
    }

    [Fact]
    public void Temperature_IsClampedToSupportedRange()
    {
        var service = new Service { Temperature = 3.5f };
        Assert.Equal(2.0f, service.Temperature);

        service.Temperature = -1.0f;
        Assert.Equal(0.0f, service.Temperature);
    }

    [Fact]
    public void ContextWindow_HasSafeMinimum()
    {
        var service = new Service { ContextWindow = 1 };
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

    private static object? Read(Type type, object instance, string propertyName)
    {
        var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return property.GetValue(instance);
    }

    private static void Write(Type type, object instance, string propertyName, object value)
    {
        var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);

        var converted = property.PropertyType.IsEnum && value is string text
            ? Enum.Parse(property.PropertyType, text)
            : value;
        property.SetValue(instance, converted);
    }
}
