using SourceGit.Models;
using Xunit;

namespace SourceGit.Tests;

public class WorktreeBaseBranchTests
{
    [Theory]
    [InlineData("develop", WorktreeBaseBranchKind.Develop)]
    [InlineData("DEVELOP", WorktreeBaseBranchKind.Develop)]
    [InlineData("master", WorktreeBaseBranchKind.Master)]
    [InlineData("Master", WorktreeBaseBranchKind.Master)]
    [InlineData("release/2.4", WorktreeBaseBranchKind.Release)]
    [InlineData("RELEASE/2026.08", WorktreeBaseBranchKind.Release)]
    public void GetKind_classifies_supported_base_branches_case_insensitively(string branch, WorktreeBaseBranchKind expected)
    {
        Assert.Equal(expected, WorktreeBaseBranch.GetKind(branch));
    }

    [Theory]
    [InlineData("main")]
    [InlineData("feature/foo")]
    [InlineData("")]
    public void GetKind_returns_none_for_unsupported_base_branches(string branch)
    {
        Assert.Equal(WorktreeBaseBranchKind.None, WorktreeBaseBranch.GetKind(branch));
    }

    [Theory]
    [InlineData("develop", "develop")]
    [InlineData("origin/develop", "develop")]
    [InlineData("refs/heads/master", "master")]
    [InlineData("refs/remotes/origin/release/2.4", "release/2.4")]
    public void Normalize_removes_git_ref_and_remote_prefixes(string branch, string expected)
    {
        Assert.Equal(expected, WorktreeBaseBranch.Normalize(branch));
    }

    [Theory]
    [InlineData(WorktreeBaseBranchKind.Develop, "#E5484D")]
    [InlineData(WorktreeBaseBranchKind.Master, "#D6409F")]
    [InlineData(WorktreeBaseBranchKind.Release, "#F76B15")]
    [InlineData(WorktreeBaseBranchKind.None, "Transparent")]
    public void GetBadgeColor_returns_requested_branch_family_color(WorktreeBaseBranchKind kind, string expected)
    {
        Assert.Equal(expected, WorktreeBaseBranch.GetBadgeColor(kind));
    }

    [Fact]
    public void SelectBestCandidate_prefers_nearest_supported_ancestor()
    {
        var candidates = new[]
        {
            new WorktreeBaseBranchCandidate("master", 12),
            new WorktreeBaseBranchCandidate("release/2.4", 7),
            new WorktreeBaseBranchCandidate("develop", 3),
        };

        Assert.Equal("develop", WorktreeBaseBranch.SelectBestCandidate(candidates));
    }

    [Fact]
    public void SelectBestCandidate_ignores_unsupported_branches()
    {
        var candidates = new[]
        {
            new WorktreeBaseBranchCandidate("feature/other", 1),
            new WorktreeBaseBranchCandidate("master", 5),
        };

        Assert.Equal("master", WorktreeBaseBranch.SelectBestCandidate(candidates));
    }

    [Fact]
    public void SelectBestCandidate_returns_empty_when_no_supported_candidate_exists()
    {
        var candidates = new[]
        {
            new WorktreeBaseBranchCandidate("main", 1),
            new WorktreeBaseBranchCandidate("feature/foo", 2),
        };

        Assert.Equal(string.Empty, WorktreeBaseBranch.SelectBestCandidate(candidates));
    }
}
