using SourceGit.Models;
using Xunit;

namespace SourceGit.Tests.Models
{
    public class InlineElementTests
    {
        private static InlineElement MakeElement(int start = 5, int length = 10) =>
            new(InlineElementType.Keyword, start, length, null);

        [Theory]
        [InlineData(5, 3)] // same start
        [InlineData(2, 5)] // overlaps from left
        [InlineData(12, 5)] // overlaps from right
        [InlineData(7, 2)] // fully contained inside
        [InlineData(0, 20)] // covers element completely
        [InlineData(5, 0)] // zero-length at element start: start == Start always returns true
        public void IsIntersecting_ReturnsTrue(int probeStart, int probeLength)
        {
            Assert.True(MakeElement().IsIntersecting(probeStart, probeLength));
        }

        [Theory]
        [InlineData(0, 4)] // completely before
        [InlineData(15, 3)] // completely after
        [InlineData(4, 0)] // zero-length adjacent before element start
        [InlineData(2, 3)] // ends exactly at element start
        public void IsIntersecting_ReturnsFalse(int probeStart, int probeLength)
        {
            Assert.False(MakeElement().IsIntersecting(probeStart, probeLength));
        }
    }
}
