using SourceGit.Models;
using Xunit;

namespace SourceGit.Tests.Models
{
    public class ChangeTests
    {
        [Fact]
        public void Set_Modified_LeavesPathUnchanged_AndOriginalPathEmpty()
        {
            var change = new Change { Path = "src/foo.cs" };
            change.Set(ChangeState.Modified);

            Assert.Equal("src/foo.cs", change.Path);
            Assert.Equal("", change.OriginalPath);
            Assert.Equal(ChangeState.Modified, change.Index);
            Assert.Equal(ChangeState.None, change.WorkTree);
        }

        [Theory]
        [InlineData(ChangeState.Renamed, ChangeState.None, "old/foo.cs\tnew/bar.cs", "old/foo.cs", "new/bar.cs")]
        [InlineData(ChangeState.Copied, ChangeState.None, "template/base.cs\tsrc/generated.cs", "template/base.cs", "src/generated.cs")]
        [InlineData(ChangeState.None, ChangeState.Renamed, "old/foo.cs\tnew/bar.cs", "old/foo.cs", "new/bar.cs")]
        public void Set_SplitsPath_OnTabSeparator(ChangeState index, ChangeState workTree, string path, string expectedOriginal, string expectedPath)
        {
            var change = new Change { Path = path };
            change.Set(index, workTree);

            Assert.Equal(expectedOriginal, change.OriginalPath);
            Assert.Equal(expectedPath, change.Path);
        }

        [Fact]
        public void Set_Renamed_SplitsPath_OnArrowSeparator()
        {
            var change = new Change { Path = "old/foo.cs -> new/bar.cs" };
            change.Set(ChangeState.Renamed);

            Assert.Equal("old/foo.cs", change.OriginalPath);
            Assert.Equal("new/bar.cs", change.Path);
        }

        [Fact]
        public void Set_Modified_StripsQuotes_WhenPathIsQuoted()
        {
            var change = new Change { Path = "\"src/path with spaces/foo.cs\"" };
            change.Set(ChangeState.Modified);

            Assert.Equal("src/path with spaces/foo.cs", change.Path);
        }

        [Fact]
        public void Set_Renamed_StripsQuotes_FromBothPaths()
        {
            var change = new Change { Path = "\"old/a b.cs\"\t\"new/c d.cs\"" };
            change.Set(ChangeState.Renamed);

            Assert.Equal("old/a b.cs", change.OriginalPath);
            Assert.Equal("new/c d.cs", change.Path);
        }

        [Fact]
        public void Set_StoresIndexAndWorkTreeStates()
        {
            var change = new Change { Path = "src/foo.cs" };
            change.Set(ChangeState.Added, ChangeState.Modified);

            Assert.Equal(ChangeState.Added, change.Index);
            Assert.Equal(ChangeState.Modified, change.WorkTree);
        }
    }
}
