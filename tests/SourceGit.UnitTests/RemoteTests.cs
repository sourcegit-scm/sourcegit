using SourceGit.Models;

namespace SourceGit.UnitTests;

public class RemoteTests
{
    [Theory]
    [InlineData("git@github.com:sourcegit-scm/sourcegit.git")]
    [InlineData("https://github.com/sourcegit-scm/sourcegit.git")]
    [InlineData("git@ssh.dev.azure.com:v3/Organization/Project/Repository")]
    [InlineData("https://organization@dev.azure.com/Organization/Project/_git/Repository")]
    public void IsValidURL_WhenURLProvidedIsValid_ShouldReturnTrue(string url)
    {
        Assert.True(Remote.IsValidURL(url));
    }
}
