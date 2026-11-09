using SourceGit.Models;
using Xunit;

namespace SourceGit.Tests.Models
{
    public class GitFlowTests
    {
        private static GitFlow FullyConfigured() => new()
        {
            Master        = "main",
            Develop       = "develop",
            FeaturePrefix = "feature/",
            ReleasePrefix = "release/",
            HotfixPrefix  = "hotfix/",
        };

        [Fact]
        public void IsValid_ReturnsTrue_WhenAllFieldsAreSet()
        {
            Assert.True(FullyConfigured().IsValid);
        }

        [Fact]
        public void IsValid_ReturnsFalse_WhenMasterIsEmpty()
        {
            var gf = FullyConfigured();
            gf.Master = "";
            Assert.False(gf.IsValid);
        }

        [Fact]
        public void IsValid_ReturnsFalse_WhenDevelopIsEmpty()
        {
            var gf = FullyConfigured();
            gf.Develop = "";
            Assert.False(gf.IsValid);
        }

        [Fact]
        public void IsValid_ReturnsFalse_WhenFeaturePrefixIsEmpty()
        {
            var gf = FullyConfigured();
            gf.FeaturePrefix = "";
            Assert.False(gf.IsValid);
        }

        [Fact]
        public void IsValid_ReturnsFalse_WhenReleasePrefixIsEmpty()
        {
            var gf = FullyConfigured();
            gf.ReleasePrefix = "";
            Assert.False(gf.IsValid);
        }

        [Fact]
        public void IsValid_ReturnsFalse_WhenHotfixPrefixIsEmpty()
        {
            var gf = FullyConfigured();
            gf.HotfixPrefix = "";
            Assert.False(gf.IsValid);
        }

        [Fact]
        public void IsValid_ReturnsFalse_WhenAllFieldsAreEmpty()
        {
            Assert.False(new GitFlow().IsValid);
        }

        [Theory]
        [InlineData(GitFlowBranchType.Feature, "feature/")]
        [InlineData(GitFlowBranchType.Release, "release/")]
        [InlineData(GitFlowBranchType.Hotfix, "hotfix/")]
        [InlineData(GitFlowBranchType.None, "")]
        public void GetPrefix_ReturnsExpectedPrefix(GitFlowBranchType type, string expected)
        {
            Assert.Equal(expected, FullyConfigured().GetPrefix(type));
        }
    }
}
