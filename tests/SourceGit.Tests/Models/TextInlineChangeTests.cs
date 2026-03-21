using System.Linq;
using SourceGit.Models;
using Xunit;

namespace SourceGit.Tests.Models
{
    public class TextInlineChangeTests
    {
        [Theory]
        [InlineData("hello world", "hello world")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void Compare_ReturnsEmpty_WhenStringsAreTheSame(string s1, string s2)
        {
            Assert.Empty(TextInlineChange.Compare(s1, s2));
        }

        [Fact]
        public void Compare_ReturnsSingleChange_WhenOneWordIsReplaced()
        {
            var changes = TextInlineChange.Compare("hello world", "hello there");

            Assert.Single(changes);

            var c = changes[0];
            // "world" starts at index 6 in "hello world", length 5
            Assert.Equal(6, c.DeletedStart);
            Assert.Equal(5, c.DeletedCount);
            // "there" starts at index 6 in "hello there", length 5
            Assert.Equal(6, c.AddedStart);
            Assert.Equal(5, c.AddedCount);
        }

        [Fact]
        public void Compare_ReportsFullReplacement_WhenStringsAreCompletelyDifferent()
        {
            var changes = TextInlineChange.Compare("abc", "xyz");
            Assert.NotEmpty(changes);

            var totalDeleted = changes.Sum(c => c.DeletedCount);
            Assert.Equal(3, totalDeleted);

            var totalAdded = changes.Sum(c => c.AddedCount);
            Assert.Equal(3, totalAdded);
        }

        [Fact]
        public void Compare_ReportsPureInsertion_WhenOldStringIsEmpty()
        {
            var changes = TextInlineChange.Compare("", "hello");
            Assert.NotEmpty(changes);

            var totalAdded = changes.Sum(c => c.AddedCount);
            Assert.Equal(5, totalAdded);

            var totalDeleted = changes.Sum(c => c.DeletedCount);
            Assert.Equal(0, totalDeleted);
        }

        [Fact]
        public void Compare_ReportsPureDeletion_WhenNewStringIsEmpty()
        {
            var changes = TextInlineChange.Compare("hello", "");
            Assert.NotEmpty(changes);

            var totalDeleted = changes.Sum(c => c.DeletedCount);
            Assert.Equal(5, totalDeleted);

            var totalAdded = changes.Sum(c => c.AddedCount);
            Assert.Equal(0, totalAdded);
        }

        [Fact]
        public void Compare_DetectsSingleCharacterChange()
        {
            var changes = TextInlineChange.Compare("cat", "car");
            Assert.NotEmpty(changes);
        }
    }
}
