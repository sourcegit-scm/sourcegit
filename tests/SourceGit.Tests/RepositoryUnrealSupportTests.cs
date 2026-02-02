using System.ComponentModel;
using SourceGit.ViewModels;

namespace SourceGit.Tests;

public class RepositoryUnrealSupportTests
{
    [Fact]
    public void WorkingCopy_RaisesIsUnrealEngineSupportEnabled_WhenRepositorySettingChanges()
    {
        // Arrange
        var repo = new Repository(false, "C:/tmp/repo", "C:/tmp/repo/.git");
        var workingCopy = new WorkingCopy(repo);
        var raised = false;

        workingCopy.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "IsUnrealEngineSupportEnabled")
                raised = true;
        };

        var property = typeof(Repository).GetProperty("EnableUnrealEngineSupport");

        // Act
        Assert.NotNull(property);
        property!.SetValue(repo, true);

        // Assert
        Assert.True(raised);
    }

    [Fact]
    public void EnableUnrealEngineSupport_TurnsOnOFPADecoding()
    {
        // Arrange
        var repo = new Repository(false, "C:/tmp/repo", "C:/tmp/repo/.git");
        var workingCopy = new WorkingCopy(repo);
        var ofpaRaised = false;

        workingCopy.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkingCopy.EnableOFPADecoding))
                ofpaRaised = true;
        };

        // Act
        repo.EnableUnrealEngineSupport = true;

        // Assert
        Assert.NotNull(repo.Settings);
        Assert.True(repo.Settings.EnableOFPADecoding);
        Assert.True(ofpaRaised);
    }
}
