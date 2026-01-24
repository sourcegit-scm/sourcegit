using SourceGit.Utilities;

namespace SourceGit.Tests;

public class OFPAParserTests
{
    private static string GetTestDataPath(string relativePath)
    {
        return Path.Combine(AppContext.BaseDirectory, "TestData", relativePath);
    }

    [Fact]
    public void IsOFPAFile_WithExternalActorsPath_ReturnsTrue()
    {
        // Arrange
        var path = "Content/__ExternalActors__/Maps/Test/ABC123.uasset";

        // Act
        var result = OFPAParser.IsOFPAFile(path);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsOFPAFile_WithExternalObjectsPath_ReturnsTrue()
    {
        // Arrange
        var path = "Content/__ExternalObjects__/Blueprints/XYZ789.uasset";

        // Act
        var result = OFPAParser.IsOFPAFile(path);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsOFPAFile_WithRegularPath_ReturnsFalse()
    {
        // Arrange
        var path = "Content/Blueprints/BP_Player.uasset";

        // Act
        var result = OFPAParser.IsOFPAFile(path);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Decode_WithUE53File_ReturnsActorLabel()
    {
        // Arrange
        var filePath = GetTestDataPath("UE5_3/J28ZVKRUOZJY0PHKR205X.uasset");

        // Act
        var result = OFPAParser.Decode(filePath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ActorLabel", result.Value.LabelType);
        Assert.Equal("BP_IntroCameraActor2", result.Value.LabelValue);
    }

    [Fact]
    public void Decode_WithUE56File_ReturnsActorLabel()
    {
        // Arrange
        var filePath = GetTestDataPath("UE5_6/TIK1LLNYUFCW2RY3OQGQCH.uasset");

        // Act
        var result = OFPAParser.Decode(filePath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ActorLabel", result.Value.LabelType);
        Assert.Equal("LandscapeStreamingProxy_7_2_0", result.Value.LabelValue);
    }

    [Fact]
    public void Decode_WithUE57File_ReturnsFolderLabel()
    {
        // Arrange
        var filePath = GetTestDataPath("UE5_7/QD0WQDX4NT49M879U915NN.uasset");

        // Act
        var result = OFPAParser.Decode(filePath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("FolderLabel", result.Value.LabelType);
        Assert.Equal("Lighting", result.Value.LabelValue);
    }

    [Fact]
    public void Decode_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var filePath = GetTestDataPath("NonExistent/file.uasset");

        // Act
        var result = OFPAParser.Decode(filePath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Decode_WithInvalidFile_ReturnsNull()
    {
        // Arrange - create a temp file with invalid content
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, new byte[] { 0x00, 0x01, 0x02, 0x03 });

            // Act
            var result = OFPAParser.Decode(tempFile);

            // Assert
            Assert.Null(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void DecodeFromData_WithValidData_ReturnsLabel()
    {
        // Arrange
        var filePath = GetTestDataPath("UE5_3/J28ZVKRUOZJY0PHKR205X.uasset");
        var data = File.ReadAllBytes(filePath);

        // Act
        var result = OFPAParser.DecodeFromData(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("BP_IntroCameraActor2", result.Value.LabelValue);
    }
}
