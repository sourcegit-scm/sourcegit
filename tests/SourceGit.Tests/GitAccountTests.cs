using SourceGit.Models;
using Xunit;

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
    public void MatchesIdentity_ReturnsFalse_WhenIdentityDoesNotMatch(string userName, string email)
    {
        var account = new GitAccount
        {
            Name = "Personal",
            GitUserName = "Hieu Dam",
            GitEmail = "hieu@example.com",
        };

        Assert.False(account.MatchesIdentity(userName, email));
    }

    [Fact]
    public void Accounts_HaveStableUniqueIds()
    {
        var first = new GitAccount();
        var second = new GitAccount();

        Assert.False(string.IsNullOrWhiteSpace(first.Id));
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Resolver_PrefersConfiguredId_WhenCommitIdentityIsShared()
    {
        var personal = new GitAccount
        {
            GitUserName = "Hieu Dam",
            GitEmail = "shared@example.com",
            GitHubUserName = "personal-user",
        };
        var work = new GitAccount
        {
            GitUserName = "Hieu Dam",
            GitEmail = "shared@example.com",
            GitHubUserName = "work-user",
        };

        var resolved = GitAccountResolver.Resolve(
            [personal, work],
            work.Id,
            "Hieu Dam",
            "shared@example.com");

        Assert.Same(work, resolved);
    }

    [Fact]
    public void Resolver_FallsBackToIdentity_WhenRepositoryHasNoAccountId()
    {
        var account = new GitAccount
        {
            GitUserName = "Hieu Dam",
            GitEmail = "hieu@example.com",
        };

        var resolved = GitAccountResolver.Resolve(
            [account],
            null,
            "Hieu Dam",
            "hieu@example.com");

        Assert.Same(account, resolved);
    }

    [Fact]
    public void CredentialConfig_ScopesUsernameToGitHubHttpsUrl()
    {
        var keys = GitHubCredentialConfig.GetUsernameKeys(
            [
                "https://github.com/owner/repository.git",
                "https://gitlab.com/owner/repository.git",
                "git@github.com:owner/repository.git",
            ]);

        Assert.Equal(["credential.https://github.com.username"], keys);
    }

    [Fact]
    public void CredentialConfig_ReturnsDistinctKeysForGitHubHttpProtocols()
    {
        var keys = GitHubCredentialConfig.GetUsernameKeys(
            [
                "https://github.com/owner/repository.git",
                "http://github.com/owner/other.git",
                "https://github.com/owner/second.git",
            ]);

        Assert.Equal(
            [
                "credential.https://github.com.username",
                "credential.http://github.com.username",
            ],
            keys);
    }
}
