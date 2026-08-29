using SourceGit.Models;

namespace SourceGit.Tests;

public class GitAccountTests
{
    [Fact]
    public void MatchesIdentity_ReturnsTrue_ForMatchingNameAndEmail()
    {
        var account = new GitAccount
        {
            Name = "Personal",
            GitUserName = "Hieu Dam",
            GitEmail = "86464713+dhhieu113pro@users.noreply.github.com",
            GitHubUserName = "dhhieu113pro",
        };

        Assert.True(account.MatchesIdentity(
            "Hieu Dam",
            "86464713+dhhieu113pro@users.noreply.github.com"));
    }

    [Fact]
    public void MatchesIdentity_TreatsEmailAsCaseInsensitive()
    {
        var account = new GitAccount
        {
            Name = "Work",
            GitUserName = "Hieu Dam",
            GitEmail = "HIEU.DAM@example.com",
        };

        Assert.True(account.MatchesIdentity("Hieu Dam", "hieu.dam@example.com"));
    }

    [Theory]
    [InlineData("Other User", "hieu@example.com")]
    [InlineData("Hieu Dam", "other@example.com")]
    [InlineData(null, "hieu@example.com")]
    [InlineData("Hieu Dam", null)]
    public void MatchesIdentity_ReturnsFalse_WhenIdentityDoesNotMatch(string? userName, string? email)
    {
        var account = new GitAccount
        {
            Name = "Personal",
            GitUserName = "Hieu Dam",
            GitEmail = "hieu@example.com",
        };

        Assert.False(account.MatchesIdentity(userName, email));
    }
}
