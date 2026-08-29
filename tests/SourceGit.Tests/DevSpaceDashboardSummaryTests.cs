using SourceGit.Models;
using SourceGit.ViewModels;
using Xunit;

namespace SourceGit.Tests
{
    public sealed class DevSpaceDashboardSummaryTests
    {
        [Fact]
        public void BuildGitSummaryAggregatesUniquePathsAndStates()
        {
            var staged = new[]
            {
                new Change { Path = "src/added.cs", Index = ChangeState.Added },
                new Change { Path = "src/both.cs", Index = ChangeState.Modified },
            };
            var unstaged = new[]
            {
                new Change { Path = "src/both.cs", WorkTree = ChangeState.Modified },
                new Change { Path = "src/deleted.cs", WorkTree = ChangeState.Deleted },
                new Change { Path = "src/renamed.cs", WorkTree = ChangeState.Renamed },
                new Change { Path = "src/new.cs", WorkTree = ChangeState.Untracked },
            };

            var summary = DevSpaceDashboard.BuildGitSummary(staged, unstaged);

            Assert.Equal(5, summary.Total);
            Assert.Equal(2, summary.Added);
            Assert.Equal(1, summary.Modified);
            Assert.Equal(1, summary.Deleted);
            Assert.Equal(1, summary.Renamed);
            Assert.Equal(2, summary.Staged);
            Assert.Equal(4, summary.Unstaged);
        }

        [Fact]
        public void ActivityIsNewestFirstAndCappedAtTwenty()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sourcegit-dashboard-summary-{System.Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(root);
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                for (var i = 0; i < 25; i++)
                    spaces.Dashboard.AddActivity(DevSpaceActivityKind.FileOpened, $"file-{i}");

                Assert.Equal(20, spaces.Dashboard.Activity.Count);
                Assert.Equal("file-24", spaces.Dashboard.Activity[0].Text);
                Assert.Equal("file-5", spaces.Dashboard.Activity[19].Text);
            }
            finally
            {
                System.IO.Directory.Delete(root, true);
            }
        }

        private sealed class FakeLauncher : SourceGit.DevSpaces.IDevSpaceSessionLauncher
        {
            public SourceGit.DevSpaces.DevSpaceLaunchSpec Create(string terminal, string workingDirectory, string startupCommand = null) =>
                new(terminal ?? string.Empty, [], workingDirectory);
        }
    }
}
