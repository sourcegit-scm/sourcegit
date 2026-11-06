using System.Linq;
using SourceGit.Models;
using Xunit;

namespace SourceGit.Tests.Models
{
    public class BranchTests
    {
        private static Branch MakeBranch(int ahead, int behind) => new()
        {
            Ahead = Enumerable.Repeat("x", ahead).ToList(),
            Behind = Enumerable.Repeat("x", behind).ToList(),
        };

        [Theory]
        [InlineData("main", "origin", true, "main")]
        [InlineData("main", "origin", false, "origin/main")]
        [InlineData("feature", "upstream", false, "upstream/feature")]
        public void FriendlyName_ReturnsExpected(string name, string remote, bool isLocal, string expected)
        {
            var branch = new Branch { Name = name, Remote = remote, IsLocal = isLocal };
            Assert.Equal(expected, branch.FriendlyName);
        }

        [Theory]
        [InlineData(0, 0, false)]
        [InlineData(1, 0, true)]
        [InlineData(0, 1, true)]
        [InlineData(1, 1, true)]
        public void IsTrackStatusVisible_ReturnsExpected(int ahead, int behind, bool expected)
        {
            Assert.Equal(expected, MakeBranch(ahead, behind).IsTrackStatusVisible);
        }

        [Theory]
        [InlineData(0, 0, "")]
        [InlineData(3, 0, "3↑")]
        [InlineData(0, 2, "2↓")]
        [InlineData(3, 2, "3↑ 2↓")]
        [InlineData(1, 0, "1↑")]
        [InlineData(0, 1, "1↓")]
        public void TrackStatusDescription_ReturnsExpected(int ahead, int behind, string expected)
        {
            Assert.Equal(expected, MakeBranch(ahead, behind).TrackStatusDescription);
        }
    }
}
