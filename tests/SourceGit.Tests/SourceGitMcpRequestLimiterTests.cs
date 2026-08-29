using System;

using SourceGit.Mcp;
using Xunit;

namespace SourceGit.Tests;

public class SourceGitMcpRequestLimiterTests
{
    [Fact]
    public void TryEnter_rejects_requests_above_the_limit_until_a_lease_is_released()
    {
        var limiter = new SourceGitMcpRequestLimiter(1);

        Assert.True(limiter.TryEnter(out var first));
        Assert.False(limiter.TryEnter(out _));

        first.Dispose();

        Assert.True(limiter.TryEnter(out var second));
        second.Dispose();
    }

    [Fact]
    public void Constructor_rejects_non_positive_limits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceGitMcpRequestLimiter(0));
    }
}
