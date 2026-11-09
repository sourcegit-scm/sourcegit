using SourceGit.Models;
using Xunit;

namespace SourceGit.Tests.Models
{
    public class NumericSortTests
    {
        [Theory]
        [InlineData("file2", "file10")] // natural sort: 2 < 10
        [InlineData("file1", "file2")] // natural sort: 1 < 2
        [InlineData("9", "10")] // pure numeric: shorter digit-run loses
        [InlineData("a", "b")]
        [InlineData("", "a")]
        [InlineData("v1.2.3", "v1.10.0")] // mixed alpha/numeric segments
        [InlineData("img1a", "img1b")] // alpha suffix after numeric
        public void Compare_ReturnsNegative_WhenFirstComesBeforeSecond(string s1, string s2)
        {
            Assert.True(NumericSort.Compare(s1, s2) < 0);
        }

        [Theory]
        [InlineData("file10", "file2")] // natural sort: 10 > 2
        [InlineData("file2", "file1")] // natural sort: 2 > 1
        [InlineData("10", "9")]
        [InlineData("b", "a")]
        [InlineData("a", "")]
        public void Compare_ReturnsPositive_WhenFirstComesAfterSecond(string s1, string s2)
        {
            Assert.True(NumericSort.Compare(s1, s2) > 0);
        }

        [Theory]
        [InlineData("file1", "file1")]
        [InlineData("abc", "abc")]
        [InlineData("42", "42")]
        [InlineData("", "")]
        [InlineData("ABC", "abc")]
        [InlineData("File", "file")]
        public void Compare_ReturnsZero_WhenStringsAreEqual(string s1, string s2)
        {
            Assert.Equal(0, NumericSort.Compare(s1, s2));
        }
    }
}
