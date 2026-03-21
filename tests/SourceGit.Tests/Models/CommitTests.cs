using SourceGit.Models;
using Xunit;

namespace SourceGit.Tests.Models
{
    public class CommitTests
    {
        [Fact]
        public void ParseParents_AddsSingleParent()
        {
            var commit = new Commit();
            commit.ParseParents("abc1234567890");

            Assert.Single(commit.Parents);
            Assert.Equal("abc1234567890", commit.Parents[0]);
        }

        [Fact]
        public void ParseParents_AddsMultipleParents_ForMergeCommit()
        {
            var commit = new Commit();
            commit.ParseParents("aaaaaaaaaa bbbbbbbbbb");

            Assert.Equal(2, commit.Parents.Count);
            Assert.Equal("aaaaaaaaaa", commit.Parents[0]);
            Assert.Equal("bbbbbbbbbb", commit.Parents[1]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")] // length < 8 → skip
        public void ParseParents_DoesNothing_WhenDataIsTooShortOrEmpty(string data)
        {
            var commit = new Commit();
            commit.ParseParents(data);

            Assert.Empty(commit.Parents);
        }

        [Theory]
        [InlineData("HEAD -> refs/heads/main", DecoratorType.CurrentBranchHead, "main", true)]
        [InlineData("HEAD", DecoratorType.CurrentCommitHead, "HEAD", true)]
        [InlineData("tag: refs/tags/v1.0.0", DecoratorType.Tag, "v1.0.0", false)]
        [InlineData("refs/heads/feature/my-feature", DecoratorType.LocalBranchHead, "feature/my-feature", false)]
        [InlineData("refs/remotes/origin/main", DecoratorType.RemoteBranchHead, "origin/main", false)]
        public void ParseDecorators_ParsesSingleDecorator(string data, DecoratorType expectedType, string expectedName, bool expectedIsMerged)
        {
            var commit = new Commit();
            commit.ParseDecorators(data);

            Assert.Single(commit.Decorators);
            Assert.Equal(expectedType, commit.Decorators[0].Type);
            Assert.Equal(expectedName, commit.Decorators[0].Name);
            Assert.Equal(expectedIsMerged, commit.IsMerged);
        }

        [Fact]
        public void ParseDecorators_SkipsRemoteHeadEntries()
        {
            // entries ending in /HEAD should be ignored
            var commit = new Commit();
            commit.ParseDecorators("refs/remotes/origin/HEAD");

            Assert.Empty(commit.Decorators);
        }

        [Fact]
        public void ParseDecorators_ParsesMultipleEntries_AndSortsThem()
        {
            var commit = new Commit();
            commit.ParseDecorators("HEAD -> refs/heads/main, refs/heads/feature, tag: refs/tags/v2.0");

            // CurrentBranchHead(1) < LocalBranchHead(2) < Tag(5) per enum order
            Assert.Equal(3, commit.Decorators.Count);
            Assert.Equal(DecoratorType.CurrentBranchHead, commit.Decorators[0].Type);
            Assert.Equal(DecoratorType.LocalBranchHead, commit.Decorators[1].Type);
            Assert.Equal(DecoratorType.Tag, commit.Decorators[2].Type);
        }

        [Theory]
        [InlineData("")]
        [InlineData("ab")] // length < 3 → skip
        public void ParseDecorators_DoesNothing_WhenDataIsTooShortOrEmpty(string data)
        {
            var commit = new Commit();
            commit.ParseDecorators(data);

            Assert.Empty(commit.Decorators);
        }

        [Theory]
        [InlineData(DecoratorType.LocalBranchHead, "main", "main")]
        [InlineData(DecoratorType.RemoteBranchHead, "origin/main", "origin/main")]
        [InlineData(DecoratorType.Tag, "v1.0.0", "v1.0.0")]
        public void GetFriendlyName_ReturnsDecoratorName(DecoratorType type, string name, string expected)
        {
            var commit = new Commit { SHA = "abcdefghij1234567890" };
            commit.Decorators.Add(new Decorator { Type = type, Name = name });

            Assert.Equal(expected, commit.GetFriendlyName());
        }

        [Fact]
        public void GetFriendlyName_ReturnsFirstTenCharsOfSha_WhenNoDecoratorsPresent()
        {
            var commit = new Commit { SHA = "abcdefghij1234567890" };

            Assert.Equal("abcdefghij", commit.GetFriendlyName());
        }
    }
}
