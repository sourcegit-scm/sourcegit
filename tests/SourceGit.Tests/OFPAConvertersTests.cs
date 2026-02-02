using System.Globalization;
using SourceGit.Converters;

namespace SourceGit.Tests;

public class OFPAConvertersTests
{
    [Fact]
    public void PathToDisplayName_FallsBack_WhenDecodedIsNull()
    {
        // Arrange
        var converter = new PathToDisplayNameConverter();
        var path = "Content/__ExternalActors__/Maps/Test/A.uasset";
        var decoded = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [path] = null!,
        };

        // Act
        var result = converter.Convert(
            new object?[] { path, decoded },
            typeof(string),
            "PureFileName",
            CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("A.uasset", result);
    }

    [Fact]
    public void PathToDisplayName_UsesDecoded_WhenDecodedIsNonEmpty()
    {
        // Arrange
        var converter = new PathToDisplayNameConverter();
        var path = "Content/__ExternalActors__/Maps/Test/B.uasset";
        var decoded = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [path] = "Actor_01",
        };

        // Act
        var result = converter.Convert(
            new object?[] { path, decoded },
            typeof(string),
            null,
            CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Actor_01", result);
    }
}
