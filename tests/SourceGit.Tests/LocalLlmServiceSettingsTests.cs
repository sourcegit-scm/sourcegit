using System.Reflection;
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

    private static object? Read(Type type, object instance, string propertyName)
    {
        var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return property.GetValue(instance);
    }
}
